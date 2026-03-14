using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    public enum BTNodeState { Running, Success, Failure }

    public abstract class BTNode
    {
        protected AIBlackboard Blackboard;
        public BTNode(AIBlackboard blackboard) => Blackboard = blackboard;
        public abstract BTNodeState Evaluate();
    }

    // SELECTOR: Runs children until one succeeds (The "OR" gate)
    public class BTSelector : BTNode
    {
        private List<BTNode> _children;
        public BTSelector(AIBlackboard bb, List<BTNode> children) : base(bb) => _children = children;

        public override BTNodeState Evaluate()
        {
            foreach (var child in _children)
            {
                var state = child.Evaluate();
                if (state != BTNodeState.Failure) return state;
            }
            return BTNodeState.Failure;
        }
    }

    // SEQUENCE: Runs children until one fails (The "AND" gate)
    public class BTSequence : BTNode
    {
        private List<BTNode> _children;
        public BTSequence(AIBlackboard bb, List<BTNode> children) : base(bb) => _children = children;

        public override BTNodeState Evaluate()
        {
            foreach (var child in _children)
            {
                var state = child.Evaluate();
                if (state != BTNodeState.Success) return state;
            }
            return BTNodeState.Success;
        }
    }
}