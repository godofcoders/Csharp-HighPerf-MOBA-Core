using UnityEngine;
using MOBA.Core.Simulation.AI;
using System.Collections.Generic;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        [SerializeField] private BrawlerController _brawler;
        public void SetTarget(BrawlerController b)
        {
            _brawler = b;

            _blackboard = new AIBlackboard();

            _treeRoot = new BTSelector(_blackboard, new List<BTNode>
{
    new BTSequence(_blackboard, new List<BTNode>
    {
        new NodeFindTarget(_blackboard, _brawler),
        new NodeMoveToTarget(_blackboard, _brawler)
    }),
    new NodeWander(_blackboard, _brawler)
});
        }

        private AIBlackboard _blackboard;
        private BTNode _treeRoot;

        protected override void Awake()
        {
            base.Awake();
        }


        public override void Tick(uint currentTick)
        {
            if (SimulationClock.Grid == null)
            {
                Debug.Log("AI stopped: Grid null");
                return;
            }

            if (_brawler == null)
            {
                Debug.Log("AI stopped: Brawler null");
                return;
            }

            if (_brawler.State == null)
            {
                Debug.Log("AI stopped: State null");
                return;
            }

            if (_brawler.State.IsDead)
            {
                Debug.Log("AI stopped: Brawler is dead");
                return;
            }

            // The brain "thinks" every tick
            _treeRoot.Evaluate();

            // Simple Attack Logic: If we have a target, fire!
            var target = _blackboard.Get<ISpatialEntity>("Target");
            if (target != null && currentTick % 30 == 0) // Fire once per second
            {
                Vector3 dir = (target.Position - _brawler.Position).normalized;
                _brawler.BufferAttack(InputCommandType.MainAttack, dir);
            }
        }
    }
}