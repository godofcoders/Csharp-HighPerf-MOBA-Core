using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "HealEffect", menuName = "MOBA/Effects/Heal Effect")]
    public class HealEffectDefinition : AbilityEffectDefinition
    {
        public float HealAmount = 800f;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            if (target == null || target.State == null)
                return false;

            target.State.Heal(HealAmount);
            return true;
        }
    }
}