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
            if (_definition == null || user == null)
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            if (user is not BrawlerController brawler)
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            Vector3 origin = context.Origin;
            Vector3 targetPoint;

            if (context.HasTargetPoint)
            {
                targetPoint = context.TargetPoint;
            }
            else
            {
                Vector3 dir = context.Direction.sqrMagnitude > 0.001f
                    ? context.Direction.normalized
                    : brawler.transform.forward;

                targetPoint = origin + dir * _definition.ThrowRange;
            }

            Vector3 flatOffset = targetPoint - origin;
            flatOffset.y = 0f;

            float throwDistance = flatOffset.magnitude;
            if (throwDistance > _definition.ThrowRange && throwDistance > 0.001f)
            {
                flatOffset = flatOffset.normalized * _definition.ThrowRange;
                targetPoint = origin + flatOffset;
                throwDistance = flatOffset.magnitude;
            }

            Vector3 direction = throwDistance > 0.001f
                ? flatOffset.normalized
                : brawler.transform.forward;

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
                SuperChargeOnHit = 0.20f,
                IsSuper = context.IsSuper,
                IsGadget = context.IsGadget,

                IsHybrid = false,
                AllyHealAmount = 0f,
                EnemyDamageAmount = 0f,
                HitTeamRule = _definition.HitTeamRule,
                CanAffectEnemiesOnImpact = _definition.CanAffectEnemiesOnImpact,
                CanAffectAlliesOnImpact = _definition.CanAffectAlliesOnImpact,

                DeliveryType = ProjectileDeliveryType.ThrownImpactAoE,
                TargetPoint = targetPoint,

                HasHybridAoEImpact = true,
                ImpactRadius = _definition.ImpactRadius,
                ImpactEnemyDamage = _definition.EnemyDamage,
                ImpactAllyHeal = _definition.AllyHeal,
                LingeringHazardDefinition = _definition.LingeringHazard,
                UseArcMotion = true,
                ArcHeight = 1.75f,
                TravelDistance = throwDistance,

                PresentationProfile = _definition.PresentationProfile,

                IsChainProjectile = false,
                RemainingBounces = 0,
                BounceRadius = 0f
            };

            projectileService.FireProjectile(spawnContext);

            var result = AbilityExecutionResult.Succeeded(_definition, context.SlotType);
            result.SpawnedProjectile = true;
            result.ProjectileCount = 1;
            result.ConsumedResource = true;
            return result;
        }

        public void Tick(uint currentTick)
        {
        }
    }
}