using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.Abilities
{
    /// <summary>
    /// Instant ammo refill gadget ("Speedloader"-style). Restores the caster's
    /// ammo bar on activation. Currently supports full-refill only — partial
    /// refill (e.g. +2 rounds) is a planned extension once ResourceStorage
    /// exposes an Add(int) API. The RefillAmount field is accepted for future
    /// forward-compat; any non-positive value means "full refill".
    /// </summary>
    public class AmmoRefillGadgetLogic : IAbilityLogic
    {
        private readonly int _refillAmount;

        public AmmoRefillGadgetLogic(int refillAmount)
        {
            _refillAmount = refillAmount;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user is not BrawlerController owner || owner.State == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            // TODO(partial-refill): when ResourceStorage.Add(int) lands, branch on _refillAmount
            // so designers can tune Speedloader to e.g. +2 rounds without full top-up.
            owner.State.Resources.RefillAmmo();

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.ConsumedResource = true;
            result.TargetsAffected = 1;
            return result;
        }

        public void Tick(uint currentTick) { }
    }
}
