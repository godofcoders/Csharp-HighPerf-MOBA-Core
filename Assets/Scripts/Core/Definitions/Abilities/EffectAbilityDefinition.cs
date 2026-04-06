using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "EffectAbility", menuName = "MOBA/Abilities/Effect Ability")]
    public class EffectAbilityDefinition : AbilityDefinition
    {
        [Header("Effects")]
        public AbilityEffectDefinition[] Effects;

        public override IAbilityLogic CreateLogic()
        {
            return new EffectAbilityLogic(this);
        }
    }
}