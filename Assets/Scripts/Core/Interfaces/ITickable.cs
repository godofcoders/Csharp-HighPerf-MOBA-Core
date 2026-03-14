namespace MOBA.Core.Simulation 
{
    public interface ITickable 
    {
        // We pass the current tick number for determinism
        void Tick(uint currentTick);
    }
}