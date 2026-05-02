using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Coordinator for the Gem Grab game mode. Glues together the
    /// per-brawler carrier state, the world-gem entities, and
    /// MatchManager scoring.
    ///
    /// Three responsibilities (Day 3 scope):
    ///   1. Death-drop. When a registered brawler dies with carried gems,
    ///      spawn a single Gem at their last position carrying the full
    ///      dropped count, then clear the brawler's carrier so the team
    ///      total drops correctly.
    ///   2. Team totals. Each tick, sum CarriedGemCount across each
    ///      team's living brawlers.
    ///   3. Win timer. When a team's total ≥ <see cref="_gemsToWin"/>,
    ///      start a <see cref="_winTimerSeconds"/> countdown. If they
    ///      drop below threshold, the timer resets. When the timer
    ///      expires, end the match with that team as winner.
    ///
    /// Brawl Stars rules pinned:
    ///   - 10 gems to win, 16-second hold timer (overrides PHASE_1_PLAN.md
    ///     which said 60s — that note predates the game-mode pass).
    ///   - Both teams holding ≥10 simultaneously: lower-total team's
    ///     timer wins (i.e. whoever crossed first), not implemented here
    ///     for Day 3 — defer to Day 4. For now the timer is single-team
    ///     and resets if the leader changes.
    ///
    /// Discovery: brawlers are auto-discovered via FindObjectsOfType at
    /// Start. If brawlers spawn dynamically post-Start, call
    /// <see cref="RegisterBrawler"/> manually.
    /// </summary>
    public sealed class GemGrabMode : MonoBehaviour
    {
        // Singleton, same shape as MatchManager. AI reads this to factor
        // gem-state into utility scoring; null-safe so non-Gem-Grab matches
        // (or unit tests with no scene) just see "no leader, zero gems".
        public static GemGrabMode Instance { get; private set; }

        [Header("Tuning")]
        [Tooltip("Gem prefab to spawn when a brawler dies with carried gems. Required.")]
        [SerializeField] private Gem _gemPrefab;

        [Tooltip("Gem count threshold to start the win-countdown.")]
        [Min(1)]
        [SerializeField] private int _gemsToWin = 10;

        [Tooltip("Seconds a team must hold ≥ GemsToWin gems for the match to end. Brawl Stars uses 16s.")]
        [Min(1f)]
        [SerializeField] private float _winTimerSeconds = 16f;

        [Tooltip("Total match length in seconds. On expiry, winner is decided by tiebreak (more gems → more team health → Blue). Brawl Stars uses 150s = 2:30.")]
        [Min(10f)]
        [SerializeField] private float _matchDurationSeconds = 150f;

        [Tooltip("Radius around the death position over which dropped gems are scattered. Each gem is placed at an even angle around the ring at this radius.")]
        [Min(0.1f)]
        [SerializeField] private float _deathDropScatterRadius = 0.7f;

        // Read-only views for HUD work in later sessions.
        public int BlueTeamGems { get; private set; }
        public int RedTeamGems { get; private set; }
        public TeamType LeadingTeam { get; private set; } = TeamType.Blue; // arbitrary default; gated by HasLeader
        public bool HasLeader { get; private set; }
        public float WinTimerRemainingSeconds { get; private set; }

        /// <summary>Seconds left in the match. Counts down once MatchManager
        /// is Active. On reaching 0, fires sudden-death tiebreak.</summary>
        public float MatchTimeRemainingSeconds { get; private set; }
        public bool MatchEndedByTimeout { get; private set; }

        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(8);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
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
            BrawlerController[] discovered = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < discovered.Length; i++)
                RegisterBrawler(discovered[i]);
        }

        /// <summary>Gem total for the supplied team. Used by AI scoring.</summary>
        public int GetTeamGemCount(TeamType team)
        {
            if (team == TeamType.Blue) return BlueTeamGems;
            if (team == TeamType.Red) return RedTeamGems;
            return 0;
        }

        /// <summary>True if the supplied team has fewer gems than the
        /// opposing team. AI uses this to decide whether to push for gems
        /// vs play safe.</summary>
        public bool IsTeamBehind(TeamType team)
        {
            int own = GetTeamGemCount(team);
            int opp = team == TeamType.Blue ? RedTeamGems : BlueTeamGems;
            return own < opp;
        }

        public void RegisterBrawler(BrawlerController brawler)
        {
            if (brawler == null || _brawlers.Contains(brawler))
                return;

            _brawlers.Add(brawler);

            // Capture for the closure; subscribe to OnDeath so we can drop
            // the brawler's gems at their last position. OnDeath fires
            // BEFORE Reset() (which is the respawn path), so CarriedGemCount
            // is still readable here.
            BrawlerController captured = brawler;
            if (captured.State != null)
                captured.State.OnDeath += () => HandleDeath(captured);
        }

        private void HandleDeath(BrawlerController dying)
        {
            if (dying == null || dying.State == null)
                return;

            int dropped = dying.State.CarriedGemCount;
            if (dropped <= 0)
                return;

            // Scatter N single-value gems on an evenly-spaced ring around
            // the death position. Even angles → deterministic placement
            // (no Random; future fixed-point determinism in Phase 2 stays
            // intact). N=1 collapses to a single gem at angle 0.
            if (_gemPrefab != null && dropped > 0)
            {
                Vector3 center = dying.transform.position;
                float angleStep = (Mathf.PI * 2f) / dropped;
                for (int i = 0; i < dropped; i++)
                {
                    float angle = angleStep * i;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle) * _deathDropScatterRadius,
                        0f,
                        Mathf.Sin(angle) * _deathDropScatterRadius);
                    Gem g = Object.Instantiate(_gemPrefab, center + offset, Quaternion.identity);
                    g.SetValue(1);
                }
            }

            // Clear the dying brawler's carrier immediately so the team
            // total query below sees the post-drop state.
            dying.State.CarriedGems.Clear();
        }

        private void Update()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.CurrentState != MatchState.Active)
                return;

            UpdateTeamTotals();
            UpdateWinTimer(Time.deltaTime);
            UpdateMatchTimer(Time.deltaTime);
        }

        private void UpdateMatchTimer(float deltaTime)
        {
            if (MatchEndedByTimeout)
                return;

            // Lazy-init on first Update once Active.
            if (MatchTimeRemainingSeconds <= 0f && !MatchEndedByTimeout)
            {
                MatchTimeRemainingSeconds = _matchDurationSeconds;
            }

            MatchTimeRemainingSeconds -= deltaTime;
            if (MatchTimeRemainingSeconds > 0f)
                return;

            // Time expired. If a team already won via the win-timer this
            // tick, MatchManager is already Ended and the AddScore below
            // is a no-op (its Active-state guard).
            MatchEndedByTimeout = true;
            MatchTimeRemainingSeconds = 0f;

            TeamType winner = ResolveSuddenDeathWinner();
            MatchManager.Instance?.AddScore(winner, _gemsToWin);
        }

        /// <summary>
        /// Tiebreak chain: (1) more gems wins; (2) higher total team
        /// remaining health wins; (3) Blue wins as a final deterministic
        /// fallback. Pure function over current state — no side effects.
        /// </summary>
        private TeamType ResolveSuddenDeathWinner()
        {
            if (BlueTeamGems != RedTeamGems)
                return BlueTeamGems > RedTeamGems ? TeamType.Blue : TeamType.Red;

            float blueHealth = SumTeamHealth(TeamType.Blue);
            float redHealth = SumTeamHealth(TeamType.Red);
            if (blueHealth != redHealth)
                return blueHealth > redHealth ? TeamType.Blue : TeamType.Red;

            return TeamType.Blue;
        }

        private float SumTeamHealth(TeamType team)
        {
            float total = 0f;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null || b.State == null || b.Team != team || b.State.IsDead)
                    continue;
                total += b.State.CurrentHealth;
            }
            return total;
        }

        private void UpdateTeamTotals()
        {
            int blue = 0;
            int red = 0;

            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null || b.State == null || b.State.IsDead)
                    continue;

                int n = b.State.CarriedGemCount;
                if (b.Team == TeamType.Blue) blue += n;
                else if (b.Team == TeamType.Red) red += n;
            }

            BlueTeamGems = blue;
            RedTeamGems = red;
        }

        private void UpdateWinTimer(float deltaTime)
        {
            // Determine current leader (if any has hit threshold).
            bool blueQualifies = BlueTeamGems >= _gemsToWin;
            bool redQualifies = RedTeamGems >= _gemsToWin;

            TeamType newLeader;
            bool newHasLeader;
            if (blueQualifies && !redQualifies) { newLeader = TeamType.Blue; newHasLeader = true; }
            else if (redQualifies && !blueQualifies) { newLeader = TeamType.Red; newHasLeader = true; }
            else if (blueQualifies && redQualifies)
            {
                // Both qualify: pick whichever has more, tie → no leader.
                if (BlueTeamGems > RedTeamGems) { newLeader = TeamType.Blue; newHasLeader = true; }
                else if (RedTeamGems > BlueTeamGems) { newLeader = TeamType.Red; newHasLeader = true; }
                else { newLeader = LeadingTeam; newHasLeader = false; }
            }
            else { newLeader = LeadingTeam; newHasLeader = false; }

            // If leader identity changed (or leader dropped), reset timer.
            if (!newHasLeader || newLeader != LeadingTeam)
            {
                WinTimerRemainingSeconds = newHasLeader ? _winTimerSeconds : 0f;
            }

            LeadingTeam = newLeader;
            HasLeader = newHasLeader;

            if (!HasLeader)
                return;

            WinTimerRemainingSeconds -= deltaTime;
            if (WinTimerRemainingSeconds <= 0f)
            {
                // Win condition fires. Push enough score to MatchManager to
                // trip its first-to-10 EndMatch path (it already handles
                // OnStateChanged → Ended).
                MatchManager.Instance?.AddScore(LeadingTeam, _gemsToWin);
                WinTimerRemainingSeconds = 0f;
            }
        }
    }
}
