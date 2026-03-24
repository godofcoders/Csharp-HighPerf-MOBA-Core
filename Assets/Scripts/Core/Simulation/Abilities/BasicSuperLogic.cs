using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.Abilities
{
    public class BasicSuperLogic : IAbilityLogic
    {
        private readonly BasicSuperDefinition _def;

        public BasicSuperLogic(BasicSuperDefinition def)
        {
            _def = def;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user is not BrawlerController owner)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            owner.FireProjectile(
                origin: owner.transform.position,
                direction: context.Direction,
                speed: _def.ProjectileSpeed,
                range: _def.Range,
                damage: _def.Damage,
                sourceAbility: context.AbilityDefinition,
                slotType: AbilitySlotType.Super,
                isSuper: true,
                isGadget: false
            );

            return AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
        }

        public void Tick(uint currentTick) { }
    }
}