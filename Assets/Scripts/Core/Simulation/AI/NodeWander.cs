using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public class NodeWander : BTNode
    {
        private NavigationAgent _agent;
        private uint _nextChangeTick;

        public NodeWander(AIBlackboard bb, NavigationAgent agent) : base(bb)
        {
            _agent = agent;
        }

        public override BTNodeState Evaluate()
        {
            uint tick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            if (tick > _nextChangeTick)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-6f, 6f),
                    0f,
                    Random.Range(-6f, 6f)
                );

                Vector3 wanderPoint = _agent.Position + randomOffset;

                _agent.SetDestination(wanderPoint);

                _nextChangeTick = tick + 120;
            }

            return BTNodeState.Running;
        }
    }
}