using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public sealed class EffectAbilityLogic : IAbilityLogic
    {
        private readonly EffectAbilityDefinition _definition;

        public EffectAbilityLogic(EffectAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null || user == null)
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            if (_definition.Effects == null || _definition.Effects.Length == 0)
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            bool anyApplied = false;

            for (int i = 0; i < _definition.Effects.Length; i++)
            {
                AbilityEffectDefinition effect = _definition.Effects[i];
                if (effect == null)
                    continue;

                if (effect.Apply(user, null, context))
                    anyApplied = true;
            }

            return anyApplied
                ? AbilityExecutionResult.Succeeded(_definition, context.SlotType)
                : AbilityExecutionResult.Failed(_definition, context.SlotType);
        }

        public void Tick(uint currentTick)
        {
        }
    }
}