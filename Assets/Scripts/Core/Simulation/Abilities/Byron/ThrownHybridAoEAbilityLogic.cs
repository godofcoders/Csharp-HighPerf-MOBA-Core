using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.Abilities
{
    public class ThrownHybridAoEAbilityLogic : IAbilityLogic
    {
        private readonly ThrownHybridAoEAbilityDefinition _definition;

        public ThrownHybridAoEAbilityLogic(ThrownHybridAoEAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null || context.Source == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            if (!(context.Source is BrawlerController brawler))
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            Vector3 direction = context.Direction.sqrMagnitude > 0.001f
                ? context.Direction.normalized
                : brawler.transform.forward;

            Vector3 origin = brawler.GetCastPosition();
            Vector3 targetPoint = origin + (direction * _definition.ThrowRange);

            var projectileService = ServiceProvider.Get<IProjectileService>();

            var spawnContext = new ProjectileSpawnContext
            {
                Owner = brawler,
                SourceAbility = _definition,
                SlotType = context.SlotType,

                Origin = origin,
                Direction = direction,
                Speed = _definition.ThrowSpeed,
                Range = _definition.ThrowRange,
                Damage = 0f,

                Team = brawler.Team,
                SuperChargeOnHit = 0f,
                IsSuper = context.IsSuper,
                IsGadget = context.IsGadget,

                IsHybrid = false,
                AllyHealAmount = 0f,
                EnemyDamageAmount = 0f,
                HitTeamRule = ProjectileHitTeamRule.EnemiesOnly,

                DeliveryType = ProjectileDeliveryType.ThrownImpactAoE,
                TargetPoint = targetPoint,

                HasHybridAoEImpact = true,
                ImpactRadius = _definition.ImpactRadius,
                ImpactEnemyDamage = _definition.EnemyDamage,
                ImpactAllyHeal = _definition.AllyHeal,
            };

            projectileService.FireProjectile(spawnContext);

            return AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
        }

        public void Tick(uint currentTick)
        {
        }
    }
}