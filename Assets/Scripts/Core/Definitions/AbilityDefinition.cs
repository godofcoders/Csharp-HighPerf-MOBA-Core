using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class AbilityDefinition : ScriptableObject
    {
        public string AbilityName;
        public Sprite Icon;
        public float Cooldown = 1.0f;

        // This is a "Factory Method" - it will return the logic POCO 
        // we will build in the next phase.
        public abstract MOBA.Core.Simulation.IAbilityLogic CreateLogic();
    }
}