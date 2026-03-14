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
            // Use our Spatial Grid to find enemies efficiently
            var nearby = SimulationClock.Grid?.GetEntitiesInCell(_self.Position);
            if (nearby == null) return BTNodeState.Failure;

            ISpatialEntity closest = null;
            float minDist = float.MaxValue;

            foreach (var entity in nearby)
            {
                if (entity.EntityID == _self.EntityID) continue;

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
            if (_currentPath == null || _pathIndex >= _currentPath.Count)
            {
                // Note: You would call SimulationClock.Pathfinder.FindPath() here
                // using the target's grid position
                return BTNodeState.Running;
            }
            Vector3 targetPos = new Vector3(_currentPath[_pathIndex].X, 0, _currentPath[_pathIndex].Y);
            Vector3 dir = (targetPos - _self.Position).normalized;
            _self.SetMoveInput(dir); // Pushing intent to the controller

            // If we are close enough, we succeeded in moving to them
            if ((targetPos - _self.Position).sqrMagnitude < 0.2f) _pathIndex++;

            return BTNodeState.Running;
        }
    }
}