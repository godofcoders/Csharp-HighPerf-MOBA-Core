using System.Collections;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.Abilities
{
    /// <summary>
    /// Volley-of-arcing-bottles logic. Fires N arcing ThrownImpactAoE
    /// projectiles with per-shot landing spread + distance jitter + delay.
    /// Mirrors ThrownHybridAoEAbilityLogic's spawn shape for a single bottle
    /// and sequences them via BrawlerController.RunTimedBurst (same coroutine
    /// pattern Colt's BurstSequenceProjectileLogic uses).
    ///
    /// Landing pattern: bottles are distributed evenly across LandingSpreadAngle
    /// around the aim direction. DistanceJitter adds a small random offset to
    /// each bottle's landing distance so the pattern looks organic rather than
    /// a perfect fan at a fixed radius.
    /// </summary>
    public sealed class ThrownVolleyAoEAbilityLogic : IAbilityLogic
    {
        private readonly ThrownVolleyAoEAbilityDefinition _definition;

        public ThrownVolleyAoEAbilityLogic(ThrownVolleyAoEAbilityDefinition definition)
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
            Vector3 baseDirection = ResolveBaseDirection(brawler, context);

            brawler.RunTimedBurst(FireVolleyRoutine(brawler, origin, baseDirection, context));

            var result = AbilityExecutionResult.Succeeded(_definition, context.SlotType);
            result.SpawnedProjectile = true;
            result.ProjectileCount = Mathf.Max(1, _definition.ProjectileCount);
            result.ConsumedResource = true;
            return result;
        }

        public void Tick(uint currentTick)
        {
        }

        private Vector3 ResolveBaseDirection(BrawlerController brawler, AbilityExecutionContext context)
        {
            if (context.HasTargetPoint)
            {
                Vector3 flat = context.TargetPoint - context.Origin;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                    return flat.normalized;
            }

            if (context.Direction.sqrMagnitude > 0.001f)
                return context.Direction.normalized;

            return brawler.transform.forward;
        }

        private IEnumerator FireVolleyRoutine(BrawlerController brawler, Vector3 origin, Vector3 baseDirection, AbilityExecutionContext context)
        {
            int count = Mathf.Max(1, _definition.ProjectileCount);
            float totalSpread = _definition.LandingSpreadAngle;
            float step = count > 1 ? totalSpread / (count - 1) : 0f;
            float startAngle = -totalSpread * 0.5f;

            var projectileService = ServiceProvider.Get<IProjectileService>();

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? startAngle + step * i : 0f;
                Vector3 shotDirection = (Quaternion.Euler(0f, angle, 0f) * baseDirection).normalized;

                float jitter = _definition.DistanceJitter > 0f
                    ? 1f + Random.Range(-_definition.DistanceJitter, _definition.DistanceJitter)
                    : 1f;

                float shotDistance = Mathf.Clamp(_definition.ThrowRange * jitter, 0.1f, _definition.ThrowRange);
                Vector3 shotTarget = origin + shotDirection * shotDistance;

                projectileService.FireProjectile(BuildSpawnContext(brawler, origin, shotDirection, shotTarget, shotDistance, context));

                if (i < count - 1 && _definition.DelayBetweenShots > 0f)
                    yield return new WaitForSeconds(_definition.DelayBetweenShots);
            }
        }

        private ProjectileSpawnContext BuildSpawnContext(
            BrawlerController brawler,
            Vector3 origin,
            Vector3 direction,
            Vector3 targetPoint,
            float travelDistance,
            AbilityExecutionContext context)
        {
            return new ProjectileSpawnContext
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

                // Supers shouldn't charge themselves via their own hits; 0 is
                // the conservative choice and matches Brawl Stars behavior.
                SuperChargeOnHit = 0f,

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
                ArcHeight = _definition.ArcHeight,
                TravelDistance = travelDistance,

                PresentationProfile = _definition.PresentationProfile,

                IsChainProjectile = false,
                RemainingBounces = 0,
                BounceRadius = 0f
            };
        }
    }
}
