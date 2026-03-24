using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public class BasicProjectileAttackLogic : IAbilityLogic
    {
        private readonly BasicProjectileAttackDefinition _def;

        public BasicProjectileAttackLogic(BasicProjectileAttackDefinition def)
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
                slotType: AbilitySlotType.MainAttack,
                isSuper: false,
                isGadget: false
            );

            return AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
        }

        public void Tick(uint currentTick) { }
    }
}