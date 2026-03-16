using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class NodeFindTarget : BTNode
    {
        private BrawlerController _self;

        public NodeFindTarget(AIBlackboard bb, BrawlerController self) : base(bb)
        {
            _self = self;
        }

        public override BTNodeState Evaluate()
        {
            if (SimulationClock.Grid == null)
                return BTNodeState.Failure;

            if (_self == null || _self.State == null || _self.State.IsDead)
                return BTNodeState.Failure;

            ISpatialEntity closest = null;
            float minDist = float.MaxValue;

            var targets = SimulationClock.Grid.GetEntitiesInRadius(_self.Position, 30f);

            if (targets == null || targets.Count == 0)
                return BTNodeState.Failure;

            foreach (var entity in targets)
            {
                if (entity.EntityID == _self.EntityID)
                    continue;

                if (entity.Team == _self.Team)
                    continue;

                float dist = (entity.Position - _self.Position).sqrMagnitude;

                if (dist < minDist)
                {
                    minDist = dist;
                    closest = entity;
                }
            }

            if (closest == null)
                return BTNodeState.Failure;

            Blackboard.Set("Target", closest);

            return BTNodeState.Success;
        }
    }

    public class NodeMoveToTarget : BTNode
    {
        private NavigationAgent _agent;
        private float _attackRange = 6f;

        public NodeMoveToTarget(AIBlackboard bb, NavigationAgent agent) : base(bb)
        {
            _agent = agent;
        }

        public override BTNodeState Evaluate()
        {
            var target = Blackboard.Get<ISpatialEntity>("Target");

            if (target == null)
                return BTNodeState.Failure;

            float dist = (target.Position - _agent.Position).sqrMagnitude;

            if (dist <= _attackRange * _attackRange)
                return BTNodeState.Success;

            _agent.SetDestination(target.Position);

            return BTNodeState.Running;
        }
    }
}