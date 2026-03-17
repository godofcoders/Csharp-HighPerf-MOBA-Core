using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class AbilityDefinition : ScriptableObject
    {
        public string AbilityName;
        public Sprite Icon;
        public float Cooldown = 1.0f;

        public abstract MOBA.Core.Simulation.IAbilityLogic CreateLogic();

        // AI-facing hooks.
        // These let the brain derive spacing from the real gameplay ability,
        // instead of inventing a separate fake range model.
        public virtual float GetAIIdealRange()
        {
            return 6f;
        }

        public virtual float GetAIMaxRange()
        {
            return GetAIIdealRange();
        }
    }
}