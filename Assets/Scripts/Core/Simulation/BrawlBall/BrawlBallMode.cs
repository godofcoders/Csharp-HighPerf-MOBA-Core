using System.Collections;
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
        [SerializeField] private bool _resetBrawlersAfterNonFinalGoal = true;
        [SerializeField, Min(0f)] private float _goalCelebrationSeconds = 1.1f;

        [Header("Map Placement")]
        [SerializeField] private bool _autoPlaceGoalsFromMap = true;
        [SerializeField] private bool _autoPlaceBallAtMapCenter = true;
        [SerializeField, Min(0f)] private float _goalEdgeInset = 0.15f;
        [SerializeField, Min(0f)] private float _goalBehindSpawnOffset = 0.15f;
        [SerializeField, Min(0f)] private float _goalGroundOffset = 0.04f;
        [SerializeField] private bool _spawnBreakableGoalBlockers = true;
        [SerializeField, Min(1)] private int _goalBlockerTileCount = 4;
        [SerializeField, Range(2f, 4f)] private float _goalBlockerForwardTileOffset = 3f;
        [SerializeField, Min(0.1f)] private float _goalBlockerTileHealth = 900f;
        [SerializeField, Min(0.1f)] private float _goalBlockerHeight = 1.15f;

        [Header("Ball State")]
        [SerializeField] private Transform _ballTransform;
        [SerializeField] private BrawlerController _ballCarrier;
        [SerializeField, Min(0f)] private float _goalScoreLockoutSeconds = 0.35f;

        private readonly List<BrawlBallGoalController> _goals = new List<BrawlBallGoalController>(2);
        private readonly List<BreakableObjectController> _goalBlockers =
            new List<BreakableObjectController>(8);
        private BrawlBallController _ball;
        private BreakableObjectDefinition _goalBlockerDefinition;
        private bool _goalBlockerNavigationDirty;
        private uint _goalScoreUnlockTick;
        private float _regulationElapsedSeconds;
        private float _overtimeElapsedSeconds;
        private bool _activeClockStarted;
        private bool _isOvertime;
        private bool _matchResolved;
        private TeamType _resolvedWinner = TeamType.Neutral;
        private Coroutine _roundResetRoutine;

        private const float GoalPlacementClearance = 0.12f;
        private const float GoalPlacementProbeHeight = 1.4f;
        private const float GoalPlacementFallbackBoundsPaddingX = 6f;
        private const float GoalPlacementFallbackBoundsPaddingZ = 8f;
        private const int MaxGoalSweepSamples = 12;

        private struct TeamSpawnLane
        {
            public int Count;
            public float SumX;
            public float SumZ;
            public float MinZ;
            public float MaxZ;

            public float AverageX => Count > 0 ? SumX / Count : 0f;
            public float AverageZ => Count > 0 ? SumZ / Count : 0f;

            public void Add(Vector3 position)
            {
                if (Count == 0)
                {
                    MinZ = position.z;
                    MaxZ = position.z;
                }
                else
                {
                    MinZ = Mathf.Min(MinZ, position.z);
                    MaxZ = Mathf.Max(MaxZ, position.z);
                }

                SumX += position.x;
                SumZ += position.z;
                Count++;
            }
        }

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
            if (_roundResetRoutine != null)
            {
                StopCoroutine(_roundResetRoutine);
                _roundResetRoutine = null;
            }

            ClearGoalBlockers();
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
            ClearGoalBlockers();

            if (_goalBlockerDefinition != null)
            {
                Destroy(_goalBlockerDefinition);
                _goalBlockerDefinition = null;
            }
        }

        private void Start()
        {
            PlaceModeObjectsFromMap();
        }

        private void Update()
        {
            RefreshGoalBlockerNavigationIfNeeded();

            MatchManager matchManager = MatchManager.Instance;
            if (matchManager == null)
                return;

            if (matchManager.CurrentState == MatchState.Active)
            {
                AdvanceMatchClock(Time.deltaTime);
                return;
            }

            if (matchManager.CurrentState == MatchState.Waiting)
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
            return TryScoreGoalBetween(ballPosition, ballPosition, currentTick, out scoringTeam);
        }

        public bool TryScoreGoalBetween(
            Vector3 startPosition,
            Vector3 endPosition,
            uint currentTick,
            out TeamType scoringTeam)
        {
            scoringTeam = TeamType.Neutral;

            if (!CanResolveGoal(currentTick))
                return false;

            float ballRadius = _ball != null ? _ball.CollisionRadius : 0f;
            float distance = Vector3.Distance(startPosition, endPosition);
            float sampleStep = Mathf.Max(0.05f, ballRadius * 0.5f);
            int sampleCount = distance <= 0.001f
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(distance / sampleStep), 1, MaxGoalSweepSamples);

            for (int sample = 0; sample <= sampleCount; sample++)
            {
                float t = sampleCount == 0 ? 1f : sample / (float)sampleCount;
                Vector3 sampledPosition = Vector3.Lerp(startPosition, endPosition, t);
                if (!TryResolveScoringGoalAt(sampledPosition, ballRadius, out scoringTeam))
                    continue;

                _goalScoreUnlockTick = currentTick +
                    SimulationClock.SecondsToTicks(_goalScoreLockoutSeconds);
                ConsumeBallAfterGoal();
                RecordGoal(scoringTeam);
                return true;
            }

            return false;
        }

        private bool TryResolveScoringGoalAt(
            Vector3 ballPosition,
            float ballRadius,
            out TeamType scoringTeam)
        {
            scoringTeam = TeamType.Neutral;
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
                return true;
            }

            return false;
        }

        private void ConsumeBallAfterGoal()
        {
            SetBallCarrierState(null);

            if (_ball != null)
                _ball.ConsumeForGoal();
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

            bool endsMatch = _isOvertime || GetTeamGoals(scoringTeam) >= _goalsToWin;

            BrawlBallEventBus.RaiseGoalScored(scoringTeam, BlueGoals, RedGoals);

            if (endsMatch)
            {
                DeployableMatchCleanup.DespawnAllActiveDeployables();
                ResolveMatch(scoringTeam);
                return;
            }

            StartRoundResetAfterGoal();
        }

        public int GetTeamGoals(TeamType team)
        {
            if (team == TeamType.Blue)
                return BlueGoals;

            if (team == TeamType.Red)
                return RedGoals;

            return 0;
        }

        public bool TryGetScoringGoalPosition(TeamType scoringTeam, out Vector3 position)
        {
            if (TryGetScoringGoal(scoringTeam, out BrawlBallGoalController goal))
            {
                position = goal.CenterPosition;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryGetScoringGoalMouthPosition(TeamType scoringTeam, out Vector3 position)
        {
            if (TryGetScoringGoal(scoringTeam, out BrawlBallGoalController goal))
            {
                position = goal.MouthPosition;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryGetScoringGoalApproachPosition(
            TeamType scoringTeam,
            float distanceFromMouth,
            out Vector3 position)
        {
            if (TryGetScoringGoal(scoringTeam, out BrawlBallGoalController goal))
            {
                position = goal.GetApproachPosition(distanceFromMouth);
                return true;
            }

            position = default;
            return false;
        }

        private bool TryGetScoringGoal(TeamType scoringTeam, out BrawlBallGoalController scoringGoal)
        {
            scoringGoal = null;

            if (!IsScoringTeam(scoringTeam))
                return false;

            for (int i = _goals.Count - 1; i >= 0; i--)
            {
                BrawlBallGoalController goal = _goals[i];
                if (goal == null)
                {
                    _goals.RemoveAt(i);
                    continue;
                }

                if (goal.ScoringTeam != scoringTeam)
                    continue;

                scoringGoal = goal;
                return true;
            }

            return false;
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
                PhaseRemainingSeconds);
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

            Vector3 objectivePosition = BallPosition;
            string objectiveName = "LooseBall";
            float radius = 2.35f;
            float weight = 132f;

            if (enemyHasBall)
            {
                objectivePosition = carrier.Position;
                objectiveName = "EnemyBallCarrier";
                radius = 3.1f;
                weight = 160f;
            }
            else if (ownHasBall)
            {
                objectivePosition = carrier.Position;
                objectiveName = "OwnBallCarrier";
                radius = 3.35f;
                weight = 118f;
            }
            else
            {
                weight = 150f;
            }

            objective = new AIObjectiveCandidate(
                AIObjectiveType.Ball,
                objectivePosition,
                weight,
                radius,
                objectiveName,
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

        private void ResetBrawlersForNextRound()
        {
            if (!_resetBrawlersAfterNonFinalGoal || SpawnManager.Instance == null)
                return;

            BrawlerController[] brawlers = FindObjectsOfType<BrawlerController>(true);
            if (brawlers == null || brawlers.Length == 0)
                return;

            int blueOrdinal = 0;
            int redOrdinal = 0;
            for (int i = 0; i < brawlers.Length; i++)
            {
                BrawlerController brawler = brawlers[i];
                if (brawler == null)
                    continue;

                if (brawler.Team == TeamType.Blue)
                {
                    SpawnManager.Instance.ForceRespawn(brawler, brawler.Team, blueOrdinal);
                    blueOrdinal++;
                }
                else if (brawler.Team == TeamType.Red)
                {
                    SpawnManager.Instance.ForceRespawn(brawler, brawler.Team, redOrdinal);
                    redOrdinal++;
                }
            }
        }

        private void StartRoundResetAfterGoal()
        {
            if (_roundResetRoutine != null)
            {
                StopCoroutine(_roundResetRoutine);
                _roundResetRoutine = null;
            }

            MatchManager matchManager = MatchManager.Instance;
            if (matchManager != null)
                matchManager.StartRoundResetCountdown(_goalCelebrationSeconds);

            _roundResetRoutine = StartCoroutine(RoundResetAfterGoalRoutine());
        }

        private IEnumerator RoundResetAfterGoalRoutine()
        {
            float delaySeconds = Mathf.Max(0f, _goalCelebrationSeconds);
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            ResetBall();
            DeployableMatchCleanup.DespawnAllActiveDeployables();
            ResetBrawlersForNextRound();
            _roundResetRoutine = null;
        }

        private void PlaceModeObjectsFromMap()
        {
            if (!_autoPlaceGoalsFromMap && !_autoPlaceBallAtMapCenter)
                return;

            if (!TryResolvePlayableMapBounds(out Bounds bounds))
                return;

            Vector3 arenaCenter = bounds.center;

            if (_autoPlaceBallAtMapCenter && _ball != null)
            {
                Vector3 ballSpawn = new Vector3(
                    arenaCenter.x,
                    _ball.CurrentPosition.y,
                    arenaCenter.z);
                _ball.OverrideSpawnPosition(ballSpawn, BallCarrier == null);
            }

            if (_autoPlaceGoalsFromMap)
            {
                PlaceGoalsOnMapEdges(bounds);
                EnsureGoalBlockers(bounds);
            }
        }

        private void PlaceGoalsOnMapEdges(Bounds bounds)
        {
            Vector3 arenaCenter = bounds.center;
            float groundOffset = Mathf.Max(0f, _goalGroundOffset);
            int obstacleMask = ResolveObstacleMask();
            float scanStep = ResolveGoalPlacementScanStep();
            bool hasSpawnLanes = TryResolveNormalizedTeamSpawnLanes(
                out TeamSpawnLane blueSpawnLane,
                out TeamSpawnLane redSpawnLane);

            for (int i = _goals.Count - 1; i >= 0; i--)
            {
                BrawlBallGoalController goal = _goals[i];
                if (goal == null)
                {
                    _goals.RemoveAt(i);
                    continue;
                }

                if (!IsScoringTeam(goal.ScoringTeam))
                    continue;

                bool hasDefendingSpawnLane = TryResolveDefendingSpawnLane(
                    goal.ScoringTeam,
                    hasSpawnLanes,
                    blueSpawnLane,
                    redSpawnLane,
                    out TeamSpawnLane defendingSpawnLane);

                Vector3 position = ResolveGoalEdgePosition(
                    goal,
                    bounds,
                    groundOffset,
                    obstacleMask,
                    scanStep,
                    hasDefendingSpawnLane,
                    defendingSpawnLane);

                goal.transform.position = position;
                goal.AlignMouthToward(arenaCenter);
            }
        }

        private void EnsureGoalBlockers(Bounds bounds)
        {
            if (!_spawnBreakableGoalBlockers)
            {
                ClearGoalBlockers();
                return;
            }

            ClearGoalBlockers();

            BreakableObjectDefinition definition = ResolveGoalBlockerDefinition();
            if (definition == null)
                return;

            float tileSize = ResolveGoalBlockerTileSize();
            float blockerHeight = Mathf.Max(0.1f, _goalBlockerHeight);
            int tileCount = Mathf.Max(1, _goalBlockerTileCount);

            for (int i = 0; i < _goals.Count; i++)
            {
                BrawlBallGoalController goal = _goals[i];
                if (goal == null || !IsScoringTeam(goal.ScoringTeam))
                    continue;

                Vector3 forward = goal.FieldDirection;
                Vector3 lateral = goal.transform.right;
                forward.y = 0f;
                lateral.y = 0f;
                if (forward.sqrMagnitude <= 0.001f || lateral.sqrMagnitude <= 0.001f)
                    continue;

                forward.Normalize();
                lateral.Normalize();

                float forwardOffsetTiles = Mathf.Clamp(_goalBlockerForwardTileOffset, 2f, 4f);
                Vector3 rowCenter = goal.MouthPosition + forward * (tileSize * forwardOffsetTiles);
                rowCenter.y = Mathf.Max(goal.transform.position.y, _goalGroundOffset) + blockerHeight * 0.5f;

                float halfIndex = (tileCount - 1) * 0.5f;
                for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
                {
                    Vector3 position = rowCenter + lateral * ((tileIndex - halfIndex) * tileSize);
                    position.x = Mathf.Clamp(position.x, bounds.min.x + tileSize * 0.5f, bounds.max.x - tileSize * 0.5f);
                    BreakableObjectController blocker = CreateGoalBlockerTile(
                        definition,
                        position,
                        Quaternion.LookRotation(forward, Vector3.up),
                        tileSize,
                        blockerHeight);

                    if (blocker == null)
                        continue;

                    _goalBlockers.Add(blocker);
                    MarkGoalBlockerNavigation(blocker, false);
                }
            }

            _goalBlockerNavigationDirty = _goalBlockers.Count > 0;
        }

        private BreakableObjectController CreateGoalBlockerTile(
            BreakableObjectDefinition definition,
            Vector3 position,
            Quaternion rotation,
            float tileSize,
            float height)
        {
            GameObject blockerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blockerObject.name = "BrawlBallGoalBreakableTile";
            blockerObject.transform.position = position;
            blockerObject.transform.rotation = rotation;
            blockerObject.transform.localScale = new Vector3(
                tileSize * 0.92f,
                height,
                tileSize * 0.92f);

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer >= 0)
                blockerObject.layer = obstacleLayer;

            BreakableObjectController blocker =
                blockerObject.AddComponent<BreakableObjectController>();
            blocker.Initialize(definition);
            return blocker;
        }

        private BreakableObjectDefinition ResolveGoalBlockerDefinition()
        {
            if (_goalBlockerDefinition != null)
                return _goalBlockerDefinition;

            BreakableObjectDefinition definition =
                ScriptableObject.CreateInstance<BreakableObjectDefinition>();
            definition.name = "RuntimeBrawlBallGoalBlocker";
            definition.hideFlags = HideFlags.DontSave;
            definition.MaxHealth = Mathf.Max(1f, _goalBlockerTileHealth);
            definition.CollisionRadius = Mathf.Max(0.25f, ResolveGoalBlockerTileSize() * 0.52f);
            definition.BlocksNavigation = true;
            definition.NavigationClearRadius = Mathf.Max(0.45f, ResolveGoalBlockerTileSize() * 0.72f);
            definition.CanBeDamagedByProjectiles = true;
            definition.CanBeDamagedByAreaEffects = true;
            definition.RequiresSuperDamage = true;
            definition.RequiredSourceAbility = null;
            definition.DestroyGameObjectOnDeath = true;
            definition.BaseTint = new Color(0.66f, 0.48f, 0.28f, 1f);
            definition.HitFlashColor = new Color(1f, 0.84f, 0.45f, 1f);
            definition.CriticalTint = new Color(0.42f, 0.28f, 0.16f, 1f);
            definition.FallbackDebrisColor = new Color(0.62f, 0.45f, 0.26f, 1f);
            definition.FallbackDebrisPieces = 6;

            _goalBlockerDefinition = definition;
            return _goalBlockerDefinition;
        }

        private static float ResolveGoalBlockerTileSize()
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            return mapGenerator != null
                ? Mathf.Max(0.5f, mapGenerator.CellSize)
                : 1f;
        }

        private static void MarkGoalBlockerNavigation(
            BreakableObjectController blocker,
            bool walkable)
        {
            if (blocker == null || blocker.Definition == null || !blocker.Definition.BlocksNavigation)
                return;

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return;

            float radius = Mathf.Max(blocker.CollisionRadius, blocker.Definition.NavigationClearRadius);
            pathfinder.SetWalkableCircle(blocker.Position, radius, walkable);
        }

        private void RefreshGoalBlockerNavigationIfNeeded()
        {
            if (!_goalBlockerNavigationDirty || SimulationClock.Pathfinder == null)
                return;

            bool hasLiveBlocker = false;
            for (int i = _goalBlockers.Count - 1; i >= 0; i--)
            {
                BreakableObjectController blocker = _goalBlockers[i];
                if (blocker == null || blocker.IsDestroyed)
                {
                    _goalBlockers.RemoveAt(i);
                    continue;
                }

                hasLiveBlocker = true;
                MarkGoalBlockerNavigation(blocker, false);
            }

            _goalBlockerNavigationDirty = false;
            if (!hasLiveBlocker)
                _goalBlockers.Clear();
        }

        private void ClearGoalBlockers()
        {
            _goalBlockerNavigationDirty = false;

            for (int i = _goalBlockers.Count - 1; i >= 0; i--)
            {
                BreakableObjectController blocker = _goalBlockers[i];
                if (blocker == null)
                    continue;

                MarkGoalBlockerNavigation(blocker, true);
                Destroy(blocker.gameObject);
            }

            _goalBlockers.Clear();
        }

        private Vector3 ResolveGoalEdgePosition(
            BrawlBallGoalController goal,
            Bounds bounds,
            float groundOffset,
            int obstacleMask,
            float scanStep,
            bool hasDefendingSpawnLane,
            TeamSpawnLane defendingSpawnLane)
        {
            Vector3 zoneSize = goal.ZoneSize;
            Vector3 arenaCenter = bounds.center;
            float halfDepth = zoneSize.z * 0.5f;
            float outwardSign = ResolveGoalOutwardSign(goal, arenaCenter, hasDefendingSpawnLane, defendingSpawnLane);
            float edgeInset = Mathf.Min(Mathf.Max(0f, _goalEdgeInset), halfDepth);
            float edgeCenterZ = outwardSign > 0f
                ? bounds.max.z + halfDepth - edgeInset
                : bounds.min.z - halfDepth + edgeInset;
            float startZ = edgeCenterZ;
            float spawnExtremeZ = outwardSign > 0f
                ? defendingSpawnLane.MaxZ
                : defendingSpawnLane.MinZ;

            if (hasDefendingSpawnLane)
            {
                float behindSpawnZ = spawnExtremeZ +
                    outwardSign * (halfDepth + Mathf.Max(_goalBehindSpawnOffset, GoalPlacementClearance));
                startZ = outwardSign > 0f
                    ? Mathf.Max(edgeCenterZ, behindSpawnZ)
                    : Mathf.Min(edgeCenterZ, behindSpawnZ);
            }

            float directionToCenter = -outwardSign;
            float maxScanDistance = Mathf.Abs(startZ - arenaCenter.z);
            int steps = Mathf.Max(1, Mathf.CeilToInt(maxScanDistance / Mathf.Max(0.05f, scanStep)));

            Vector3 best = new Vector3(
                ResolveGoalCenterX(bounds, zoneSize, arenaCenter.x),
                Mathf.Max(goal.transform.position.y, groundOffset),
                startZ);

            for (int i = 0; i <= steps; i++)
            {
                Vector3 candidate = best;
                candidate.z = startZ + directionToCenter * scanStep * i;
                if (!ContainsGoalSearchBoundsXZ(bounds, candidate, zoneSize))
                    continue;

                if (hasDefendingSpawnLane &&
                    !IsGoalBehindSpawnLine(candidate.z, halfDepth, outwardSign, spawnExtremeZ))
                {
                    break;
                }

                Quaternion rotation = ResolveGoalRotation(candidate, arenaCenter);
                if (IsGoalFootprintClear(candidate, rotation, zoneSize, obstacleMask))
                    return candidate;
            }

            return best;
        }

        private static float ResolveGoalOutwardSign(
            BrawlBallGoalController goal,
            Vector3 arenaCenter,
            bool hasDefendingSpawnLane,
            TeamSpawnLane defendingSpawnLane)
        {
            if (hasDefendingSpawnLane)
                return defendingSpawnLane.AverageZ >= arenaCenter.z ? 1f : -1f;

            return goal.ScoringTeam == TeamType.Blue ? 1f : -1f;
        }

        private static float ResolveGoalCenterX(
            Bounds bounds,
            Vector3 zoneSize,
            float fallbackX)
        {
            // Brawl Ball goals belong to the map's central scoring lane. Spawn
            // markers can be lane-offset for team spread and should not pull the
            // goal mouth toward blocker-side pockets.
            float desiredX = fallbackX;
            float halfWidth = zoneSize.x * 0.5f;
            if (bounds.size.x <= zoneSize.x)
                return fallbackX;

            return Mathf.Clamp(desiredX, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        }

        private bool ContainsGoalSearchBoundsXZ(Bounds bounds, Vector3 position, Vector3 zoneSize)
        {
            float halfWidth = zoneSize.x * 0.5f;
            float zPadding = zoneSize.z + Mathf.Max(_goalEdgeInset, _goalBehindSpawnOffset) + GoalPlacementClearance;
            return position.x >= bounds.min.x &&
                   position.x <= bounds.max.x &&
                   position.x - halfWidth >= bounds.min.x - GoalPlacementClearance &&
                   position.x + halfWidth <= bounds.max.x + GoalPlacementClearance &&
                   position.z >= bounds.min.z - zPadding &&
                   position.z <= bounds.max.z + zPadding;
        }

        private bool IsGoalBehindSpawnLine(
            float goalCenterZ,
            float halfDepth,
            float outwardSign,
            float spawnExtremeZ)
        {
            float innerGoalEdgeZ = goalCenterZ - outwardSign * halfDepth;
            float requiredOffset = Mathf.Max(_goalBehindSpawnOffset, GoalPlacementClearance);
            return outwardSign > 0f
                ? innerGoalEdgeZ >= spawnExtremeZ + requiredOffset
                : innerGoalEdgeZ <= spawnExtremeZ - requiredOffset;
        }

        private static Quaternion ResolveGoalRotation(Vector3 position, Vector3 worldTarget)
        {
            Vector3 toTarget = worldTarget - position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(-toTarget.normalized);
        }

        private static bool IsGoalFootprintClear(
            Vector3 position,
            Quaternion rotation,
            Vector3 zoneSize,
            int obstacleMask)
        {
            if (obstacleMask == 0)
                return true;

            Vector3 halfExtents = new Vector3(
                zoneSize.x * 0.5f + GoalPlacementClearance,
                GoalPlacementProbeHeight * 0.5f,
                zoneSize.z * 0.5f + GoalPlacementClearance);
            Vector3 center = position + Vector3.up * halfExtents.y;

            return !Physics.CheckBox(
                center,
                halfExtents,
                rotation,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
        }

        private static int ResolveObstacleMask()
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
                return mapGenerator.ObstacleLayer.value;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
        }

        private static float ResolveGoalPlacementScanStep()
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            return mapGenerator != null
                ? Mathf.Max(0.15f, mapGenerator.CellSize * 0.25f)
                : 0.25f;
        }

        private static bool TryResolvePlayableMapBounds(out Bounds bounds)
        {
            bool hasGroundBounds = TryResolveSpawnedMapGroundBounds(out Bounds groundBounds);
            if (hasGroundBounds)
            {
                bounds = groundBounds;
                return true;
            }

            if (TryResolveSpawnPointBounds(out bounds))
                return true;

            bool hasGeneratorBounds = TryResolveGeneratorBounds(out Bounds generatorBounds);
            if (hasGeneratorBounds)
            {
                bounds = generatorBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private static bool TryResolveSpawnedMapGroundBounds(out Bounds bounds)
        {
            bounds = default;

            MapLoader mapLoader = FindObjectOfType<MapLoader>();
            GameObject spawnedMap = mapLoader != null ? mapLoader.SpawnedMapInstance : null;
            if (spawnedMap == null)
                return false;

            int obstacleMask = ResolveObstacleMask();
            int excludedDecorationMask = obstacleMask | ResolveLayerMask("Bushes") | ResolveLayerMask("Bush");
            Collider[] colliders = spawnedMap.GetComponentsInChildren<Collider>(false);
            bool found = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.isTrigger ||
                    (excludedDecorationMask & (1 << collider.gameObject.layer)) != 0)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (found)
                return true;

            Renderer[] renderers = spawnedMap.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    (excludedDecorationMask & (1 << renderer.gameObject.layer)) != 0)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static int ResolveLayerMask(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }

        private static bool TryResolveSpawnPointBounds(out Bounds bounds)
        {
            SpawnPointMarker[] markers = FindObjectsOfType<SpawnPointMarker>(false);
            if (markers == null || markers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bool hasBounds = false;
            bounds = default;
            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker marker = markers[i];
                if (marker == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = new Bounds(marker.transform.position, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(marker.transform.position);
                }
            }

            if (!hasBounds)
                return false;

            bounds.Expand(new Vector3(
                GoalPlacementFallbackBoundsPaddingX,
                0f,
                GoalPlacementFallbackBoundsPaddingZ));
            return true;
        }

        private static bool TryResolveNormalizedTeamSpawnLanes(
            out TeamSpawnLane blueSpawnLane,
            out TeamSpawnLane redSpawnLane)
        {
            blueSpawnLane = default;
            redSpawnLane = default;

            SpawnPointMarker[] markers = FindObjectsOfType<SpawnPointMarker>(false);
            if (markers == null || markers.Length == 0)
                return false;

            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker marker = markers[i];
                if (marker == null)
                    continue;

                if (marker.Team == TeamType.Blue)
                    blueSpawnLane.Add(marker.transform.position);
                else if (marker.Team == TeamType.Red)
                    redSpawnLane.Add(marker.transform.position);
            }

            if (blueSpawnLane.Count == 0 || redSpawnLane.Count == 0)
                return false;

            // Mirrors MapLoader.NormalizeTeamSpawnOrientation: blue plays from
            // the lower side, red from the upper side, even if a prefab's raw
            // markers were authored the other way around.
            if (blueSpawnLane.AverageZ > redSpawnLane.AverageZ)
            {
                TeamSpawnLane authoredBlue = blueSpawnLane;
                blueSpawnLane = redSpawnLane;
                redSpawnLane = authoredBlue;
            }

            return true;
        }

        private static bool TryResolveDefendingSpawnLane(
            TeamType scoringTeam,
            bool hasSpawnLanes,
            TeamSpawnLane blueSpawnLane,
            TeamSpawnLane redSpawnLane,
            out TeamSpawnLane defendingSpawnLane)
        {
            defendingSpawnLane = default;
            if (!hasSpawnLanes)
                return false;

            if (scoringTeam == TeamType.Blue)
            {
                defendingSpawnLane = redSpawnLane;
                return defendingSpawnLane.Count > 0;
            }

            if (scoringTeam == TeamType.Red)
            {
                defendingSpawnLane = blueSpawnLane;
                return defendingSpawnLane.Count > 0;
            }

            return false;
        }

        private static bool TryResolveGeneratorBounds(out Bounds bounds)
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null)
            {
                float cellSize = Mathf.Max(0.1f, mapGenerator.CellSize);
                float width = Mathf.Max(1, mapGenerator.Width) * cellSize;
                float height = Mathf.Max(1, mapGenerator.Height) * cellSize;
                bounds = new Bounds(
                    mapGenerator.transform.position,
                    new Vector3(width, 0f, height));
                return true;
            }

            bounds = default;
            return false;
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
