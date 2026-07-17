using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Knockout coordinator. 3v3, no mid-round respawns, best-of-N rounds
    /// (default 3 → first to 2 round-wins takes the match).
    ///
    /// Drop into the Knockout mode prefab. On Awake disables
    /// SpawnManager.AllowAutoRespawn so dead brawlers stay dead inside a
    /// round. On round end (one team fully wiped), increments the surviving
    /// team's round score, checks the win threshold, otherwise respawns
    /// everyone for the next round.
    ///
    /// MatchManager integration: on best-of-N win, calls
    /// MatchManager.AddScore(winner, 10) which trips its existing
    /// EndMatch path (same trick GemGrabMode uses).
    /// </summary>
    public sealed class KnockoutMode : MonoBehaviour,
        IAIGameModeMacroStateProvider,
        IAIRuntimeObjectiveProvider
    {
        public static KnockoutMode Instance { get; private set; }

        [Header("Tuning")]
        [Min(1)]
        [SerializeField] private int _roundsToWin = 2;

        [Tooltip("Expected brawlers per team. Used by HUD before late-spawn discovery finishes.")]
        [Min(1)]
        [SerializeField] private int _teamSize = 3;

        [Tooltip("Seconds between a round ending and the next round starting (lets victory SFX play).")]
        [Min(0f)]
        [SerializeField] private float _interRoundDelaySeconds = 2.5f;

        [Tooltip("Keep scanning briefly for brawlers spawned after this mode prefab starts.")]
        [SerializeField] private bool _autoDiscoverBrawlers = true;

        [Header("AI Objective")]
        [Tooltip("Runtime objective weight used to pull bots toward center pressure before enemies are directly visible.")]
        [SerializeField, Min(0f)] private float _fightCenterObjectiveWeight = 128f;
        [SerializeField, Min(0.5f)] private float _fightCenterObjectiveRadius = 4.5f;
        [SerializeField, Min(0f)] private float _fightCenterPresenceRadius = 5.5f;
        [SerializeField, Range(0f, 1f)] private float _enemySideObjectiveBias = 0.56f;

        public int BlueRoundsWon { get; private set; }
        public int RedRoundsWon { get; private set; }
        public int CurrentRound { get; private set; } = 1;
        public int RoundsToWin => _roundsToWin;
        public int MaxRounds => Mathf.Max(1, (_roundsToWin * 2) - 1);
        public bool IsRoundEnding => _roundEnding;
        public GameModeId ModeId => GameModeId.Knockout;

        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(8);
        private readonly List<TeamType> _roundWinners = new List<TeamType>(3);
        private bool _roundEnding;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance == this)
            {
                ServiceProvider.Register<IAIGameModeMacroStateProvider>(this);
                ServiceProvider.Register<IAIRuntimeObjectiveProvider>(this);
            }
        }

        private void OnDisable()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (SpawnManager.Instance != null) SpawnManager.Instance.AllowAutoRespawn = true;
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void Start()
        {
            // Suppress mid-round respawns. SpawnManager normally auto-respawns
            // every dead brawler after a delay; Knockout disables that.
            if (SpawnManager.Instance != null) SpawnManager.Instance.AllowAutoRespawn = false;

            DiscoverBrawlers();
        }

        private void Update()
        {
            if (_autoDiscoverBrawlers && Time.frameCount % 30 == 0)
                DiscoverBrawlers();
        }

        private void DiscoverBrawlers()
        {
            BrawlerController[] discovered = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < discovered.Length; i++)
                RegisterBrawler(discovered[i]);

            if (_brawlers.Count > 0 &&
                GetRegisteredCount(TeamType.Blue) > 0 &&
                GetRegisteredCount(TeamType.Red) > 0)
            {
                _autoDiscoverBrawlers = false;
            }
        }

        public void RegisterBrawler(BrawlerController b)
        {
            if (b == null || _brawlers.Contains(b)) return;
            _brawlers.Add(b);
            if (b.State != null)
            {
                BrawlerController captured = b;
                captured.State.OnDeath += () => HandleDeath(captured);
            }
        }

        public int GetTeamRoundsWon(TeamType team)
        {
            if (team == TeamType.Blue)
                return BlueRoundsWon;

            if (team == TeamType.Red)
                return RedRoundsWon;

            return 0;
        }

        public TeamType GetRoundWinner(int roundIndex)
        {
            if (roundIndex < 0 || roundIndex >= _roundWinners.Count)
                return TeamType.Neutral;

            return _roundWinners[roundIndex];
        }

        public bool HasRoundResult(int roundIndex)
        {
            return roundIndex >= 0 && roundIndex < _roundWinners.Count;
        }

        public int GetDisplayTeamSize(TeamType team)
        {
            int registered = GetRegisteredCount(team);
            return Mathf.Max(1, Mathf.Max(_teamSize, registered));
        }

        public int GetEliminatedCount(TeamType team)
        {
            int registered = GetRegisteredCount(team);
            if (registered <= 0)
                return 0;

            return Mathf.Clamp(registered - GetAliveCount(team), 0, GetDisplayTeamSize(team));
        }

        public bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state)
        {
            state = AIGameModeMacroState.Neutral;
            if (team == TeamType.Neutral)
                return false;

            state = AIGameModeMacroStrategy.ResolveKnockout(this, team);
            return true;
        }

        public bool TryGetRuntimeObjective(
            TeamType team,
            AIObjectiveType preferredType,
            Vector3 selfPosition,
            out AIObjectiveCandidate objective)
        {
            objective = default;

            if (team == TeamType.Neutral || _roundEnding)
                return false;

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            if (!TryGetAliveCentroid(team, out Vector3 friendlyCenter, out int friendlyAlive) ||
                !TryGetAliveCentroid(enemyTeam, out Vector3 enemyCenter, out int enemyAlive))
            {
                return false;
            }

            Vector3 fightCenter = Vector3.Lerp(
                friendlyCenter,
                enemyCenter,
                Mathf.Clamp01(_enemySideObjectiveBias));

            int friendlyPresence = CountAliveNear(team, fightCenter, _fightCenterPresenceRadius);
            int enemyPresence = CountAliveNear(enemyTeam, fightCenter, _fightCenterPresenceRadius);
            TeamType controllingTeam = ResolveObjectiveControlTeam(
                friendlyPresence,
                enemyPresence,
                team,
                enemyTeam);

            AIObjectiveControlState controlState =
                AIObjectiveControlUtility.ResolveForTeam(
                    controllingTeam,
                    team,
                    friendlyPresence,
                    enemyPresence);

            float weight = _fightCenterObjectiveWeight;
            if (enemyAlive > friendlyAlive)
                weight += 18f;
            else if (friendlyAlive > enemyAlive)
                weight += 10f;

            if (controlState == AIObjectiveControlState.EnemyControlled)
                weight += 20f;
            else if (controlState == AIObjectiveControlState.Contested)
                weight += 14f;
            else if (controlState == AIObjectiveControlState.Neutral)
                weight += 10f;

            objective = new AIObjectiveCandidate(
                AIObjectiveType.MidControl,
                fightCenter,
                weight,
                _fightCenterObjectiveRadius,
                "Knockout Fight Center",
                true,
                controlState,
                friendlyPresence,
                enemyPresence);
            return true;
        }

        public int GetAliveCount(TeamType team)
        {
            int count = 0;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null || b.Team != team || b.State == null || b.State.IsDead)
                    continue;

                count++;
            }

            return count;
        }

        private bool TryGetAliveCentroid(TeamType team, out Vector3 centroid, out int aliveCount)
        {
            centroid = Vector3.zero;
            aliveCount = 0;

            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null || b.Team != team || b.State == null || b.State.IsDead)
                    continue;

                centroid += b.Position;
                aliveCount++;
            }

            if (aliveCount <= 0)
                return false;

            centroid /= aliveCount;
            return true;
        }

        private int CountAliveNear(TeamType team, Vector3 position, float radius)
        {
            float radiusSq = Mathf.Max(0.1f, radius) * Mathf.Max(0.1f, radius);
            int count = 0;

            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null || b.Team != team || b.State == null || b.State.IsDead)
                    continue;

                Vector3 offset = b.Position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSq)
                    count++;
            }

            return count;
        }

        private static TeamType ResolveObjectiveControlTeam(
            int friendlyPresence,
            int enemyPresence,
            TeamType friendlyTeam,
            TeamType enemyTeam)
        {
            if (friendlyPresence <= 0 && enemyPresence <= 0)
                return TeamType.Neutral;

            if (friendlyPresence > enemyPresence)
                return friendlyTeam;

            if (enemyPresence > friendlyPresence)
                return enemyTeam;

            return TeamType.Neutral;
        }

        public int GetRegisteredCount(TeamType team)
        {
            int count = 0;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b != null && b.Team == team)
                    count++;
            }

            return count;
        }

        public bool TryGetTeamBrawler(TeamType team, int teamIndex, out BrawlerController brawler)
        {
            brawler = null;
            if (teamIndex < 0)
                return false;

            int seen = 0;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController candidate = _brawlers[i];
                if (candidate == null || candidate.Team != team)
                    continue;

                if (seen == teamIndex)
                {
                    brawler = candidate;
                    return true;
                }

                seen++;
            }

            return false;
        }

        private void HandleDeath(BrawlerController dying)
        {
            if (!MatchStateUtility.IsCombatResolutionOpen())
                return;

            if (_roundEnding) return;
            // Count alive per team after this death.
            int blueAlive = 0, redAlive = 0;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null || b.State == null || b.State.IsDead) continue;
                if (b.Team == TeamType.Blue) blueAlive++;
                else if (b.Team == TeamType.Red) redAlive++;
            }

            if (blueAlive == 0 && redAlive == 0)
            {
                // Mutual wipe — neither team wins this round, just advance.
                EndRound(TeamType.Neutral);
            }
            else if (blueAlive == 0) EndRound(TeamType.Red);
            else if (redAlive == 0) EndRound(TeamType.Blue);
        }

        private void EndRound(TeamType winner)
        {
            _roundEnding = true;
            if (_roundWinners.Count < MaxRounds)
                _roundWinners.Add(winner);

            if (winner == TeamType.Blue) BlueRoundsWon++;
            else if (winner == TeamType.Red) RedRoundsWon++;

            if (BlueRoundsWon >= _roundsToWin || RedRoundsWon >= _roundsToWin)
            {
                TeamType matchWinner = BlueRoundsWon > RedRoundsWon ? TeamType.Blue : TeamType.Red;
                MatchManager.Instance?.AddScore(matchWinner, 10);
                return; // do not start a new round
            }

            Invoke(nameof(StartNextRound), _interRoundDelaySeconds);
        }

        private void StartNextRound()
        {
            DeployableMatchCleanup.DespawnAllActiveDeployables();
            CurrentRound++;
            _roundEnding = false;
            if (SpawnManager.Instance == null) return;

            int blueOrdinal = 0;
            int redOrdinal = 0;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController b = _brawlers[i];
                if (b == null) continue;

                int teamOrdinal = 0;
                if (b.Team == TeamType.Blue)
                    teamOrdinal = blueOrdinal++;
                else if (b.Team == TeamType.Red)
                    teamOrdinal = redOrdinal++;

                SpawnManager.Instance.ForceRespawn(b, b.Team, teamOrdinal);
            }
        }
    }
}
