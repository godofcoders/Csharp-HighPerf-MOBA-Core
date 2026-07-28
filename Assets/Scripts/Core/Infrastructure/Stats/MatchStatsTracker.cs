using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Match-scene MonoBehaviour that maintains per-brawler MatchStats for
    /// the duration of a match. Drop into the Match scene; auto-discovers
    /// brawlers on Start. MatchEndRouter snapshots the stats into
    /// MatchResultBoard before transitioning to Results.
    ///
    /// Tracks deaths, kills, assists, gems, and damage. Kept separate from
    /// Results UI so the match can snapshot data even after the Match scene
    /// unloads.
    /// </summary>
    public class MatchStatsTracker : MonoBehaviour
    {
        private const string MatchSceneName = "Match";
        private const uint AssistWindowTicks = 180;
        private const int RegistrationRefreshFrames = 30;

        public static MatchStatsTracker Instance { get; private set; }

        // Per-brawler running stats. Keyed by BrawlerController so the
        // Results screen can pair stats with display info (name + portrait).
        private readonly Dictionary<BrawlerController, MatchStats> _stats =
            new Dictionary<BrawlerController, MatchStats>(8);

        // Cached delegates so we can unsubscribe the SAME instance on
        // OnDisable (event-bus same-delegate-instance discipline).
        private Action<BrawlerState, int> _gemHandler;
        private Action<DamageResultContext> _damageHandler;
        private Action<BrawlerController, Vector3> _ballPickedUpHandler;
        private Action<BrawlerController, Vector3, Vector3, bool> _ballKickedHandler;
        private Action<TeamType, int, int> _goalScoredHandler;
        private Action<Vector3> _ballClearedHandler;
        private readonly List<Action> _deathHandlers = new List<Action>(8);
        private BrawlerController _lastBrawlBallScoringCandidate;

        public IReadOnlyDictionary<BrawlerController, MatchStats> Stats => _stats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene);
        }

        private static void TryInstallForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MatchSceneName)
                return;

            if (FindObjectOfType<MatchStatsTracker>() != null)
                return;

            GameObject host = new GameObject("MatchStatsTracker");
            host.AddComponent<MatchStatsTracker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Discover existing brawlers. Spawning order: SpawnManager
            // .PrepareMatch is called from MatchmakingManager.Start, which
            // runs before THIS Start in scene component order if we're
            // careful. Defensive Update path catches any post-Start spawns.
            DiscoverAndRegister();

            // Subscribe to GemEventBus once. Cache the delegate so we
            // remove the SAME instance on Disable.
            _gemHandler = HandleGemPickedUp;
            GemEventBus.OnGemPickedUp += _gemHandler;

            _damageHandler = HandleDamageApplied;
            DamageEventBus.OnDamageApplied += _damageHandler;

            _ballPickedUpHandler = HandleBrawlBallPickedUp;
            _ballKickedHandler = HandleBrawlBallKicked;
            _goalScoredHandler = HandleBrawlBallGoalScored;
            _ballClearedHandler = HandleBrawlBallCleared;
            BrawlBallEventBus.OnBallPickedUp += _ballPickedUpHandler;
            BrawlBallEventBus.OnBallKicked += _ballKickedHandler;
            BrawlBallEventBus.OnGoalScored += _goalScoredHandler;
            BrawlBallEventBus.OnBallDropped += _ballClearedHandler;
            BrawlBallEventBus.OnBallReset += _ballClearedHandler;
        }

        private void Update()
        {
            if (Time.frameCount % RegistrationRefreshFrames != 0)
                return;

            if (MatchManager.Instance != null &&
                MatchManager.Instance.CurrentState == MatchState.Ended)
            {
                return;
            }

            DiscoverAndRegister();
        }

        private void OnDisable()
        {
            if (_gemHandler != null)
            {
                GemEventBus.OnGemPickedUp -= _gemHandler;
                _gemHandler = null;
            }
            if (_damageHandler != null)
            {
                DamageEventBus.OnDamageApplied -= _damageHandler;
                _damageHandler = null;
            }
            if (_ballPickedUpHandler != null)
            {
                BrawlBallEventBus.OnBallPickedUp -= _ballPickedUpHandler;
                _ballPickedUpHandler = null;
            }
            if (_ballKickedHandler != null)
            {
                BrawlBallEventBus.OnBallKicked -= _ballKickedHandler;
                _ballKickedHandler = null;
            }
            if (_goalScoredHandler != null)
            {
                BrawlBallEventBus.OnGoalScored -= _goalScoredHandler;
                _goalScoredHandler = null;
            }
            if (_ballClearedHandler != null)
            {
                BrawlBallEventBus.OnBallDropped -= _ballClearedHandler;
                BrawlBallEventBus.OnBallReset -= _ballClearedHandler;
                _ballClearedHandler = null;
            }

            _lastBrawlBallScoringCandidate = null;

            // Death handlers are per-instance lambdas; the BrawlerState
            // references die with the scene so leaks are bounded to Match.
            // We still remove cleanly to support same-scene re-runs.
            // (Not perfectly safe across scene reloads — but the Tracker
            // itself doesn't survive scene reload either.)
            _deathHandlers.Clear();
        }

        private void DiscoverAndRegister()
        {
            BrawlerController[] all = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < all.Length; i++)
                Register(all[i]);
        }

        public void Register(BrawlerController brawler)
        {
            if (brawler == null || _stats.ContainsKey(brawler)) return;

            _stats[brawler] = default; // zero-init MatchStats

            if (brawler.State != null)
            {
                BrawlerController captured = brawler;
                Action handler = () => HandleDeath(captured);
                _deathHandlers.Add(handler);
                brawler.State.OnDeath += handler;
            }
        }

        private void HandleGemPickedUp(BrawlerState carrier, int amount)
        {
            if (carrier == null || amount <= 0) return;

            // Find the BrawlerController whose .State matches. Linear scan
            // of a small dictionary — fine for ≤6 brawlers per match.
            foreach (var kvp in _stats)
            {
                if (kvp.Key != null && kvp.Key.State == carrier)
                {
                    MatchStats s = kvp.Value;
                    s.GemsCollected += amount;
                    _stats[kvp.Key] = s;
                    return;
                }
            }
        }

        private void HandleDamageApplied(DamageResultContext result)
        {
            float dealt = result.FinalDamageApplied;
            if (dealt <= 0f) return;

            BrawlerController attacker = result.Damage.Attacker;
            BrawlerController victim = result.Damage.Target as BrawlerController;

            if (attacker != null && _stats.ContainsKey(attacker))
            {
                MatchStats s = _stats[attacker];
                s.DamageDealt += dealt;
                _stats[attacker] = s;
            }
            if (victim != null && _stats.ContainsKey(victim))
            {
                MatchStats s = _stats[victim];
                s.DamageTaken += dealt;
                _stats[victim] = s;
            }
        }

        private void HandleBrawlBallPickedUp(BrawlerController carrier, Vector3 position)
        {
            if (carrier != null)
                _lastBrawlBallScoringCandidate = carrier;
        }

        private void HandleBrawlBallKicked(
            BrawlerController kicker,
            Vector3 position,
            Vector3 direction,
            bool isSuperKick)
        {
            if (kicker != null)
                _lastBrawlBallScoringCandidate = kicker;
        }

        private void HandleBrawlBallGoalScored(TeamType scoringTeam, int blueGoals, int redGoals)
        {
            BrawlerController scorer = _lastBrawlBallScoringCandidate;
            _lastBrawlBallScoringCandidate = null;

            if (scorer == null ||
                scorer.Team != scoringTeam ||
                !_stats.ContainsKey(scorer))
            {
                return;
            }

            MatchStats stats = _stats[scorer];
            stats.GoalsScored += 1;
            _stats[scorer] = stats;
        }

        private void HandleBrawlBallCleared(Vector3 position)
        {
            _lastBrawlBallScoringCandidate = null;
        }

        private void HandleDeath(BrawlerController dying)
        {
            if (dying == null || !_stats.ContainsKey(dying)) return;

            MatchStats s = _stats[dying];
            s.Deaths += 1;
            _stats[dying] = s;

            // Kill credit. LastAttacker was stamped by DamageService.
            // Self-kill (no attacker) doesn't credit anyone.
            BrawlerController killer = dying.State != null ? dying.State.LastAttacker : null;
            if (killer != null && killer != dying && _stats.ContainsKey(killer))
            {
                MatchStats ks = _stats[killer];
                ks.Kills += 1;
                _stats[killer] = ks;
            }

            RecordAssists(dying, killer);
        }

        private void RecordAssists(BrawlerController dying, BrawlerController killer)
        {
            if (dying == null || dying.State == null || dying.State.AssistTracker == null)
                return;

            int killerId = killer != null ? killer.EntityID : 0;
            uint currentTick = dying.State.LastDamageTakenTick;
            if (ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock) && clock != null)
                currentTick = clock.CurrentTick;

            List<int> assists = dying.State.AssistTracker.GetAssistContributors(
                currentTick,
                AssistWindowTicks,
                killerId);

            for (int i = 0; i < assists.Count; i++)
            {
                BrawlerController assister = FindRegisteredBrawlerByEntityId(assists[i]);
                if (assister == null || assister == dying || assister == killer)
                    continue;

                MatchStats stats = _stats[assister];
                stats.Assists += 1;
                _stats[assister] = stats;
            }

            ListPool<int>.Release(assists);
        }

        private BrawlerController FindRegisteredBrawlerByEntityId(int entityId)
        {
            if (entityId == 0)
                return null;

            foreach (var kvp in _stats)
            {
                BrawlerController brawler = kvp.Key;
                if (brawler == null)
                    continue;

                if (brawler.EntityID == entityId)
                    return brawler;
            }

            return null;
        }

        /// <summary>Look up a brawler's current stats. Returns default
        /// (all zeros) if the brawler isn't registered.</summary>
        public MatchStats GetStats(BrawlerController brawler)
        {
            if (brawler == null) return default;
            return _stats.TryGetValue(brawler, out MatchStats s) ? s : default;
        }

        /// <summary>Best-MVP scorer across all tracked brawlers. Returns
        /// null if there are no registered brawlers.</summary>
        public BrawlerController FindMVP()
        {
            BrawlerController best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var kvp in _stats)
            {
                if (kvp.Key == null) continue;
                float score = kvp.Value.ComputeMvpScore();
                if (score > bestScore)
                {
                    bestScore = score;
                    best = kvp.Key;
                }
            }
            return best;
        }
    }
}
