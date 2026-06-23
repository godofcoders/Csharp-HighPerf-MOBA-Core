using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class NavigationAgent
    {
        private const int BoundaryClearanceCells = 1;
        private const int ObstacleClearanceCells = 1;
        private const uint AvoidanceDirectionLockTicks = 6;

        private readonly BrawlerController _brawler;
        private readonly ISimulationClock _clock;
        private readonly AICommandSource _commandSource;
        private readonly BrawlerAIProfile _profile;

        private List<PathNode> _path;
        private int _pathIndex;

        private Vector3 _destination;
        private bool _hasDestination;
        private float _arrivalDistance = 0.6f;
        private uint _destinationRequestTick;
        private Vector3 _destinationStartPosition;
        private float _destinationStartDistanceSq;
        private float _bestDistanceToDestinationSq;
        private bool _routeBlocked;
        private bool _currentDestinationHighPriority;

        private uint _nextRepathTick;
        private readonly uint _repathCooldownTicks = 12;
        private readonly float _repathDistanceThreshold = 1.0f;

        private Vector3 _lastSamplePosition;
        private uint _lastSampleTick;
        private int _consecutiveStuckSamples;
        private int _consecutiveRouteFailures;
        private Vector3 _lastQueuedMoveDirection;
        private uint _lastQueuedMoveTick;
        private int _consecutiveActiveZeroMoveTicks;
        private int _consecutivePathBudgetDeferrals;
        private Vector3 _lastAvoidanceDirection;
        private uint _avoidanceDirectionLockUntilTick;
        private bool _hasAvoidanceDirection;

        public Vector3 Position => _brawler.Position;
        public bool HasDestination => _hasDestination;
        public Vector3 Destination => _destination;
        public bool IsRouteBlocked => _routeBlocked;
        public int ConsecutiveStuckSamples => _consecutiveStuckSamples;
        public int ConsecutiveRouteFailures => _consecutiveRouteFailures;
        public Vector3 LastQueuedMoveDirection => _lastQueuedMoveDirection;
        public int ConsecutiveActiveZeroMoveTicks => _consecutiveActiveZeroMoveTicks;
        public int ConsecutivePathBudgetDeferrals => _consecutivePathBudgetDeferrals;
        public bool IsActiveDestinationMovementSuppressed =>
            _hasDestination && _consecutiveActiveZeroMoveTicks > 0;

        public NavigationAgent(
            BrawlerController brawler,
            AICommandSource commandSource,
            BrawlerAIProfile profile = null)
        {
            _brawler = brawler;
            _commandSource = commandSource;
            _profile = profile;
            _clock = ServiceProvider.Get<ISimulationClock>();
            _lastSamplePosition = brawler.Position;
            _lastSampleTick = _clock.CurrentTick;
        }

        public void RequestDestination(
            Vector3 target,
            float arrivalDistance = 0.6f,
            bool highPriority = false)
        {
            target = ClampTargetToPathfinderBounds(FlattenToMovementPlane(target));
            _arrivalDistance = arrivalDistance;
            _currentDestinationHighPriority = highPriority;

            if (!_hasDestination)
            {
                ForceRepath(target, highPriority);
                return;
            }

            if (_routeBlocked && _clock.CurrentTick >= _nextRepathTick)
            {
                ForceRepath(target, highPriority);
                return;
            }

            float movedTargetSq = (target - _destination).sqrMagnitude;
            if (movedTargetSq >= (_repathDistanceThreshold * _repathDistanceThreshold) &&
                _clock.CurrentTick >= _nextRepathTick)
            {
                ForceRepath(target, highPriority);
                return;
            }

            _destination = target;
        }

        public void ForceRepath(Vector3 target, bool highPriority = false)
        {
            target = ClampTargetToPathfinderBounds(FlattenToMovementPlane(target));
            _currentDestinationHighPriority = highPriority;
            bool destinationChanged = !_hasDestination ||
                                      (target - _destination).sqrMagnitude >=
                                      (_repathDistanceThreshold * _repathDistanceThreshold);

            if (SimulationClock.Pathfinder == null)
            {
                _destination = target;
                if (destinationChanged)
                    ResetDestinationProgress(target);

                _hasDestination = true;
                _path = null;
                _pathIndex = 0;
                _routeBlocked = false;
                _consecutiveRouteFailures = 0;
                _consecutivePathBudgetDeferrals = 0;
                _nextRepathTick = _clock.CurrentTick + _repathCooldownTicks;
                return;
            }

            var start = SimulationClock.Pathfinder.GetGridCoords(_brawler.Position);
            var end = SimulationClock.Pathfinder.GetGridCoords(target);
            var requestedEnd = end;
            int endpointRepairRadius = GetEndpointRepairRadius(SimulationClock.Pathfinder);
            bool foundStart = true;
            bool foundEnd = true;

            if (!SimulationClock.Pathfinder.IsWalkableWithNavigationClearance(start))
            {
                foundStart =
                    SimulationClock.Pathfinder.TryGetNearestWalkableCoordsWithNavigationClearance(
                        start,
                        endpointRepairRadius,
                        out start) ||
                    SimulationClock.Pathfinder.TryGetNearestWalkableCoords(
                        start,
                        endpointRepairRadius,
                        out start);
            }

            if (!SimulationClock.Pathfinder.IsWalkableWithNavigationClearance(end))
            {
                foundEnd =
                    SimulationClock.Pathfinder.TryGetNearestWalkableCoordsWithNavigationClearance(
                        end,
                        endpointRepairRadius,
                        out end) ||
                    SimulationClock.Pathfinder.TryGetNearestWalkableCoordsWithBoundaryClearance(
                        end,
                        endpointRepairRadius,
                        out end);
            }

            if (!foundStart || !foundEnd)
            {
                _destination = target;
                if (destinationChanged)
                    ResetDestinationProgress(target);

                _hasDestination = true;
                _path = null;
                _pathIndex = 0;
                _routeBlocked = true;
                _consecutiveRouteFailures++;
                _consecutivePathBudgetDeferrals = 0;
                _nextRepathTick = _clock.CurrentTick + _repathCooldownTicks;
                return;
            }

            if (end != requestedEnd)
                target = FlattenToMovementPlane(SimulationClock.Pathfinder.GetWorldPos(end));

            destinationChanged = !_hasDestination ||
                                 (target - _destination).sqrMagnitude >=
                                 (_repathDistanceThreshold * _repathDistanceThreshold);

            _destination = target;
            if (destinationChanged)
                ResetDestinationProgress(target);

            bool escalateBudgetPriority =
                !highPriority &&
                _consecutivePathBudgetDeferrals >= GetPathBudgetStarvationLimit();
            bool queryHighPriority = highPriority || escalateBudgetPriority;
            _currentDestinationHighPriority = queryHighPriority;

            if (!AIBudgetCoordinator.TryAcquirePathQuery(
                    _clock.CurrentTick,
                    _profile,
                    queryHighPriority))
            {
                bool canKeepExistingPath = _path != null &&
                                           _pathIndex < _path.Count &&
                                           !_routeBlocked;

                _hasDestination = true;
                if (!canKeepExistingPath)
                {
                    _path = null;
                    _pathIndex = 0;
                    _consecutivePathBudgetDeferrals++;
                    if (_consecutivePathBudgetDeferrals >= GetPathBudgetStarvationLimit())
                    {
                        AIIncidentLogger.Record(
                            _brawler.EntityID,
                            AIIncidentType.PathBudgetStarvation,
                            _clock.CurrentTick,
                            $"defers={_consecutivePathBudgetDeferrals}");
                    }
                }
                else
                {
                    _consecutivePathBudgetDeferrals = 0;
                }

                _routeBlocked = false;
                _nextRepathTick = _clock.CurrentTick + GetBudgetDeferredPathTicks();
                return;
            }

            _consecutivePathBudgetDeferrals = 0;
            _path = SimulationClock.Pathfinder.FindPathWithNavigationClearance(
                start.x,
                start.y,
                end.x,
                end.y,
                BoundaryClearanceCells,
                ObstacleClearanceCells);

            if (_path == null)
                _path = SimulationClock.Pathfinder.FindPath(start.x, start.y, end.x, end.y);

            _pathIndex = 0;
            _hasDestination = true;
            _routeBlocked = _path == null;
            if (_routeBlocked)
                _consecutiveRouteFailures++;
            else
                _consecutiveRouteFailures = 0;

            _nextRepathTick = _clock.CurrentTick + _repathCooldownTicks;
        }

        public void Stop()
        {
            _hasDestination = false;
            _path = null;
            _pathIndex = 0;
            _routeBlocked = false;
            _consecutiveStuckSamples = 0;
            _consecutiveRouteFailures = 0;
            _consecutiveActiveZeroMoveTicks = 0;
            _consecutivePathBudgetDeferrals = 0;
            _hasAvoidanceDirection = false;
            QueueMove(Vector3.zero);
        }

        public void ClearDestinationForFallback()
        {
            _hasDestination = false;
            _path = null;
            _pathIndex = 0;
            _routeBlocked = false;
            _currentDestinationHighPriority = false;
            _consecutiveStuckSamples = 0;
            _consecutiveRouteFailures = 0;
            _consecutiveActiveZeroMoveTicks = 0;
            _consecutivePathBudgetDeferrals = 0;
            _lastQueuedMoveDirection = Vector3.zero;
            _hasAvoidanceDirection = false;
        }

        public void Tick()
        {
            if (!_hasDestination)
            {
                QueueMove(Vector3.zero);
                return;
            }

            float distToDestinationSq = GetPlanarDelta(_destination).sqrMagnitude;
            if (distToDestinationSq <= (_arrivalDistance * _arrivalDistance))
            {
                Stop();
                return;
            }

            UpdateStuckCheck();
            UpdateDestinationProgress(distToDestinationSq);

            if (_routeBlocked)
            {
                if (_clock.CurrentTick >= _nextRepathTick)
                    ForceRepath(_destination, highPriority: true);

                if (_routeBlocked)
                {
                    QueueMove(Vector3.zero);
                    return;
                }
            }

            if (_path == null || _pathIndex >= _path.Count)
            {
                Vector3 directDir = GetPlanarDelta(_destination);
                if (directDir.sqrMagnitude > 0.0001f)
                    QueueMove(directDir.normalized);
                else
                    QueueMove(Vector3.zero);

                return;
            }

            Vector3 nodeWorld = SimulationClock.Pathfinder.GetWorldPos(_path[_pathIndex].X, _path[_pathIndex].Y);
            nodeWorld = FlattenToMovementPlane(nodeWorld);

            if (GetPlanarDelta(nodeWorld).sqrMagnitude <= 0.25f)
            {
                _pathIndex++;

                if (_pathIndex >= _path.Count)
                {
                    Vector3 finalDir = GetPlanarDelta(_destination);
                    QueueMove(finalDir.sqrMagnitude > 0.0001f ? finalDir.normalized : Vector3.zero);
                    return;
                }

                nodeWorld = SimulationClock.Pathfinder.GetWorldPos(_path[_pathIndex].X, _path[_pathIndex].Y);
                nodeWorld = FlattenToMovementPlane(nodeWorld);
            }

            Vector3 dir = GetPlanarDelta(nodeWorld);
            QueueMove(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero);
        }

        public bool TryGetFailureSignal(
            BrawlerAIProfile profile,
            uint currentTick,
            out AIFailureRecoverySignal signal)
        {
            signal = default;

            if (!_hasDestination || profile == null || !profile.EnableFailureRecovery)
                return false;

            float distance = Mathf.Sqrt(Mathf.Max(0f, GetPlanarDelta(_destination).sqrMagnitude));
            uint ageTicks = currentTick >= _destinationRequestTick
                ? currentTick - _destinationRequestTick
                : 0u;

            float progress = GetProgressDistance();

            if (_routeBlocked)
            {
                signal = BuildSignal(
                    AIFailureRecoveryReason.BlockedRoute,
                    currentTick,
                    Mathf.Max(1, _consecutiveRouteFailures),
                    distance,
                    ageTicks,
                    progress);
                return true;
            }

            if (_consecutiveStuckSamples >= Mathf.Max(1, profile.NavigationStuckSampleLimit))
            {
                signal = BuildSignal(
                    AIFailureRecoveryReason.NavigationStall,
                    currentTick,
                    _consecutiveStuckSamples,
                    distance,
                    ageTicks,
                    progress);
                return true;
            }

            if (ageTicks >= profile.StaleDestinationRecoveryTicks &&
                distance > Mathf.Max(_arrivalDistance * 1.5f, profile.TacticalMinimumStepDistance) &&
                progress <= Mathf.Max(0f, profile.StaleDestinationProgressThreshold))
            {
                signal = BuildSignal(
                    AIFailureRecoveryReason.StaleDestination,
                    currentTick,
                    1,
                    distance,
                    ageTicks,
                    progress);
                return true;
            }

            return false;
        }

        public bool TryRequestRecoveryDestination(
            AIFailureRecoveryRequest request,
            BrawlerAIProfile profile,
            out Vector3 recoveryDestination)
        {
            recoveryDestination = Vector3.zero;

            if (profile == null || !profile.EnableFailureRecovery)
                return false;

            Vector3 directionToDestination = GetPlanarDelta(_destination);

            if (directionToDestination.sqrMagnitude <= 0.001f)
                directionToDestination = _brawler.transform.forward;

            directionToDestination.y = 0f;
            if (directionToDestination.sqrMagnitude <= 0.001f)
                directionToDestination = Vector3.forward;

            directionToDestination.Normalize();

            float detourDistance = Mathf.Max(0.75f, profile.FailureRecoveryDetourDistance);
            int sideSign = request.SideSign >= 0 ? 1 : -1;

            if (SimulationClock.Pathfinder == null)
            {
                recoveryDestination = _brawler.Position + GetRecoveryDirection(directionToDestination, sideSign, 0) * detourDistance;
                ForceRepath(recoveryDestination, highPriority: true);
                _arrivalDistance = 0.35f;
                _routeBlocked = false;
                _consecutiveStuckSamples = 0;
                return true;
            }

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            Vector2Int start = pathfinder.GetGridCoords(_brawler.Position);

            if (!pathfinder.IsWalkableWithNavigationClearance(start) &&
                !pathfinder.TryGetNearestWalkableCoordsWithNavigationClearance(start, 2, out start) &&
                !pathfinder.TryGetNearestWalkableCoords(start, 2, out start))
            {
                return false;
            }

            bool found = false;
            Vector2Int bestCoords = start;
            float bestScore = float.MinValue;

            for (int i = 0; i < 6; i++)
            {
                Vector3 direction = GetRecoveryDirection(directionToDestination, sideSign, i);
                Vector3 candidateWorld = _brawler.Position + direction * detourDistance;
                Vector2Int candidateCoords = pathfinder.GetGridCoords(candidateWorld);

                if (!pathfinder.IsWalkableWithNavigationClearance(candidateCoords) &&
                    !pathfinder.TryGetNearestWalkableCoordsWithNavigationClearance(candidateCoords, 2, out candidateCoords) &&
                    !pathfinder.TryGetNearestWalkableCoordsWithBoundaryClearance(candidateCoords, 2, out candidateCoords))
                {
                    continue;
                }

                if (candidateCoords == start)
                    continue;

                if (!AIBudgetCoordinator.TryAcquirePathQuery(
                        _clock.CurrentTick,
                        profile,
                        highPriority: true))
                {
                    continue;
                }

                int pathLength;
                if (!pathfinder.TryGetPathLengthWithNavigationClearance(
                        start.x,
                        start.y,
                        candidateCoords.x,
                        candidateCoords.y,
                        out pathLength) &&
                    !pathfinder.TryGetPathLength(
                        start.x,
                        start.y,
                        candidateCoords.x,
                        candidateCoords.y,
                        out pathLength))
                {
                    continue;
                }

                Vector3 resolvedWorld = FlattenToMovementPlane(pathfinder.GetWorldPos(candidateCoords));
                float directionScore = Vector3.Dot(
                    GetPlanarDelta(resolvedWorld).normalized,
                    direction);
                float destinationSeparation = GetPlanarDelta(resolvedWorld, _destination).magnitude;
                float score = (directionScore * 10f) + destinationSeparation - (pathLength * 0.35f);

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestCoords = candidateCoords;
                }
            }

            if (!found)
                return false;

            recoveryDestination = FlattenToMovementPlane(pathfinder.GetWorldPos(bestCoords));
            _arrivalDistance = 0.35f;
            ForceRepath(recoveryDestination, highPriority: true);
            _routeBlocked = false;
            _consecutiveStuckSamples = 0;
            return true;
        }

        private void UpdateStuckCheck()
        {
            uint currentTick = _clock.CurrentTick;
            if ((currentTick - _lastSampleTick) < GetStuckSampleIntervalTicks())
                return;

            float movedSq = GetPlanarDelta(_brawler.Position, _lastSamplePosition).sqrMagnitude;
            float distToDestinationSq = GetPlanarDelta(_destination).sqrMagnitude;
            float stuckMoveThreshold = GetStuckMoveThreshold();

            if (movedSq < (stuckMoveThreshold * stuckMoveThreshold) &&
                distToDestinationSq > (_arrivalDistance * _arrivalDistance))
            {
                _consecutiveStuckSamples++;

                if (currentTick >= _nextRepathTick && !_routeBlocked)
                    ForceRepath(_destination, highPriority: true);
            }
            else
            {
                _consecutiveStuckSamples = 0;
            }

            _lastSamplePosition = _brawler.Position;
            _lastSampleTick = currentTick;
        }

        private void ResetDestinationProgress(Vector3 target)
        {
            _destinationRequestTick = _clock.CurrentTick;
            _destinationStartPosition = _brawler.Position;
            _destinationStartDistanceSq = GetPlanarDelta(target, _destinationStartPosition).sqrMagnitude;
            _bestDistanceToDestinationSq = _destinationStartDistanceSq;
            _lastSamplePosition = _brawler.Position;
            _lastSampleTick = _clock.CurrentTick;
        }

        private void UpdateDestinationProgress(float distToDestinationSq)
        {
            if (distToDestinationSq + 0.01f < _bestDistanceToDestinationSq)
                _bestDistanceToDestinationSq = distToDestinationSq;
        }

        private float GetProgressDistance()
        {
            float startDistance = Mathf.Sqrt(Mathf.Max(0f, _destinationStartDistanceSq));
            float bestDistance = Mathf.Sqrt(Mathf.Max(0f, _bestDistanceToDestinationSq));
            return Mathf.Max(0f, startDistance - bestDistance);
        }

        private AIFailureRecoverySignal BuildSignal(
            AIFailureRecoveryReason reason,
            uint currentTick,
            int consecutiveCount,
            float distance,
            uint destinationAgeTicks,
            float progressDistance)
        {
            return new AIFailureRecoverySignal
            {
                Reason = reason,
                Tick = currentTick,
                ConsecutiveCount = consecutiveCount,
                Destination = _destination,
                DistanceToDestination = distance,
                DestinationAgeTicks = destinationAgeTicks,
                ProgressDistance = progressDistance
            };
        }

        private static Vector3 GetRecoveryDirection(Vector3 forward, int sideSign, int index)
        {
            Vector3 side = new Vector3(forward.z, 0f, -forward.x) * sideSign;

            switch (index)
            {
                case 0:
                    return side.normalized;

                case 1:
                    return (-side).normalized;

                case 2:
                    return (-forward + side * 0.7f).normalized;

                case 3:
                    return (-forward - side * 0.7f).normalized;

                case 4:
                    return (forward + side * 0.45f).normalized;

                default:
                    return (forward - side * 0.45f).normalized;
            }
        }

        private uint GetBudgetDeferredPathTicks()
        {
            return _profile != null && _profile.BudgetDeferredPathTicks > 0u
                ? _profile.BudgetDeferredPathTicks
                : 1u;
        }

        private int GetPathBudgetStarvationLimit()
        {
            return _profile != null && _profile.PathBudgetStarvationLimit > 0
                ? _profile.PathBudgetStarvationLimit
                : 3;
        }

        private void QueueMove(Vector3 direction)
        {
            direction = ApplyLocalObstacleAvoidance(direction);
            TrackMoveCommand(direction);
            _commandSource?.QueueMove(direction, _currentDestinationHighPriority);
        }

        private Vector3 ApplyLocalObstacleAvoidance(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                _hasAvoidanceDirection = false;
                return direction;
            }

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return direction;

            float magnitude = Mathf.Clamp01(direction.magnitude);
            Vector3 desired = direction;
            desired.y = 0f;
            if (desired.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            desired.Normalize();
            Vector3 pressure = GetObstacleAvoidancePressure(pathfinder);
            bool hasPressure = pressure.sqrMagnitude > 0.0001f;
            Vector3 pressureDirection = hasPressure ? pressure.normalized : Vector3.zero;
            bool projectedWalkable = IsMoveProjectionWalkable(
                pathfinder,
                desired,
                requireNavigationClearance: false);
            bool movingIntoObstacle =
                hasPressure &&
                Vector3.Dot(desired, pressureDirection) < -0.15f;

            if (projectedWalkable && !movingIntoObstacle)
            {
                _hasAvoidanceDirection = false;
                return desired * magnitude;
            }

            if (_hasAvoidanceDirection &&
                _clock.CurrentTick <= _avoidanceDirectionLockUntilTick &&
                Vector3.Dot(_lastAvoidanceDirection, desired) > -0.4f &&
                IsMoveProjectionWalkable(
                    pathfinder,
                    _lastAvoidanceDirection,
                    requireNavigationClearance: false))
            {
                return _lastAvoidanceDirection * magnitude;
            }

            if (TrySelectAvoidanceDirection(
                    pathfinder,
                    desired,
                    pressureDirection,
                    out Vector3 adjusted))
            {
                _lastAvoidanceDirection = adjusted;
                _avoidanceDirectionLockUntilTick = _clock.CurrentTick + AvoidanceDirectionLockTicks;
                _hasAvoidanceDirection = true;
                return adjusted * magnitude;
            }

            return projectedWalkable ? desired * magnitude : Vector3.zero;
        }

        private Vector3 GetObstacleAvoidancePressure(AStarSolver pathfinder)
        {
            Vector2Int center = pathfinder.GetGridCoords(_brawler.Position);
            Vector3 pressure = Vector3.zero;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Vector2Int coords = new Vector2Int(center.x + x, center.y + y);
                    if (pathfinder.IsInBounds(coords) && pathfinder.IsWalkable(coords))
                        continue;

                    Vector3 away = _brawler.Position - pathfinder.GetWorldPos(coords);
                    away.y = 0f;
                    if (away.sqrMagnitude <= 0.0001f)
                        continue;

                    float weight = 1f / Mathf.Max(0.12f, away.sqrMagnitude);
                    pressure += away.normalized * weight;
                }
            }

            return pressure;
        }

        private bool TrySelectAvoidanceDirection(
            AStarSolver pathfinder,
            Vector3 desired,
            Vector3 pressureDirection,
            out Vector3 adjusted)
        {
            adjusted = Vector3.zero;
            bool found = false;
            float bestScore = float.MinValue;
            Vector3 side = new Vector3(desired.z, 0f, -desired.x);
            if (pressureDirection.sqrMagnitude > 0.0001f &&
                Vector3.Dot(side, pressureDirection) < 0f)
            {
                side = -side;
            }

            EvaluateAvoidanceCandidate(
                pathfinder,
                desired + pressureDirection * 0.90f,
                desired,
                pressureDirection,
                ref found,
                ref bestScore,
                ref adjusted);
            EvaluateAvoidanceCandidate(
                pathfinder,
                desired + side * 0.85f + pressureDirection * 0.35f,
                desired,
                pressureDirection,
                ref found,
                ref bestScore,
                ref adjusted);
            EvaluateAvoidanceCandidate(
                pathfinder,
                desired - side * 0.85f + pressureDirection * 0.35f,
                desired,
                pressureDirection,
                ref found,
                ref bestScore,
                ref adjusted);
            EvaluateAvoidanceCandidate(
                pathfinder,
                side + pressureDirection * 0.55f,
                desired,
                pressureDirection,
                ref found,
                ref bestScore,
                ref adjusted);
            EvaluateAvoidanceCandidate(
                pathfinder,
                -side + pressureDirection * 0.55f,
                desired,
                pressureDirection,
                ref found,
                ref bestScore,
                ref adjusted);
            EvaluateAvoidanceCandidate(
                pathfinder,
                pressureDirection,
                desired,
                pressureDirection,
                ref found,
                ref bestScore,
                ref adjusted);

            return found;
        }

        private void EvaluateAvoidanceCandidate(
            AStarSolver pathfinder,
            Vector3 candidate,
            Vector3 desired,
            Vector3 pressureDirection,
            ref bool found,
            ref float bestScore,
            ref Vector3 adjusted)
        {
            candidate.y = 0f;
            if (candidate.sqrMagnitude <= 0.0001f)
                return;

            candidate.Normalize();
            if (!IsMoveProjectionWalkable(
                    pathfinder,
                    candidate,
                    requireNavigationClearance: false))
            {
                return;
            }

            Vector3 destinationDirection = GetPlanarDelta(_destination);
            if (destinationDirection.sqrMagnitude > 0.0001f)
                destinationDirection.Normalize();

            float score =
                Vector3.Dot(candidate, desired) * 4f +
                Vector3.Dot(candidate, destinationDirection) * 1.5f;

            if (pressureDirection.sqrMagnitude > 0.0001f)
                score += Vector3.Dot(candidate, pressureDirection) * 2f;

            if (IsMoveProjectionWalkable(
                    pathfinder,
                    candidate,
                    requireNavigationClearance: true))
            {
                score += 3f;
            }

            if (_hasAvoidanceDirection &&
                _clock.CurrentTick <= _avoidanceDirectionLockUntilTick)
            {
                score += Vector3.Dot(candidate, _lastAvoidanceDirection) * 1.5f;
            }

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                adjusted = candidate;
            }
        }

        private bool IsMoveProjectionWalkable(
            AStarSolver pathfinder,
            Vector3 direction,
            bool requireNavigationClearance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            direction.Normalize();
            float lookahead = Mathf.Max(0.35f, pathfinder.CellSize * 0.75f);
            Vector2Int coords = pathfinder.GetGridCoords(_brawler.Position + direction * lookahead);

            return requireNavigationClearance
                ? pathfinder.IsWalkableWithNavigationClearance(coords)
                : pathfinder.IsWalkableWithBoundaryClearance(coords);
        }

        private void TrackMoveCommand(Vector3 direction)
        {
            bool activeZeroMove =
                _hasDestination &&
                direction.sqrMagnitude <= 0.0001f;
            uint currentTick = _clock.CurrentTick;

            if (_lastQueuedMoveTick != currentTick)
            {
                _consecutiveActiveZeroMoveTicks = activeZeroMove
                    ? _consecutiveActiveZeroMoveTicks + 1
                    : 0;
                _lastQueuedMoveTick = currentTick;
            }
            else if (!activeZeroMove)
            {
                _consecutiveActiveZeroMoveTicks = 0;
            }

            _lastQueuedMoveDirection = direction;
        }

        private uint GetStuckSampleIntervalTicks()
        {
            return _profile != null && _profile.NavigationStuckSampleIntervalTicks > 0u
                ? _profile.NavigationStuckSampleIntervalTicks
                : 8u;
        }

        private float GetStuckMoveThreshold()
        {
            return _profile != null && _profile.NavigationStuckMoveThreshold > 0f
                ? _profile.NavigationStuckMoveThreshold
                : 0.08f;
        }

        private int GetEndpointRepairRadius(AStarSolver pathfinder)
        {
            int profileRadius = _profile != null
                ? Mathf.CeilToInt(Mathf.Max(0f, _profile.MapDestinationSearchRadius) / Mathf.Max(0.1f, pathfinder.CellSize))
                : 0;

            return Mathf.Max(
                3,
                profileRadius,
                Mathf.Max(pathfinder.Width, pathfinder.Height) / 2);
        }

        private Vector3 FlattenToMovementPlane(Vector3 value)
        {
            value.y = _brawler.Position.y;
            return value;
        }

        private Vector3 ClampTargetToPathfinderBounds(Vector3 target)
        {
            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null || !IsOutsidePathfinderBounds(pathfinder, target))
                return target;

            return FlattenToMovementPlane(
                pathfinder.GetWorldPos(pathfinder.GetGridCoords(target)));
        }

        private static bool IsOutsidePathfinderBounds(AStarSolver pathfinder, Vector3 target)
        {
            float minX = pathfinder.Origin.x;
            float minZ = pathfinder.Origin.z;
            float maxX = minX + pathfinder.Width * pathfinder.CellSize;
            float maxZ = minZ + pathfinder.Height * pathfinder.CellSize;

            return target.x < minX ||
                   target.x >= maxX ||
                   target.z < minZ ||
                   target.z >= maxZ;
        }

        private Vector3 GetPlanarDelta(Vector3 destination)
        {
            return GetPlanarDelta(destination, _brawler.Position);
        }

        private static Vector3 GetPlanarDelta(Vector3 destination, Vector3 origin)
        {
            Vector3 delta = destination - origin;
            delta.y = 0f;
            return delta;
        }
    }
}
