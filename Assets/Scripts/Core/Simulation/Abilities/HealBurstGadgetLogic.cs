using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.Abilities
{
    public class HealBurstGadgetLogic : IAbilityLogic
    {
        private readonly float _healAmount;

        public HealBurstGadgetLogic(float healAmount)
        {
            _healAmount = healAmount;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user is not BrawlerController owner || owner.State == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            owner.State.Heal(_healAmount);

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.ConsumedResource = true;
            result.TargetsAffected = 1;

            return result;
        }

        public void Tick(uint currentTick)
        {
        }
    }
}