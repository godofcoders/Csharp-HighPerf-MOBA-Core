namespace MOBA.Core.Simulation
{
    public sealed class SummonUnitDeployableBehavior : IDeployableBehavior
    {
        private DeployableController _controller;

        public void Initialize(DeployableController controller)
        {
            _controller = controller;
        }

        public void Tick(uint currentTick)
        {
            if (_controller == null || _controller.Definition == null)
                return;

            // Architecture placeholder:
            // later this family will use navigation/chase/attack like a lightweight bot actor.
        }
    }
}