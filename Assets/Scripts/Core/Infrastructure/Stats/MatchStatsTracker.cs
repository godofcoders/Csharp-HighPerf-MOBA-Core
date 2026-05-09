using System;
using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Match-scene MonoBehaviour that maintains per-brawler MatchStats for
    /// the duration of a match. Drop into the Match scene; auto-discovers
    /// brawlers on Start. MatchEndRouter snapshots the stats into
    /// MatchResultBoard before transitioning to Results.
    ///
    /// Phase 1 actively populates Deaths + GemsCollected. Damage and Kills
    /// are reserved for when the damage-event hookup lands; the fields
    /// are present so the wire shape doesn't change later.
    /// </summary>
    public class MatchStatsTracker : MonoBehaviour
    {
        public static MatchStatsTracker Instance { get; private set; }

        // Per-brawler running stats. Keyed by BrawlerController so the
        // Results screen can pair stats with display info (name + portrait).
        private readonly Dictionary<BrawlerController, MatchStats> _stats =
            new Dictionary<BrawlerController, MatchStats>(8);

        // Cached delegates so we can unsubscribe the SAME instance on
        // OnDisable (event-bus same-delegate-instance discipline).
        private Action<BrawlerState, int> _gemHandler;
        private readonly List<Action> _deathHandlers = new List<Action>(8);

        public IReadOnlyDictionary<BrawlerController, MatchStats> Stats => _stats;

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
        }

        private void OnDisable()
        {
            if (_gemHandler != null)
            {
                GemEventBus.OnGemPickedUp -= _gemHandler;
                _gemHandler = null;
            }
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
