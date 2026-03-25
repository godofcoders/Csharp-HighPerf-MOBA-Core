using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public interface IPassiveRuntime
    {
        PassiveDefinition Definition { get; }
        object SourceToken { get; }

        void OnInstalled(BrawlerState state);
        void Tick(BrawlerState state, uint currentTick);
        void OnUninstalled(BrawlerState state);
    }
}