using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class NavigationAgent
    {
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

        public Vector3 Position => _brawler.Position;
        public bool HasDestination => _hasDestination;
        public Vector3 Destination => _destination;
        public bool IsRouteBlocked => _routeBlocked;
        public int ConsecutiveStuckSamples => _consecutiveStuckSamples;
        public int ConsecutiveRouteFailures => _consecutiveRouteFailures;
        public Vector3 LastQueuedMoveDirection => _lastQueuedMoveDirection;
        public int ConsecutiveActiveZeroMoveTicks => _consecutiveActiveZeroMoveTicks;
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
                _nextRepathTick = _clock.CurrentTick + _repathCooldownTicks;
                return;
            }

            var start = SimulationClock.Pathfinder.GetGridCoords(_brawler.Position);
            var end = SimulationClock.Pathfinder.GetGridCoords(target);
            var requestedEnd = end;
            int endpointRepairRadius = GetEndpointRepairRadius(SimulationClock.Pathfinder);
            bool foundStart = true;
            bool foundEnd = true;

            if (!SimulationClock.Pathfinder.IsWalkable(start))
                foundStart = SimulationClock.Pathfinder.TryGetNearestWalkableCoords(
                    start,
                    endpointRepairRadius,
                    out start);

            if (!SimulationClock.Pathfinder.IsWalkable(end))
                foundEnd = SimulationClock.Pathfinder.TryGetNearestWalkableCoords(
                    end,
                    endpointRepairRadius,
                    out end);

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

            if (!AIBudgetCoordinator.TryAcquirePathQuery(
                    _clock.CurrentTick,
                    _profile,
                    highPriority))
            {
                bool canKeepExistingPath = _path != null &&
                                           _pathIndex < _path.Count &&
                                           !_routeBlocked;

                _hasDestination = true;
                if (!canKeepExistingPath)
                {
                    _path = null;
                    _pathIndex = 0;
                }

                _routeBlocked = false;
                _nextRepathTick = _clock.CurrentTick + GetBudgetDeferredPathTicks();
                return;
            }

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
            _lastQueuedMoveDirection = Vector3.zero;
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

            if (!pathfinder.IsWalkable(start) &&
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

                if (!pathfinder.IsWalkable(candidateCoords) &&
                    !pathfinder.TryGetNearestWalkableCoords(candidateCoords, 2, out candidateCoords))
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

                if (!pathfinder.TryGetPathLength(
                        start.x,
                        start.y,
                        candidateCoords.x,
                        candidateCoords.y,
                        out int pathLength))
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

        private void QueueMove(Vector3 direction)
        {
            TrackMoveCommand(direction);
            _commandSource?.QueueMove(direction, _currentDestinationHighPriority);
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
