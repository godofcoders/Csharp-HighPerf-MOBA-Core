using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "EffectAbility", menuName = "MOBA/Abilities/Effect Ability")]
    public class EffectAbilityDefinition : AbilityDefinition
    {
        [Header("Effects")]
        public AbilityEffectDefinition[] Effects;

        [Header("Preview")]
        public float PreviewRange = 6f;

        public override IAbilityLogic CreateLogic()
        {
            return new EffectAbilityLogic(this);
        }

        public override float GetAIIdealRange()
        {
            return PreviewRange * 0.85f;
        }

        public override float GetAIMaxRange()
        {
            return PreviewRange;
        }
    }
}
