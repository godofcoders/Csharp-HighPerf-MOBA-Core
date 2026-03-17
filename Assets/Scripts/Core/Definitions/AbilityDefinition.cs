using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class AbilityDefinition : ScriptableObject
    {
        public string AbilityName;
        public Sprite Icon;
        public float Cooldown = 1.0f;

        [Header("AI / Combat Tags")]
        public AbilityTag[] Tags;

        public abstract MOBA.Core.Simulation.IAbilityLogic CreateLogic();

        public virtual float GetAIIdealRange()
        {
            return 6f;
        }

        public virtual float GetAIMaxRange()
        {
            return GetAIIdealRange();
        }

        public bool HasTag(AbilityTag tag)
        {
            if (Tags == null) return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (Tags[i] == tag)
                    return true;
            }

            return false;
        }
    }
}