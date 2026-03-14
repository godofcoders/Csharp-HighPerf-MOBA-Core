using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class StarPowerDefinition : ScriptableObject
    {
        // This is called when the Brawler is initialized
        public abstract void Apply(MOBA.Core.Simulation.BrawlerState state);
    }
}