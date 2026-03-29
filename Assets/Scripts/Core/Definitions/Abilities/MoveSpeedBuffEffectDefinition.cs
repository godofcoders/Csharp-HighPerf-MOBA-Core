using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "MoveSpeedBuffEffect", menuName = "MOBA/Effects/Move Speed Buff Effect")]
    public class MoveSpeedBuffEffectDefinition : AbilityEffectDefinition
    {
        public float Magnitude = 1.5f;
        public float DurationSeconds = 4f;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            if (target == null || target.State == null)
                return false;

            object effectSource = this;

            var modifier = new MovementModifier(
                MovementModifierType.SpeedMultiplier,
                Magnitude,
                effectSource);

            target.State.AddIncomingMovementModifier(modifier);
            return true;
        }
    }
}