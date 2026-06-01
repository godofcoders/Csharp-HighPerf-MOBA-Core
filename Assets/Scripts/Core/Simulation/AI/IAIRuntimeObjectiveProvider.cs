using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public interface IAIRuntimeObjectiveProvider
    {
        GameModeId ModeId { get; }

        bool TryGetRuntimeObjective(
            TeamType team,
            AIObjectiveType preferredType,
            Vector3 selfPosition,
            out AIObjectiveCandidate objective);
    }
}
