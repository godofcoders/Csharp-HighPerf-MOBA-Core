using UnityEngine;
using System.Collections.Generic;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        [SerializeField] private BrawlerController _brawler;

        private NavigationAgent _navAgent;
        private AIBlackboard _blackboard;
        private BTNode _treeRoot;

        public void SetTarget(BrawlerController brawler)
        {
            _brawler = brawler;

            _navAgent = new NavigationAgent(_brawler);

            _blackboard = new AIBlackboard();

            _treeRoot = new BTSelector(_blackboard, new List<BTNode>
            {
                new BTSequence(_blackboard, new List<BTNode>
                {
                    new NodeFindTarget(_blackboard, _brawler),
                    new NodeMoveToTarget(_blackboard, _navAgent),
                    new NodeChaseTarget(_blackboard, _navAgent)
                }),

                new NodeWander(_blackboard, _navAgent)
            });
        }

        protected override void Awake()
        {
            base.Awake();
        }

        public override void Tick(uint currentTick)
        {
            if (_brawler == null)
                return;

            if (_brawler.State == null)
                return;

            if (_brawler.State.IsDead)
                return;

            if (SimulationClock.Grid == null)
                return;

            // AI decision update
            if (currentTick % 6 == 0)
            {
                _treeRoot?.Evaluate();
            }

            // Navigation update
            _navAgent?.Tick();

            // Attack logic
            var target = _blackboard.Get<ISpatialEntity>("Target");

            if (target != null && currentTick % 30 == 0)
            {
                Vector3 dir = (target.Position - _brawler.Position).normalized;

                _brawler.BufferAttack(InputCommandType.MainAttack, dir);
            }
        }
    }
}