using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class BuffZoneDeployableBehavior : IDeployableBehavior
    {
        private readonly List<BrawlerController> _targets = new List<BrawlerController>(16);

        private DeployableController _controller;
        private uint _nextPulseTick;

        public void Initialize(DeployableController controller)
        {
            _controller = controller;
            _nextPulseTick = 0;
        }

        public void Tick(uint currentTick)
        {
            if (_controller == null || _controller.Definition == null)
                return;

            if (currentTick < _nextPulseTick)
                return;

            Pulse(currentTick);
            _nextPulseTick = currentTick + (uint)(_controller.Definition.PulseIntervalSeconds * 30f);
        }

        private void Pulse(uint currentTick)
        {
            AbilityDefinition ability = _controller.Definition.AbilityDefinition;
            if (ability is not EffectAoEAbilityDefinition effectDef)
                return;

            AbilityTargetRequest request = new AbilityTargetRequest
            {
                Source = _controller.Owner,
                Origin = _controller.Position,
                Direction = _controller.transform.forward,
                Range = _controller.Definition.PulseRadius,
                TeamRule = AbilityTargetTeamRule.Ally,
                SelectionRule = AbilityTargetSelectionRule.Nearest,
                CountRule = AbilityTargetCountRule.Multiple,
                IncludeSelf = true,
                RequireAlive = true
            };

            _targets.Clear();
            AbilityTargetResolver.ResolveTargets(request, _targets);

            if (effectDef.Effects == null)
                return;

            AbilityExecutionContext context = new AbilityExecutionContext
            {
                Source = _controller.Owner,
                AbilityDefinition = ability,
                SlotType = AbilitySlotType.Super,
                Origin = _controller.Position,
                Direction = _controller.transform.forward,
                StartTick = currentTick,
                IsSuper = false,
                IsGadget = false
            };

            for (int i = 0; i < _targets.Count; i++)
            {
                BrawlerController target = _targets[i];
                if (target == null)
                    continue;

                for (int e = 0; e < effectDef.Effects.Length; e++)
                {
                    AbilityEffectDefinition effect = effectDef.Effects[e];
                    if (effect == null)
                        continue;

                    effect.Apply(_controller.AbilityUser, target, context);
                }
            }
        }
    }
}