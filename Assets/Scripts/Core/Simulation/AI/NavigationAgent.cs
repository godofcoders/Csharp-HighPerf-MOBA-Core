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

        private uint _nextRepathTick;
        private readonly uint _repathCooldownTicks = 12;
        private readonly float _repathDistanceThreshold = 1.0f;

        private Vector3 _lastSamplePosition;
        private uint _lastSampleTick;
        private readonly uint _stuckSampleInterval = 15;
        private readonly float _stuckMoveThreshold = 0.15f;
        private int _consecutiveStuckSamples;
        private int _consecutiveRouteFailures;

        public Vector3 Position => _brawler.Position;
        public bool HasDestination => _hasDestination;
        public Vector3 Destination => _destination;
        public bool IsRouteBlocked => _routeBlocked;
        public int ConsecutiveStuckSamples => _consecutiveStuckSamples;
        public int ConsecutiveRouteFailures => _consecutiveRouteFailures;

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
            _arrivalDistance = arrivalDistance;

            if (!_hasDestination)
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
            bool destinationChanged = !_hasDestination ||
                                      (target - _destination).sqrMagnitude >=
                                      (_repathDistanceThreshold * _repathDistanceThreshold);

            _destination = target;
            if (destinationChanged)
                ResetDestinationProgress(target);

            if (SimulationClock.Pathfinder == null)
            {
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
            _commandSource?.QueueMove(Vector3.zero);
        }

        public void Tick()
        {
            if (!_hasDestination)
            {
                _commandSource?.QueueMove(Vector3.zero);
                return;
            }

            float distToDestinationSq = (_destination - _brawler.Position).sqrMagnitude;
            if (distToDestinationSq <= (_arrivalDistance * _arrivalDistance))
            {
                Stop();
                return;
            }

            UpdateStuckCheck();
            UpdateDestinationProgress(distToDestinationSq);

            if (_routeBlocked)
            {
                _commandSource?.QueueMove(Vector3.zero);
                return;
            }

            if (_path == null || _pathIndex >= _path.Count)
            {
                Vector3 directDir = _destination - _brawler.Position;
                if (directDir.sqrMagnitude > 0.0001f)
                    _commandSource?.QueueMove(directDir.normalized);
                else
                    _commandSource?.QueueMove(Vector3.zero);

                return;
            }

            Vector3 nodeWorld = SimulationClock.Pathfinder.GetWorldPos(_path[_pathIndex].X, _path[_pathIndex].Y);

            if ((nodeWorld - _brawler.Position).sqrMagnitude <= 0.25f)
            {
                _pathIndex++;

                if (_pathIndex >= _path.Count)
                {
                    Vector3 finalDir = _destination - _brawler.Position;
                    _commandSource?.QueueMove(finalDir.sqrMagnitude > 0.0001f ? finalDir.normalized : Vector3.zero);
                    return;
                }

                nodeWorld = SimulationClock.Pathfinder.GetWorldPos(_path[_pathIndex].X, _path[_pathIndex].Y);
            }

            Vector3 dir = nodeWorld - _brawler.Position;
            _commandSource?.QueueMove(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero);
        }

        public bool TryGetFailureSignal(
            BrawlerAIProfile profile,
            uint currentTick,
            out AIFailureRecoverySignal signal)
        {
            signal = default;

            if (!_hasDestination || profile == null || !profile.EnableFailureRecovery)
                return false;

            float distance = Mathf.Sqrt(Mathf.Max(0f, (_destination - _brawler.Position).sqrMagnitude));
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

            Vector3 directionToDestination = _destination - _brawler.Position;
            directionToDestination.y = 0f;

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

                Vector3 resolvedWorld = pathfinder.GetWorldPos(candidateCoords);
                float directionScore = Vector3.Dot(
                    (resolvedWorld - _brawler.Position).normalized,
                    direction);
                float destinationSeparation = Vector3.Distance(resolvedWorld, _destination);
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

            recoveryDestination = pathfinder.GetWorldPos(bestCoords);
            _arrivalDistance = 0.35f;
            ForceRepath(recoveryDestination, highPriority: true);
            _routeBlocked = false;
            _consecutiveStuckSamples = 0;
            return true;
        }

        private void UpdateStuckCheck()
        {
            uint currentTick = _clock.CurrentTick;
            if ((currentTick - _lastSampleTick) < _stuckSampleInterval)
                return;

            float movedSq = (_brawler.Position - _lastSamplePosition).sqrMagnitude;
            float distToDestinationSq = (_destination - _brawler.Position).sqrMagnitude;

            if (movedSq < (_stuckMoveThreshold * _stuckMoveThreshold) &&
                distToDestinationSq > (_arrivalDistance * _arrivalDistance))
            {
                _consecutiveStuckSamples++;

                if (currentTick >= _nextRepathTick && !_routeBlocked)
                    ForceRepath(_destination);
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
            _destinationStartDistanceSq = (target - _destinationStartPosition).sqrMagnitude;
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
    }
}
