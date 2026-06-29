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

        [Header("Map Placement")]
        [SerializeField] private bool _autoPlaceGoalsFromMap = true;
        [SerializeField] private bool _autoPlaceBallAtMapCenter = true;
        [SerializeField, Min(0f)] private float _goalEdgeInset = 1.1f;
        [SerializeField, Min(0f)] private float _goalGroundOffset = 0.04f;

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

        private const float GoalPlacementClearance = 0.12f;
        private const float GoalPlacementProbeHeight = 1.4f;
        private const float GoalPlacementFallbackBoundsPaddingX = 6f;
        private const float GoalPlacementFallbackBoundsPaddingZ = 8f;

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

        private void Start()
        {
            PlaceModeObjectsFromMap();
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
                PlaceGoalsOnMapEdges(bounds);
        }

        private void PlaceGoalsOnMapEdges(Bounds bounds)
        {
            Vector3 arenaCenter = bounds.center;
            float groundOffset = Mathf.Max(0f, _goalGroundOffset);
            int obstacleMask = ResolveObstacleMask();
            float scanStep = ResolveGoalPlacementScanStep();

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

                Vector3 position = ResolveGoalEdgePosition(
                    goal,
                    bounds,
                    groundOffset,
                    obstacleMask,
                    scanStep);

                goal.transform.position = position;
                goal.AlignMouthToward(arenaCenter);
            }
        }

        private Vector3 ResolveGoalEdgePosition(
            BrawlBallGoalController goal,
            Bounds bounds,
            float groundOffset,
            int obstacleMask,
            float scanStep)
        {
            Vector3 zoneSize = goal.ZoneSize;
            Vector3 arenaCenter = bounds.center;
            bool blueScoresHere = goal.ScoringTeam == TeamType.Blue;
            float halfDepth = zoneSize.z * 0.5f;
            float centerInset = Mathf.Max(_goalEdgeInset, halfDepth + GoalPlacementClearance);
            float startZ = blueScoresHere
                ? bounds.max.z - centerInset
                : bounds.min.z + centerInset;
            float directionToCenter = blueScoresHere ? -1f : 1f;
            float maxScanDistance = Mathf.Abs(startZ - arenaCenter.z);
            int steps = Mathf.Max(1, Mathf.CeilToInt(maxScanDistance / Mathf.Max(0.05f, scanStep)));

            Vector3 best = new Vector3(
                arenaCenter.x,
                Mathf.Max(goal.transform.position.y, groundOffset),
                startZ);

            for (int i = 0; i <= steps; i++)
            {
                Vector3 candidate = best;
                candidate.z = startZ + directionToCenter * scanStep * i;
                if (!ContainsBoundsXZ(bounds, candidate))
                    continue;

                Quaternion rotation = ResolveGoalRotation(candidate, arenaCenter);
                if (IsGoalFootprintClear(candidate, rotation, zoneSize, obstacleMask))
                    return candidate;
            }

            return best;
        }

        private static bool ContainsBoundsXZ(Bounds bounds, Vector3 position)
        {
            return position.x >= bounds.min.x &&
                   position.x <= bounds.max.x &&
                   position.z >= bounds.min.z &&
                   position.z <= bounds.max.z;
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
            bool hasGeneratorBounds = TryResolveGeneratorBounds(out Bounds generatorBounds);

            if (hasGroundBounds && hasGeneratorBounds &&
                TryIntersectBoundsXZ(groundBounds, generatorBounds, out bounds))
            {
                return true;
            }

            if (hasGroundBounds)
            {
                bounds = groundBounds;
                return true;
            }

            if (TryResolveSpawnPointBounds(out bounds))
                return true;

            if (hasGeneratorBounds)
            {
                bounds = generatorBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private static bool TryIntersectBoundsXZ(Bounds first, Bounds second, out Bounds intersection)
        {
            float minX = Mathf.Max(first.min.x, second.min.x);
            float maxX = Mathf.Min(first.max.x, second.max.x);
            float minZ = Mathf.Max(first.min.z, second.min.z);
            float maxZ = Mathf.Min(first.max.z, second.max.z);

            if (maxX <= minX || maxZ <= minZ)
            {
                intersection = default;
                return false;
            }

            Vector3 center = new Vector3(
                (minX + maxX) * 0.5f,
                first.center.y,
                (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(
                maxX - minX,
                0f,
                maxZ - minZ);
            intersection = new Bounds(center, size);
            return true;
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
