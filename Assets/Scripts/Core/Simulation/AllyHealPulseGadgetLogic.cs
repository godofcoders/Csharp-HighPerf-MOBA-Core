using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class AllyHealPulseGadgetLogic : IAbilityLogic
    {
        private readonly List<BrawlerController> _targets = new List<BrawlerController>(16);

        public void Tick(uint currentTick)
        {
        }

        public AbilityExecutionResult Execute(BrawlerController user, AbilityExecutionContext context)
        {
            if (user == null || user.State == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            float healAmount = 800f;
            float radius = 6f;

            if (context.AbilityDefinition is AoEAbilityDefinition aoe)
            {
                radius = aoe.Radius;
            }

            user.ResolveTargets(
                AbilityTargetTeamRule.Ally,
                AbilityTargetSelectionRule.LowestHealth,
                radius,
                _targets,
                includeSelf: true,
                requireAlive: true);

            bool healedAnyone = false;

            for (int i = 0; i < _targets.Count; i++)
            {
                BrawlerController target = _targets[i];
                if (target == null || target.State == null)
                    continue;

                target.State.Heal(healAmount);
                healedAnyone = true;
            }

            return healedAnyone
                ? AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType)
                : AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}