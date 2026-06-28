using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class BrawlBallMode : MonoBehaviour,
        IAIGameModeMacroStateProvider,
        IAIRuntimeObjectiveProvider
    {
        public static BrawlBallMode Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField, Min(1)] private int _goalsToWin = 2;
        [SerializeField, Min(1f)] private float _regulationDurationSeconds = 120f;
        [SerializeField, Min(1f)] private float _overtimeDurationSeconds = 60f;

        [Header("Ball State")]
        [SerializeField] private Transform _ballTransform;
        [SerializeField] private BrawlerController _ballCarrier;
        [SerializeField, Min(0f)] private float _goalScoreLockoutSeconds = 0.35f;

        private readonly List<BrawlBallGoalController> _goals = new List<BrawlBallGoalController>(2);
        private BrawlBallController _ball;
        private uint _goalScoreUnlockTick;
        private float _regulationElapsedSeconds;
        private float _overtimeElapsedSeconds;
        private bool _activeClockStarted;
        private bool _isOvertime;
        private bool _matchResolved;
        private TeamType _resolvedWinner = TeamType.Neutral;

        public int BlueGoals { get; private set; }
        public int RedGoals { get; private set; }
        public int GoalsToWin => _goalsToWin;
        public float RegulationDurationSeconds => _regulationDurationSeconds;
        public float OvertimeDurationSeconds => _overtimeDurationSeconds;
        public bool IsOvertime => _isOvertime;
        public bool IsMatchResolved => _matchResolved;
        public bool IsDrawResolved => _matchResolved && _resolvedWinner == TeamType.Neutral;
        public TeamType ResolvedWinner => _resolvedWinner;
        public float RegulationRemainingSeconds =>
            Mathf.Max(0f, _regulationDurationSeconds - _regulationElapsedSeconds);
        public float OvertimeRemainingSeconds =>
            _isOvertime ? Mathf.Max(0f, _overtimeDurationSeconds - _overtimeElapsedSeconds) : 0f;
        public float PhaseRemainingSeconds =>
            _isOvertime ? OvertimeRemainingSeconds : RegulationRemainingSeconds;
        public int RegisteredGoalCount => _goals.Count;
        public BrawlerController BallCarrier => IsValidCarrier(_ballCarrier) ? _ballCarrier : null;
        public BrawlBallController Ball => _ball;
        public Vector3 BallPosition => BallCarrier != null
            ? BallCarrier.Position
            : (_ball != null
                ? _ball.CurrentPosition
                : (_ballTransform != null ? _ballTransform.position : Vector3.zero));

        public GameModeId ModeId => GameModeId.BrawlBall;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

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
            if (Instance == this)
                Instance = null;

            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void Update()
        {
            MatchManager matchManager = MatchManager.Instance;
            if (matchManager == null)
                return;

            if (matchManager.CurrentState == MatchState.Active)
            {
                AdvanceMatchClock(Time.deltaTime);
                return;
            }

            if (matchManager.CurrentState != MatchState.Ended)
                ResetMatchClock();
        }

        public void SetBallCarrier(BrawlerController carrier)
        {
            if (_ball != null)
                _ball.AssignCarrierFromMode(carrier);

            SetBallCarrierState(carrier);
        }

        public void ClearBallCarrier()
        {
            if (_ball != null)
                _ball.ClearCarrierFromMode();

            SetBallCarrierState(null);
        }

        public void RegisterBall(BrawlBallController ball)
        {
            if (ball == null)
                return;

            _ball = ball;
            _ballTransform = ball.transform;

            if (BallCarrier != null)
                _ball.AssignCarrierFromMode(BallCarrier);
        }

        public void UnregisterBall(BrawlBallController ball)
        {
            if (_ball != ball)
                return;

            _ball = null;
            if (_ballTransform == ball.transform)
                _ballTransform = null;
        }

        public void RegisterGoal(BrawlBallGoalController goal)
        {
            if (goal == null || _goals.Contains(goal))
                return;

            _goals.Add(goal);
        }

        public void UnregisterGoal(BrawlBallGoalController goal)
        {
            _goals.Remove(goal);
        }

        public void ResetBall()
        {
            if (_ball != null)
                _ball.ResetToSpawn();
            else
                ClearBallCarrier();
        }

        public bool TryScoreGoalAt(Vector3 ballPosition, uint currentTick, out TeamType scoringTeam)
        {
            scoringTeam = TeamType.Neutral;

            if (!CanResolveGoal(currentTick))
                return false;

            float ballRadius = _ball != null ? _ball.CollisionRadius : 0f;

            for (int i = _goals.Count - 1; i >= 0; i--)
            {
                BrawlBallGoalController goal = _goals[i];
                if (goal == null)
                {
                    _goals.RemoveAt(i);
                    continue;
                }

                if (!IsScoringTeam(goal.ScoringTeam) ||
                    !goal.ContainsBall(ballPosition, ballRadius))
                {
                    continue;
                }

                scoringTeam = goal.ScoringTeam;
                _goalScoreUnlockTick = currentTick +
                    SimulationClock.SecondsToTicks(_goalScoreLockoutSeconds);
                RecordGoal(scoringTeam);
                return true;
            }

            return false;
        }

        public bool CanKickBall(BrawlerController carrier)
        {
            return _ball != null &&
                   BallCarrier == carrier &&
                   _ball.Carrier == carrier;
        }

        public bool TryKickBall(
            BrawlerController carrier,
            Vector3 direction,
            bool isSuperKick,
            uint currentTick)
        {
            return CanKickBall(carrier) &&
                   _ball.KickFromCarrier(carrier, direction, isSuperKick, currentTick);
        }

        public void AdvanceClockForDebug(float deltaSeconds)
        {
            AdvanceMatchClock(deltaSeconds);
        }

        public void SetScoreForDebug(int blueGoals, int redGoals)
        {
            BlueGoals = Mathf.Max(0, blueGoals);
            RedGoals = Mathf.Max(0, redGoals);
        }

        internal void NotifyBallPickedUp(BrawlerController carrier, Vector3 position)
        {
            SetBallCarrierState(carrier);
            BrawlBallEventBus.RaiseBallPickedUp(carrier, position);
        }

        internal void NotifyBallKicked(
            BrawlerController kicker,
            Vector3 position,
            Vector3 direction,
            bool isSuperKick)
        {
            SetBallCarrierState(null);
            BrawlBallEventBus.RaiseBallKicked(kicker, position, direction, isSuperKick);
        }

        internal void NotifyBallDropped(Vector3 position)
        {
            SetBallCarrierState(null);
            BrawlBallEventBus.RaiseBallDropped(position);
        }

        internal void NotifyBallReset(Vector3 position)
        {
            SetBallCarrierState(null);
            BrawlBallEventBus.RaiseBallReset(position);
        }

        public void RecordGoal(TeamType scoringTeam)
        {
            if (_matchResolved)
                return;

            if (scoringTeam == TeamType.Blue)
                BlueGoals++;
            else if (scoringTeam == TeamType.Red)
                RedGoals++;
            else
                return;

            ResetBall();
            BrawlBallEventBus.RaiseGoalScored(scoringTeam, BlueGoals, RedGoals);

            if (_isOvertime || GetTeamGoals(scoringTeam) >= _goalsToWin)
                ResolveMatch(scoringTeam);
        }

        public int GetTeamGoals(TeamType team)
        {
            if (team == TeamType.Blue)
                return BlueGoals;

            if (team == TeamType.Red)
                return RedGoals;

            return 0;
        }

        public bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state)
        {
            state = AIGameModeMacroState.Neutral;
            if (team == TeamType.Neutral)
                return false;

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            BrawlerController carrier = BallCarrier;
            bool ownHasBall = carrier != null && carrier.Team == team;
            bool enemyHasBall = carrier != null && carrier.Team == enemyTeam;

            state = AIGameModeMacroStrategy.ResolveBrawlBall(
                GetTeamGoals(team),
                GetTeamGoals(enemyTeam),
                _goalsToWin,
                ownHasBall,
                enemyHasBall,
                matchTimeRemainingSeconds: 0f);
            return true;
        }

        public bool TryGetRuntimeObjective(
            TeamType team,
            AIObjectiveType preferredType,
            Vector3 selfPosition,
            out AIObjectiveCandidate objective)
        {
            objective = default;

            if (team == TeamType.Neutral ||
                (BallCarrier == null && _ball == null && _ballTransform == null))
            {
                return false;
            }

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            BrawlerController carrier = BallCarrier;
            bool ownHasBall = carrier != null && carrier.Team == team;
            bool enemyHasBall = carrier != null && carrier.Team == enemyTeam;

            int friendlyPresence = ownHasBall ? 1 : 0;
            int enemyPresence = enemyHasBall ? 1 : 0;
            AIObjectiveControlState controlState =
                AIObjectiveControlUtility.ResolveForTeam(
                    ownHasBall
                        ? team
                        : (enemyHasBall ? enemyTeam : TeamType.Neutral),
                    team,
                    friendlyPresence,
                    enemyPresence);

            float weight = 84f;
            if (enemyHasBall)
                weight += 18f;
            else if (!ownHasBall)
                weight += 10f;

            objective = new AIObjectiveCandidate(
                AIObjectiveType.Ball,
                BallPosition,
                weight,
                2.75f,
                "RuntimeBall",
                true,
                controlState,
                friendlyPresence,
                enemyPresence);
            return true;
        }

        private void SetBallCarrierState(BrawlerController carrier)
        {
            if (!IsValidCarrier(carrier))
                carrier = null;

            if (_ballCarrier == carrier)
                return;

            _ballCarrier = carrier;
            BrawlBallEventBus.RaiseCarrierChanged(_ballCarrier);
        }

        private bool CanResolveGoal(uint currentTick)
        {
            if (_matchResolved)
                return false;

            if (currentTick < _goalScoreUnlockTick)
                return false;

            MatchManager matchManager = MatchManager.Instance;
            return matchManager == null ||
                   matchManager.CurrentState == MatchState.Active;
        }

        private void AdvanceMatchClock(float deltaSeconds)
        {
            if (_matchResolved)
                return;

            if (!_activeClockStarted)
            {
                _activeClockStarted = true;
                _regulationElapsedSeconds = 0f;
                _overtimeElapsedSeconds = 0f;
                _isOvertime = false;
            }

            float safeDelta = Mathf.Max(0f, deltaSeconds);
            if (_isOvertime)
            {
                _overtimeElapsedSeconds += safeDelta;
                if (_overtimeElapsedSeconds >= _overtimeDurationSeconds)
                    ResolveMatch(TeamType.Neutral);
                return;
            }

            _regulationElapsedSeconds += safeDelta;
            if (_regulationElapsedSeconds < _regulationDurationSeconds)
                return;

            if (BlueGoals == RedGoals)
            {
                StartOvertime();
                return;
            }

            ResolveMatch(BlueGoals > RedGoals ? TeamType.Blue : TeamType.Red);
        }

        private void StartOvertime()
        {
            if (_isOvertime || _matchResolved)
                return;

            _isOvertime = true;
            _overtimeElapsedSeconds = 0f;
        }

        private void ResolveMatch(TeamType winner)
        {
            if (_matchResolved)
                return;

            _matchResolved = true;
            _resolvedWinner = winner;

            MatchManager matchManager = MatchManager.Instance;
            if (matchManager == null)
                return;

            if (winner == TeamType.Blue || winner == TeamType.Red)
                matchManager.AddScore(winner, 10);
            else
                matchManager.EndMatch(TeamType.Neutral);
        }

        private void ResetMatchClock()
        {
            _regulationElapsedSeconds = 0f;
            _overtimeElapsedSeconds = 0f;
            _activeClockStarted = false;
            _isOvertime = false;
            _matchResolved = false;
            _resolvedWinner = TeamType.Neutral;
        }

        private static bool IsValidCarrier(BrawlerController carrier)
        {
            return SpatialEntityUtility.IsAlive(carrier) &&
                   carrier.State != null &&
                   !carrier.State.IsDead &&
                   carrier.gameObject.activeInHierarchy;
        }

        private static bool IsScoringTeam(TeamType team)
        {
            return team == TeamType.Blue || team == TeamType.Red;
        }
    }
}
