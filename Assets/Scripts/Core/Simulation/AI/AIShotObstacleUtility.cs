using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class AIShotObstacleUtility
    {
        private const float DirectShotHeightOffset = 0.25f;
        private const float DirectShotRadius = 0.22f;
        private const int HitBufferSize = 8;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[HitBufferSize];
        private static bool _hasResolvedObstacleMask;
        private static int _resolvedObstacleMask;

        public static bool CanFireAtTarget(
            BrawlerController attacker,
            AbilityDefinition ability,
            in AIAbilityCastPlan plan,
            ISpatialEntity intendedTarget,
            float fallbackRange,
            out string reason)
        {
            reason = "clear";

            if (attacker == null || ability == null)
                return true;

            if (!RequiresDirectFireLane(ability, plan))
                return true;

            Vector3 direction = plan.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                reason = "invalid_direction";
                return false;
            }

            direction.Normalize();

            float maxDistance = ResolveCheckDistance(attacker, ability, intendedTarget, fallbackRange);
            if (maxDistance <= 0.05f)
                return true;

            int obstacleMask = ResolveObstacleMask();
            if (obstacleMask == 0)
                return true;

            Vector3 origin = attacker.Position + Vector3.up * DirectShotHeightOffset;
            float radius = Mathf.Max(DirectShotRadius, ability.AimPreviewWidth * 0.35f);
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                HitBuffer,
                maxDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return true;

            RaycastHit hit = FindClosestValidHit(hitCount);
            if (hit.collider == null)
                return true;

            BreakableObjectController breakable = hit.collider.GetComponentInParent<BreakableObjectController>();
            if (breakable == null || breakable.IsDestroyed)
            {
                reason = "solid_obstacle";
                return false;
            }

            if (SpatialEntityUtility.IsSameEntity(intendedTarget, breakable))
            {
                reason = "target_breakable";
                return CanDamageBreakable(attacker, ability, breakable);
            }

            if (!CanDamageBreakable(attacker, ability, breakable))
            {
                reason = "unbreakable_obstacle";
                return false;
            }

            if (ResolveAbilityDamage(ability) < breakable.CurrentHealth)
            {
                reason = "durable_breakable_obstacle";
                return false;
            }

            reason = "breakable_obstacle";
            return true;
        }

        private static bool RequiresDirectFireLane(
            AbilityDefinition ability,
            in AIAbilityCastPlan plan)
        {
            if (plan.HasTargetPoint &&
                (ability.TargetingType == AbilityTargetingType.Area ||
                 ability.TargetingType == AbilityTargetingType.Point))
            {
                return false;
            }

            if (ability is ThrownHybridAoEAbilityDefinition ||
                ability is ThrownVolleyAoEAbilityDefinition ||
                ability is AoEAbilityDefinition ||
                ability is EffectAbilityDefinition)
            {
                return false;
            }

            return ability.DeliveryType == AbilityDeliveryType.Projectile ||
                   ability is ProjectileAbilityDefinition ||
                   ability is BasicProjectileAttackDefinition ||
                   ability is BurstSequenceProjectileAbilityDefinition ||
                   ability is ChainProjectileAbilityDefinition ||
                   ability is HybridProjectileAbilityDefinition ||
                   ability is VolleyProjectileAbilityDefinition ||
                   ability is BasicSuperDefinition;
        }

        private static float ResolveCheckDistance(
            BrawlerController attacker,
            AbilityDefinition ability,
            ISpatialEntity intendedTarget,
            float fallbackRange)
        {
            float range = ability != null
                ? Mathf.Max(0.25f, ability.GetAIMaxRange())
                : Mathf.Max(0.25f, fallbackRange);

            if (fallbackRange > 0f)
                range = Mathf.Max(range, fallbackRange);

            if (!SpatialEntityUtility.IsAlive(intendedTarget))
                return range;

            float distanceToTarget = Vector3.Distance(attacker.Position, intendedTarget.Position);
            float targetRadius = Mathf.Max(0.1f, intendedTarget.CollisionRadius);
            return Mathf.Min(range, Mathf.Max(0.25f, distanceToTarget - targetRadius * 0.45f));
        }

        private static RaycastHit FindClosestValidHit(int hitCount)
        {
            RaycastHit best = default;
            float bestDistance = float.MaxValue;

            int count = Mathf.Min(hitCount, HitBuffer.Length);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == null ||
                    !hit.collider.enabled ||
                    !hit.collider.gameObject.activeInHierarchy ||
                    hit.distance < 0f ||
                    hit.distance >= bestDistance)
                {
                    continue;
                }

                best = hit;
                bestDistance = hit.distance;
            }

            return best;
        }

        private static bool CanDamageBreakable(
            BrawlerController attacker,
            AbilityDefinition ability,
            BreakableObjectController breakable)
        {
            if (breakable == null || breakable.IsDestroyed)
                return false;

            DamageContext context = new DamageContext
            {
                Attacker = attacker,
                Target = breakable,
                Damage = ResolveAbilityDamage(ability),
                Type = ResolveDamageType(ability),
                HitPosition = breakable.Position,
                Direction = breakable.Position - (attacker != null ? attacker.Position : breakable.Position),
                SourceAbility = ability,
                IsSuper = ability != null && ability.SlotType == AbilitySlotType.Super
            };

            return breakable.CanReceiveDamage(context);
        }

        private static float ResolveAbilityDamage(AbilityDefinition ability)
        {
            if (ability is ProjectileAbilityDefinition projectile) return projectile.Damage * Mathf.Max(1, projectile.ProjectileCount);
            if (ability is BasicProjectileAttackDefinition basic) return basic.Damage;
            if (ability is BurstSequenceProjectileAbilityDefinition burst) return burst.Damage * Mathf.Max(1, burst.ProjectileCount);
            if (ability is ChainProjectileAbilityDefinition chain) return chain.Damage;
            if (ability is HybridProjectileAbilityDefinition hybridProjectile) return hybridProjectile.EnemyDamage;
            if (ability is VolleyProjectileAbilityDefinition volley) return volley.Damage * Mathf.Max(1, volley.ProjectileCount);
            if (ability is BasicSuperDefinition basicSuper) return basicSuper.Damage;
            if (ability is ThrownHybridAoEAbilityDefinition thrownHybrid) return thrownHybrid.EnemyDamage;
            if (ability is ThrownVolleyAoEAbilityDefinition thrownVolley) return thrownVolley.EnemyDamage * Mathf.Max(1, thrownVolley.ProjectileCount);
            if (ability is HybridAoEAbilityDefinition hybridAoE) return hybridAoE.EnemyDamage;
            if (ability is AoEAbilityDefinition aoe) return aoe.Damage;
            return 0f;
        }

        private static DamageType ResolveDamageType(AbilityDefinition ability)
        {
            if (ability is AoEAbilityDefinition ||
                ability is ThrownHybridAoEAbilityDefinition ||
                ability is ThrownVolleyAoEAbilityDefinition)
            {
                return DamageType.AoE;
            }

            return DamageType.Projectile;
        }

        private static int ResolveObstacleMask()
        {
            if (_hasResolvedObstacleMask)
                return _resolvedObstacleMask;

            _hasResolvedObstacleMask = true;

            MapGenerator mapGenerator = Object.FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
            {
                _resolvedObstacleMask = mapGenerator.ObstacleLayer.value;
                return _resolvedObstacleMask;
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            _resolvedObstacleMask = obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
            return _resolvedObstacleMask;
        }
    }
}
