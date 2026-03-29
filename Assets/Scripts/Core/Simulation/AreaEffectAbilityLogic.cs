using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class AreaEffectAbilityLogic : IAbilityLogic
    {
        private readonly List<BrawlerController> _targets = new List<BrawlerController>(16);

        public void Tick(uint currentTick)
        {
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            BrawlerController caster = user as BrawlerController;
            if (caster == null || context.AbilityDefinition == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            if (context.AbilityDefinition is not EffectAoEAbilityDefinition effectDef)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            caster.ResolveTargets(
                effectDef.TargetTeamRule,
                effectDef.TargetSelectionRule,
                effectDef.Radius,
                _targets,
                effectDef.IncludeSelf,
                effectDef.RequireAlive);

            bool appliedAny = false;

            for (int i = 0; i < _targets.Count; i++)
            {
                BrawlerController target = _targets[i];
                if (target == null)
                    continue;

                if (effectDef.Effects == null)
                    continue;

                for (int e = 0; e < effectDef.Effects.Length; e++)
                {
                    AbilityEffectDefinition effect = effectDef.Effects[e];
                    if (effect == null)
                        continue;

                    if (effect.Apply(user, target, context))
                        appliedAny = true;
                }
            }

            return appliedAny
                ? AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType)
                : AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
        }
    }
}