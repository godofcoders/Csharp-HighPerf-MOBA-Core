using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;

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

            float beforeHealth = owner.State.CurrentHealth;
            owner.State.Heal(_healAmount);
            float healingDone = owner.State.CurrentHealth - beforeHealth;
            if (healingDone > 0f)
            {
                AIReportCardTracker.RecordHealingDone(
                    owner,
                    owner,
                    healingDone,
                    context.IsSuper,
                    context.StartTick);
            }

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
