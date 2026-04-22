using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class AllyHealPulseGadgetLogic : IAbilityLogic
    {
        private readonly List<BrawlerController> _targets = new List<BrawlerController>(16);
        private readonly float _healAmount;
        private readonly float _radius;

        public AllyHealPulseGadgetLogic(float healAmount, float radius)
        {
            _healAmount = healAmount;
            _radius = radius;
        }

        public void Tick(uint currentTick)
        {
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            BrawlerController caster = user as BrawlerController;
            if (caster == null || caster.State == null || context.AbilityDefinition == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            caster.ResolveTargets(
                AbilityTargetTeamRule.Ally,
                AbilityTargetSelectionRule.LowestHealth,
                _radius,
                _targets,
                includeSelf: true,
                requireAlive: true);

            bool appliedAny = false;
            object sourceToken = new object();

            for (int i = 0; i < _targets.Count; i++)
            {
                BrawlerController target = _targets[i];
                if (target == null)
                    continue;

                SupportEffectRequest request = new SupportEffectRequest
                {
                    Source = caster,
                    Target = target,
                    EffectType = SupportEffectType.Heal,
                    Magnitude = _healAmount,
                    DurationSeconds = 0f,
                    SourceToken = sourceToken,
                    Origin = caster.Position
                };

                if (SupportEffectApplier.Apply(request))
                    appliedAny = true;
            }

            return appliedAny
                ? AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType)
                : AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
        }
    }
}