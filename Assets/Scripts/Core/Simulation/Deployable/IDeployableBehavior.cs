namespace MOBA.Core.Simulation
{
    public interface IDeployableBehavior
    {
        void Initialize(DeployableController controller);
        void Tick(uint currentTick);
    }
}