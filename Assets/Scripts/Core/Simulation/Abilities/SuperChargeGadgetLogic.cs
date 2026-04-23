using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.Abilities
{
    /// <summary>
    /// Instant super-meter charge gadget. Grants a configurable fraction
    /// (0..1) of the super meter to the caster on activation. Useful for
    /// "my deployable gathered energy — channel it into my super" fantasies
    /// (e.g. Jessie's turret -> PowerSurge) without needing a deployable
    /// registry lookup.
    /// </summary>
    public class SuperChargeGadgetLogic : IAbilityLogic
    {
        private readonly float _chargeFraction;

        public SuperChargeGadgetLogic(float chargeFraction)
        {
            _chargeFraction = chargeFraction;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user is not BrawlerController owner || owner.State == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            owner.State.Resources.AddSuperCharge(_chargeFraction);

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.ConsumedResource = true;
            result.TargetsAffected = 1;
            return result;
        }

        public void Tick(uint currentTick) { }
    }
}
