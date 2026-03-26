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

        private List<PathNode> _path;
        private int _pathIndex;

        private Vector3 _destination;
        private bool _hasDestination;
        private float _arrivalDistance = 0.6f;

        private uint _nextRepathTick;
        private readonly uint _repathCooldownTicks = 12;
        private readonly float _repathDistanceThreshold = 1.0f;

        private Vector3 _lastSamplePosition;
        private uint _lastSampleTick;
        private readonly uint _stuckSampleInterval = 15;
        private readonly float _stuckMoveThreshold = 0.15f;

        public Vector3 Position => _brawler.Position;
        public bool HasDestination => _hasDestination;

        public NavigationAgent(BrawlerController brawler, AICommandSource commandSource)
        {
            _brawler = brawler;
            _commandSource = commandSource;
            _clock = ServiceProvider.Get<ISimulationClock>();
            _lastSamplePosition = brawler.Position;
            _lastSampleTick = _clock.CurrentTick;
        }

        public void RequestDestination(Vector3 target, float arrivalDistance = 0.6f)
        {
            _arrivalDistance = arrivalDistance;

            if (!_hasDestination)
            {
                ForceRepath(target);
                return;
            }

            float movedTargetSq = (target - _destination).sqrMagnitude;
            if (movedTargetSq >= (_repathDistanceThreshold * _repathDistanceThreshold) &&
                _clock.CurrentTick >= _nextRepathTick)
            {
                ForceRepath(target);
                return;
            }

            _destination = target;
        }

        public void ForceRepath(Vector3 target)
        {
            _destination = target;

            if (SimulationClock.Pathfinder == null)
            {
                _hasDestination = true;
                _path = null;
                _pathIndex = 0;
                _nextRepathTick = _clock.CurrentTick + _repathCooldownTicks;
                return;
            }

            var start = SimulationClock.Pathfinder.GetGridCoords(_brawler.Position);
            var end = SimulationClock.Pathfinder.GetGridCoords(target);

            _path = SimulationClock.Pathfinder.FindPath(start.x, start.y, end.x, end.y);
            _pathIndex = 0;
            _hasDestination = true;
            _nextRepathTick = _clock.CurrentTick + _repathCooldownTicks;
        }

        public void Stop()
        {
            _hasDestination = false;
            _path = null;
            _pathIndex = 0;
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

        private void UpdateStuckCheck()
        {
            uint currentTick = _clock.CurrentTick;
            if ((currentTick - _lastSampleTick) < _stuckSampleInterval)
                return;

            float movedSq = (_brawler.Position - _lastSamplePosition).sqrMagnitude;
            float distToDestinationSq = (_destination - _brawler.Position).sqrMagnitude;

            if (movedSq < (_stuckMoveThreshold * _stuckMoveThreshold) &&
                distToDestinationSq > (_arrivalDistance * _arrivalDistance) &&
                currentTick >= _nextRepathTick)
            {
                ForceRepath(_destination);
            }

            _lastSamplePosition = _brawler.Position;
            _lastSampleTick = currentTick;
        }
    }
}