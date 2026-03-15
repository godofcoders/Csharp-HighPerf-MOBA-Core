namespace MOBA.Core.Simulation
{
    public interface ISimulationClock
    {
        uint CurrentTick { get; }
        float TickDelta { get; }
        // We can add logic to pause/speed up simulation here later
    }
}