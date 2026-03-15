using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class NodeWander : BTNode
    {
        private BrawlerController _self;
        private Vector3 _wanderDir;
        private int _nextChangeTick;

        public NodeWander(AIBlackboard bb, BrawlerController self) : base(bb)
        {
            _self = self;
        }

        public override BTNodeState Evaluate()
        {
            uint tick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            if (tick > _nextChangeTick)
            {
                _wanderDir = new Vector3(
                    Random.Range(-1f, 1f),
                    0,
                    Random.Range(-1f, 1f)
                ).normalized;

                _nextChangeTick = (int)tick + 60;
            }

            _self.SetMoveInput(_wanderDir);

            return BTNodeState.Running;
        }
    }
}