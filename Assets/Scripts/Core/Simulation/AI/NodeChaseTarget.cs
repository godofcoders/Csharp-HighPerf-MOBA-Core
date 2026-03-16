using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

public class NodeChaseTarget : BTNode
{
    private NavigationAgent _agent;

    public NodeChaseTarget(AIBlackboard bb, NavigationAgent agent) : base(bb)
    {
        _agent = agent;
    }

    public override BTNodeState Evaluate()
    {
        var target = Blackboard.Get<ISpatialEntity>("Target");
        if (target == null)
            return BTNodeState.Failure;

        _agent.SetDestination(target.Position);

        return BTNodeState.Running;
    }
}