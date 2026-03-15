using UnityEngine;
using System.Collections.Generic;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    // ACTION: Find the nearest enemy in the Spatial Grid
    public class NodeFindTarget : BTNode
    {
        private BrawlerController _self;
        public NodeFindTarget(AIBlackboard bb, BrawlerController self) : base(bb) => _self = self;

        public override BTNodeState Evaluate()
        {
            Debug.Log("Evaluating Find Target");
            // 1. Guard: Is the global Grid ready?
            if (SimulationClock.Grid == null)
            {
                Debug.Log("Find Target failed: Grid null");
                return BTNodeState.Failure;
            }

            // 2. Guard: Does this node have its own reference to the brawler?
            // This is likely where your error is!
            if (_self == null)
            {
                Debug.Log("Find Target failed: Brawler null");
                return BTNodeState.Failure;
            }

            if (_self.State == null)
            {
                Debug.Log("Find Target failed: State null");
                return BTNodeState.Failure;
            }

            if (_self.State.IsDead)
            {
                Debug.Log("Find Target failed: Brawler is dead");
                return BTNodeState.Failure;
            }


            ISpatialEntity closest = null;
            float minDist = float.MaxValue;

            // 3. The logic call (uses _self.Position)
            var targets = SimulationClock.Grid.GetEntitiesInRadius(_self.Position, 30f);
            Debug.Log($"Entities in radius: {targets?.Count}");
            if (targets == null || targets.Count == 0) return BTNodeState.Failure;

            foreach (var entity in targets)
            {
                // Don't target yourself or teammates
                if (entity.EntityID == _self.EntityID || entity.Team == _self.Team) continue;

                float dist = (entity.Position - _self.Position).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = entity;
                }
            }

            if (closest != null)
            {
                Blackboard.Set("Target", closest);
                return BTNodeState.Success;
            }

            return BTNodeState.Failure;
        }
    }

    // ACTION: Move towards the target saved in Blackboard
    public class NodeMoveToTarget : BTNode
    {
        private BrawlerController _self;
        private List<PathNode> _currentPath;
        private int _pathIndex;
        public NodeMoveToTarget(AIBlackboard bb, BrawlerController self) : base(bb) => _self = self;

        public override BTNodeState Evaluate()
        {
            var target = Blackboard.Get<ISpatialEntity>("Target");
            if (target == null) return BTNodeState.Failure;

            var pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null) return BTNodeState.Failure;

            // 1. Path Calculation (Bake path only if target moved significantly or we have no path)
            if (_currentPath == null || ServiceProvider.Get<ISimulationClock>().CurrentTick % 20 == 0)
            {
                var startCoords = SimulationClock.Pathfinder.GetGridCoords(_self.Position);
                var endCoords = SimulationClock.Pathfinder.GetGridCoords(target.Position);

                // A* Solve
                _currentPath = SimulationClock.Pathfinder.FindPath(startCoords.x, startCoords.y, endCoords.x, endCoords.y);
                _pathIndex = 0;
            }

            if (_currentPath == null || _currentPath.Count == 0) return BTNodeState.Failure;

            // 2. Waypoint Following
            if (_pathIndex < _currentPath.Count)
            {
                // Convert Grid Node back to World Position
                Vector3 targetPos = SimulationClock.Pathfinder.GetWorldPos(_currentPath[_pathIndex].X, _currentPath[_pathIndex].Y);
                Vector3 dir = (targetPos - _self.Position).normalized;

                // Push movement to the "Body"
                _self.SetMoveInput(dir);

                // Check if we reached this waypoint
                if ((targetPos - _self.Position).sqrMagnitude < 0.25f)
                {
                    _pathIndex++;
                }

                return BTNodeState.Running;
            }

            return BTNodeState.Success;
        }
    }
}