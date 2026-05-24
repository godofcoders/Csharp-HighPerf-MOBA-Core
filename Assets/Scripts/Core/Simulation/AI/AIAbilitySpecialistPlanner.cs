using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIAbilitySpecialistPlanner
    {
        private const float HybridHealHealthThreshold = 0.68f;
        private const float HybridEmergencyHealthThreshold = 0.40f;
        private const float HealthyDeployableKeepThreshold = 0.35f;
        private const float FireLaneWidth = 1.15f;
        private const uint TargetMotionForgetTicks = 90;

        private readonly BrawlerController _self;
        private readonly List<ISpatialEntity> _buffer = new List<ISpatialEntity>(24);
        private readonly Dictionary<int, TargetMotionRecord> _targetMotion =
            new Dictionary<int, TargetMotionRecord>(16);

        private struct TargetMotionRecord
        {
            public Vector3 Position;
            public uint Tick;
        }

        public AIAbilitySpecialistPlanner(BrawlerController self)
        {
            _self = self;
        }

        public bool TryBuildMainAttackPlan(
            ISpatialEntity requestedTarget,
            float maxRange,
            uint currentTick,
            out AIAbilityCastPlan plan)
        {
            plan = default;

            AbilityDefinition ability = _self != null ? _self.Definition?.MainAttack : null;
            if (ability == null)
                return false;

            float abilityRange = GetAbilityRange(ability, maxRange);

            if (ability is HybridProjectileAbilityDefinition hybrid &&
                TryFindHybridHealTarget(hybrid.AllyHeal, abilityRange, includeSelf: false, out BrawlerController allyToHeal) &&
                ShouldPrioritizeHybridHeal(allyToHeal, requestedTarget))
            {
                plan = BuildDirectionalPlan(allyToHeal, "hybrid_ally_heal");
                return true;
            }

            if (!SpatialEntityUtility.IsAlive(requestedTarget))
                return false;

            if (!IsWithinRange(requestedTarget.Position, abilityRange))
                return false;

            if (ability is ThrownHybridAoEAbilityDefinition thrown)
            {
                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    thrown.ThrowRange,
                    currentTick);

                Vector3 point = BuildAreaDenialPoint(
                    targetPoint,
                    thrown.ThrowRange,
                    Mathf.Max(2.5f, thrown.ImpactRadius * 1.25f));

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "thrown_area_denial");
                return true;
            }

            if (ability is ThrownVolleyAoEAbilityDefinition volley)
            {
                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    volley.ThrowRange,
                    currentTick);

                Vector3 point = BuildAreaDenialPoint(
                    targetPoint,
                    volley.ThrowRange,
                    Mathf.Max(3.5f, volley.ImpactRadius * 1.5f));

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "volley_area_denial");
                return true;
            }

            if (ability is BurstSequenceProjectileAbilityDefinition)
            {
                if (!TryBuildPredictiveProjectilePlan(
                        ability,
                        requestedTarget,
                        abilityRange,
                        currentTick,
                        requireQualityGate: true,
                        "line_pressure",
                        out plan))
                {
                    return false;
                }

                return true;
            }

            if (AIPredictiveCombatUtility.TryGetProjectileKinematics(
                    ability,
                    abilityRange,
                    out _,
                    out _))
            {
                return TryBuildPredictiveProjectilePlan(
                    ability,
                    requestedTarget,
                    abilityRange,
                    currentTick,
                    requireQualityGate: true,
                    "predictive_main",
                    out plan);
            }

            plan = BuildDirectionalPlan(requestedTarget, "default_main");
            return true;
        }

        public bool TryBuildSuperPlan(
            ISpatialEntity requestedTarget,
            float superRange,
            uint currentTick,
            out AIAbilityCastPlan plan)
        {
            plan = default;

            AbilityDefinition ability = _self != null ? _self.Definition?.SuperAbility : null;
            if (ability == null)
                return false;

            float abilityRange = GetAbilityRange(ability, superRange);

            if (ability is EffectAbilityDefinition effect &&
                effect.TargetingType == AbilityTargetingType.Area)
            {
                if (HasHealthyOwnedDeployable())
                    return false;

                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                Vector3 point = BuildDeployablePressurePoint(requestedTarget.Position, abilityRange);
                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "deployable_pressure");
                plan.ForceUse = true;
                return true;
            }

            if (ability is ThrownHybridAoEAbilityDefinition hybridAoE)
            {
                if (TryFindHybridHealTarget(
                        hybridAoE.AllyHeal,
                        abilityRange,
                        includeSelf: true,
                        out BrawlerController allyToHeal) &&
                    ShouldPrioritizeHybridHeal(allyToHeal, requestedTarget))
                {
                    plan = AIAbilityCastPlan.PointTarget(
                        allyToHeal,
                        _self.Position,
                        ClampPointToRange(allyToHeal.Position, abilityRange),
                        "hybrid_super_heal");
                    plan.ForceUse = true;
                    return true;
                }

                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    hybridAoE.ThrowRange,
                    currentTick);

                Vector3 point = BuildAreaDenialPoint(
                    targetPoint,
                    hybridAoE.ThrowRange,
                    Mathf.Max(3f, hybridAoE.ImpactRadius * 0.75f));

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "hybrid_super_damage");
                return true;
            }

            if (ability is ThrownVolleyAoEAbilityDefinition volley)
            {
                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    volley.ThrowRange,
                    currentTick);

                Vector3 point = BuildAreaDenialPoint(
                    targetPoint,
                    volley.ThrowRange,
                    Mathf.Max(4f, volley.ImpactRadius * 1.5f));

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "volley_super_denial");
                return true;
            }

            if (ability is BurstSequenceProjectileAbilityDefinition)
            {
                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                return TryBuildPredictiveProjectilePlan(
                    ability,
                    requestedTarget,
                    abilityRange,
                    currentTick,
                    requireQualityGate: false,
                    "super_line_pressure",
                    out plan);
            }

            if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                return false;

            if (AIPredictiveCombatUtility.TryGetProjectileKinematics(
                    ability,
                    abilityRange,
                    out _,
                    out _))
            {
                return TryBuildPredictiveProjectilePlan(
                    ability,
                    requestedTarget,
                    abilityRange,
                    currentTick,
                    requireQualityGate: false,
                    "predictive_super",
                    out plan);
            }

            plan = BuildDirectionalPlan(requestedTarget, "default_super");
            return true;
        }

        public bool ShouldHoldSuperForSpecialistValue()
        {
            AbilityDefinition ability = _self != null ? _self.Definition?.SuperAbility : null;

            return ability is EffectAbilityDefinition effect &&
                   effect.TargetingType == AbilityTargetingType.Area &&
                   HasHealthyOwnedDeployable();
        }

        private bool TryBuildPredictiveProjectilePlan(
            AbilityDefinition ability,
            ISpatialEntity target,
            float fallbackRange,
            uint currentTick,
            bool requireQualityGate,
            string reason,
            out AIAbilityCastPlan plan)
        {
            plan = default;

            if (!SpatialEntityUtility.IsAlive(target))
                return false;

            if (!AIPredictiveCombatUtility.TryGetProjectileKinematics(
                    ability,
                    fallbackRange,
                    out float range,
                    out float projectileSpeed))
            {
                return false;
            }

            if (!IsWithinRange(target.Position, range))
                return false;

            Vector3 targetVelocity = EstimateTargetVelocity(target, currentTick);
            bool targetControlled = IsTargetControlled(target);
            int availableAmmo = GetAvailableAmmo();

            AIPredictiveShotResult preview =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    _self.Position,
                    target.Position,
                    targetVelocity,
                    range,
                    projectileSpeed,
                    availableAmmo,
                    targetControlled,
                    enemyCountInLane: 1,
                    allyCountInLane: 0);

            CountFireLaneEntities(
                target,
                preview.AimPoint,
                range,
                FireLaneWidth,
                out int enemiesInLane,
                out int alliesInLane);

            AIPredictiveShotResult result =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    _self.Position,
                    target.Position,
                    targetVelocity,
                    range,
                    projectileSpeed,
                    availableAmmo,
                    targetControlled,
                    enemiesInLane,
                    alliesInLane);

            if (requireQualityGate && !result.ShouldFire)
                return false;

            Vector3 direction = ability is BurstSequenceProjectileAbilityDefinition
                ? BuildLinePressureDirection(result.AimPoint, range)
                : result.AimPoint - _self.Position;
            direction.y = 0f;

            plan = AIAbilityCastPlan.Directional(
                target,
                direction,
                $"{reason}:{result.Reason}:q={result.Quality:0.00}");
            return true;
        }

        private Vector3 GetPredictedTargetPoint(
            AbilityDefinition ability,
            ISpatialEntity target,
            float fallbackRange,
            uint currentTick)
        {
            if (!SpatialEntityUtility.IsAlive(target) ||
                !AIPredictiveCombatUtility.TryGetProjectileKinematics(
                    ability,
                    fallbackRange,
                    out float range,
                    out float projectileSpeed))
            {
                return target != null ? target.Position : _self.Position;
            }

            Vector3 targetVelocity = EstimateTargetVelocity(target, currentTick);
            AIPredictiveShotResult result =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    _self.Position,
                    target.Position,
                    targetVelocity,
                    range,
                    projectileSpeed,
                    availableAmmo: 3,
                    targetControlled: IsTargetControlled(target),
                    enemyCountInLane: 1,
                    allyCountInLane: 0);

            return result.AimPoint;
        }

        private AIAbilityCastPlan BuildDirectionalPlan(ISpatialEntity target, string reason)
        {
            Vector3 direction = target.Position - _self.Position;
            direction.y = 0f;
            return AIAbilityCastPlan.Directional(target, direction, reason);
        }

        private bool TryFindHybridHealTarget(
            float healAmount,
            float range,
            bool includeSelf,
            out BrawlerController target)
        {
            target = null;

            if (SimulationClock.Grid == null || _self == null)
                return false;

            _buffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, range, _buffer);

            float bestScore = float.MinValue;

            if (includeSelf)
                ScoreHealCandidate(_self, healAmount, ref target, ref bestScore);

            for (int i = 0; i < _buffer.Count; i++)
            {
                if (_buffer[i] is not BrawlerController ally)
                    continue;

                if (ally.EntityID == _self.EntityID)
                    continue;

                if (ally.Team != _self.Team)
                    continue;

                ScoreHealCandidate(ally, healAmount, ref target, ref bestScore);
            }

            return target != null;
        }

        private void ScoreHealCandidate(
            BrawlerController candidate,
            float healAmount,
            ref BrawlerController best,
            ref float bestScore)
        {
            if (candidate == null || candidate.State == null || candidate.State.IsDead)
                return;

            float maxHealth = Mathf.Max(1f, candidate.State.MaxHealth.Value);
            float missingHealth = maxHealth - candidate.State.CurrentHealth;
            float healthRatio = candidate.State.CurrentHealth / maxHealth;

            if (healthRatio > HybridHealHealthThreshold && missingHealth < healAmount * 0.75f)
                return;

            float distance = Vector3.Distance(_self.Position, candidate.Position);
            float score =
                ((1f - healthRatio) * 100f) +
                Mathf.Clamp(missingHealth / Mathf.Max(1f, healAmount), 0f, 3f) * 18f -
                distance * 1.25f +
                candidate.State.CarriedGemCount * 6f;

            if (healthRatio <= HybridEmergencyHealthThreshold)
                score += 35f;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        private bool ShouldPrioritizeHybridHeal(BrawlerController ally, ISpatialEntity requestedTarget)
        {
            if (ally == null || ally.State == null)
                return false;

            float allyRatio = ally.State.CurrentHealth / Mathf.Max(1f, ally.State.MaxHealth.Value);
            if (allyRatio <= HybridEmergencyHealthThreshold)
                return true;

            if (!SpatialEntityUtility.IsAlive(requestedTarget) ||
                requestedTarget is not BrawlerController enemy ||
                enemy.State == null)
                return true;

            float enemyRatio = enemy.State.CurrentHealth / Mathf.Max(1f, enemy.State.MaxHealth.Value);
            if (enemyRatio <= 0.25f)
                return false;

            return allyRatio <= HybridHealHealthThreshold;
        }

        private Vector3 BuildAreaDenialPoint(Vector3 targetPosition, float range, float clusterRadius)
        {
            Vector3 desired = targetPosition;

            if (SimulationClock.Grid != null)
            {
                _buffer.Clear();
                SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(targetPosition, clusterRadius, _buffer);

                Vector3 sum = Vector3.zero;
                int count = 0;

                for (int i = 0; i < _buffer.Count; i++)
                {
                    ISpatialEntity entity = _buffer[i];
                    if (!IsLiveEnemy(entity))
                        continue;

                    sum += entity.Position;
                    count++;
                }

                if (count > 1)
                    desired = sum / count;
            }

            Vector3 awayFromSelf = desired - _self.Position;
            awayFromSelf.y = 0f;

            if (awayFromSelf.sqrMagnitude > 0.001f)
                desired += awayFromSelf.normalized * 0.6f;

            return ClampPointToRange(desired, range);
        }

        private Vector3 BuildDeployablePressurePoint(Vector3 targetPosition, float range)
        {
            Vector3 toTarget = targetPosition - _self.Position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.001f)
                return _self.Position;

            Vector3 direction = toTarget.normalized;
            float placementDistance = Mathf.Min(range, Mathf.Max(2.5f, toTarget.magnitude * 0.65f));
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            float sideSign = (_self.EntityID & 1) == 0 ? 1f : -1f;

            return ClampPointToRange(
                _self.Position + direction * placementDistance + side * sideSign * 0.75f,
                range);
        }

        private Vector3 BuildLinePressureDirection(Vector3 primaryTargetPosition, float range)
        {
            Vector3 baseDirection = primaryTargetPosition - _self.Position;
            baseDirection.y = 0f;

            if (baseDirection.sqrMagnitude <= 0.001f)
                return _self.transform.forward;

            baseDirection.Normalize();

            if (SimulationClock.Grid == null)
                return baseDirection;

            _buffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, range, _buffer);

            Vector3 pressureSum = primaryTargetPosition;
            int pressureCount = 1;

            for (int i = 0; i < _buffer.Count; i++)
            {
                ISpatialEntity entity = _buffer[i];
                if (!IsLiveEnemy(entity))
                    continue;

                Vector3 toEntity = entity.Position - _self.Position;
                toEntity.y = 0f;

                float forwardDistance = Vector3.Dot(toEntity, baseDirection);
                if (forwardDistance <= 0f || forwardDistance > range)
                    continue;

                Vector3 closestPoint = _self.Position + baseDirection * forwardDistance;
                float lateralDistance = Vector3.Distance(closestPoint, entity.Position);

                if (lateralDistance > 1.5f)
                    continue;

                pressureSum += entity.Position;
                pressureCount++;
            }

            if (pressureCount <= 1)
                return baseDirection;

            Vector3 pressureDirection = (pressureSum / pressureCount) - _self.Position;
            pressureDirection.y = 0f;

            return pressureDirection.sqrMagnitude > 0.001f
                ? pressureDirection.normalized
                : baseDirection;
        }

        private Vector3 EstimateTargetVelocity(ISpatialEntity target, uint currentTick)
        {
            if (target == null || target.EntityID == 0)
                return Vector3.zero;

            Vector3 currentPosition = target.Position;
            int targetId = target.EntityID;
            Vector3 velocity = Vector3.zero;

            if (_targetMotion.TryGetValue(targetId, out TargetMotionRecord previous) &&
                currentTick >= previous.Tick)
            {
                uint tickDelta = currentTick - previous.Tick;
                if (tickDelta > 0 && tickDelta <= TargetMotionForgetTicks)
                {
                    float seconds = tickDelta * SimulationClock.TickDeltaTime;
                    velocity = (currentPosition - previous.Position) / Mathf.Max(0.001f, seconds);
                    velocity.y = 0f;
                }
            }

            _targetMotion[targetId] = new TargetMotionRecord
            {
                Position = currentPosition,
                Tick = currentTick
            };

            return ClampTargetVelocity(target, velocity);
        }

        private Vector3 ClampTargetVelocity(ISpatialEntity target, Vector3 velocity)
        {
            if (target is not BrawlerController targetBrawler || targetBrawler.State == null)
                return velocity;

            if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                return Vector3.zero;

            if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                velocity *= 0.35f;

            float maxSpeed = Mathf.Max(1f, targetBrawler.State.MoveSpeed.Value * 1.75f);
            if (velocity.magnitude > maxSpeed)
                velocity = velocity.normalized * maxSpeed;

            return velocity;
        }

        private bool IsTargetControlled(ISpatialEntity target)
        {
            return target is BrawlerController targetBrawler &&
                   targetBrawler.State != null &&
                   (targetBrawler.State.HasStatus(StatusEffectType.Stun) ||
                    targetBrawler.State.HasStatus(StatusEffectType.Slow));
        }

        private int GetAvailableAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.AvailableBars
                : 3;
        }

        private void CountFireLaneEntities(
            ISpatialEntity primaryTarget,
            Vector3 aimPoint,
            float range,
            float width,
            out int enemiesInLane,
            out int alliesInLane)
        {
            enemiesInLane = 0;
            alliesInLane = 0;

            Vector3 direction = aimPoint - _self.Position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                enemiesInLane = 1;
                return;
            }

            direction.Normalize();

            if (SimulationClock.Grid == null)
            {
                enemiesInLane = 1;
                return;
            }

            _buffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, range, _buffer);

            for (int i = 0; i < _buffer.Count; i++)
            {
                ISpatialEntity entity = _buffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) || entity.EntityID == _self.EntityID)
                    continue;

                Vector3 toEntity = entity.Position - _self.Position;
                toEntity.y = 0f;

                float forwardDistance = Vector3.Dot(toEntity, direction);
                if (forwardDistance <= 0f || forwardDistance > range)
                    continue;

                Vector3 closestPoint = _self.Position + direction * forwardDistance;
                if (Vector3.Distance(closestPoint, entity.Position) > width)
                    continue;

                if (entity.Team == _self.Team)
                {
                    alliesInLane++;
                }
                else
                {
                    enemiesInLane++;
                }
            }

            if (enemiesInLane == 0 && primaryTarget != null)
                enemiesInLane = 1;
        }

        private bool HasHealthyOwnedDeployable()
        {
            if (!ServiceProvider.TryGet<IDeployableRegistry>(out var registry) || registry == null)
                return false;

            if (!registry.TryGetMostWoundedOwnedDeployable(_self, out DeployableController deployable))
                return false;

            if (deployable.State == null)
                return false;

            float healthRatio = deployable.State.CurrentHealth / Mathf.Max(1f, deployable.State.MaxHealth);
            return healthRatio > HealthyDeployableKeepThreshold;
        }

        private bool IsLiveEnemy(ISpatialEntity entity)
        {
            if (!SpatialEntityUtility.IsAlive(entity) || entity.Team == _self.Team)
                return false;

            if (entity is BrawlerController brawler && (brawler.State == null || brawler.State.IsDead))
                return false;

            return true;
        }

        private bool IsWithinRange(Vector3 point, float range)
        {
            return (point - _self.Position).sqrMagnitude <= range * range;
        }

        private Vector3 ClampPointToRange(Vector3 point, float range)
        {
            Vector3 offset = point - _self.Position;
            offset.y = 0f;

            if (offset.sqrMagnitude <= range * range)
                return point;

            return _self.Position + offset.normalized * range;
        }

        private float GetAbilityRange(AbilityDefinition ability, float fallbackRange)
        {
            switch (ability)
            {
                case BasicProjectileAttackDefinition basic:
                    return basic.Range;
                case BasicSuperDefinition basicSuper:
                    return basicSuper.Range;
                case BurstSequenceProjectileAbilityDefinition burst:
                    return burst.Range;
                case ProjectileAbilityDefinition projectile:
                    return projectile.Range;
                case ChainProjectileAbilityDefinition chain:
                    return chain.Range;
                case HybridProjectileAbilityDefinition hybrid:
                    return hybrid.Range;
                case ThrownHybridAoEAbilityDefinition thrown:
                    return thrown.ThrowRange;
                case ThrownVolleyAoEAbilityDefinition volley:
                    return volley.ThrowRange;
                case EffectAbilityDefinition effect:
                    return effect.PreviewRange;
                default:
                    return Mathf.Max(1f, fallbackRange);
            }
        }
    }
}
