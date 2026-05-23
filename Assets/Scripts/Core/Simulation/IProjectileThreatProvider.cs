using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface IProjectileThreatProvider
    {
        void AppendProjectileThreatsNonAlloc(
            Vector3 observerPosition,
            TeamType observerTeam,
            float scanRadius,
            List<GameplayThreatInfo> results);
    }
}
