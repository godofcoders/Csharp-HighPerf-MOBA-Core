using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class AreaMoveSpeedBuffSupportLogic : IAbilityLogic
    {
        private readonly List<BrawlerController> _targets = new List<BrawlerController>(16);

        public void Tick(uint currentTick)
        {
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            BrawlerController caster = user as BrawlerController;
            if (caster == null || caster.State == null || context.AbilityDefinition == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            float radius = 5f;
            float buffAmount = 1.5f;
            float durationSeconds = 4f;

            if (context.AbilityDefinition is AoEAbilityDefinition aoe)
                radius = aoe.Radius;

            caster.ResolveTargets(
                AbilityTargetTeamRule.Ally,
                AbilityTargetSelectionRule.Nearest,
                radius,
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
                    EffectType = SupportEffectType.MoveSpeedBuff,
                    Magnitude = buffAmount,
                    DurationSeconds = durationSeconds,
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