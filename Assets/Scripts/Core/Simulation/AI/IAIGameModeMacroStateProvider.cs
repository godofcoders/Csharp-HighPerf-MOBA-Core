using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public interface IAIGameModeMacroStateProvider
    {
        GameModeId ModeId { get; }

        bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state);
    }
}
