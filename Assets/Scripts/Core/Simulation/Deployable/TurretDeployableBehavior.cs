using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class TurretDeployableBehavior : IDeployableBehavior
    {
        private DeployableController _controller;
        private uint _nextActionTick;

        public void Initialize(DeployableController controller)
        {
            _controller = controller;
            _nextActionTick = 0;
        }

        public void Tick(uint currentTick)
        {
            if (_controller == null || _controller.Definition == null)
                return;

            if (currentTick < _nextActionTick)
                return;

            BrawlerController target = ResolveTarget();
            if (target == null)
                return;

            FireAt(target, currentTick);
        }

        private BrawlerController ResolveTarget()
        {
            AbilityTargetRequest request = new AbilityTargetRequest
            {
                Source = _controller.Owner,
                Origin = _controller.Position,
                Direction = _controller.transform.forward,
                Range = _controller.Definition.DetectionRadius,
                TeamRule = AbilityTargetTeamRule.Enemy,
                SelectionRule = AbilityTargetSelectionRule.Nearest,
                CountRule = AbilityTargetCountRule.Single,
                IncludeSelf = false,
                RequireAlive = true
            };

            return AbilityTargetResolver.ResolveSingleTarget(request);
        }

        private void FireAt(BrawlerController target, uint currentTick)
        {
            AbilityDefinition ability = _controller.Definition.AbilityDefinition;
            if (ability == null)
                return;

            IAbilityLogic logic = ability.CreateLogic();
            if (logic == null)
                return;

            var context = new AbilityExecutionContext
            {
                Source = _controller.Owner,
                AbilityDefinition = ability,
                SlotType = AbilitySlotType.MainAttack,
                Origin = _controller.Position,
                Direction = (target.Position - _controller.Position).normalized,
                StartTick = currentTick,
                IsSuper = false,
                IsGadget = false
            };

            logic.Execute(_controller.Owner, context);
            _nextActionTick = currentTick + (uint)(_controller.Definition.ActionIntervalSeconds * 30f);
        }
    }
}