using UnityEngine;
using MOBA.Core.Simulation.AI;
using System.Collections.Generic;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        [SerializeField] private BrawlerController _brawler;

        private AIBlackboard _blackboard;
        private BTNode _treeRoot;

        protected override void Awake()
        {
            base.Awake();
            _blackboard = new AIBlackboard();

            // CONSTRUCT THE TREE: 
            // If can't find target, fail. If found, move to it.
            _treeRoot = new BTSequence(_blackboard, new List<BTNode>
            {
                new NodeFindTarget(_blackboard, _brawler),
                new NodeMoveToTarget(_blackboard, _brawler)
            });
        }

        public override void Tick(uint currentTick)
        {
            if (_brawler.State.IsDead) return;

            // The brain "thinks" every tick
            _treeRoot.Evaluate();

            // Simple Attack Logic: If we have a target, fire!
            var target = _blackboard.Get<ISpatialEntity>("Target");
            if (target != null && currentTick % 30 == 0) // Fire once per second
            {
                Vector3 dir = (target.Position - _brawler.Position).normalized;
                _brawler.BufferAttack(Simulation.InputCommandType.MainAttack, dir);
            }
        }
    }
}