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
        private const uint ComboWindowTicks = 36;
        private const float DeployableProtectionRadius = 5.5f;
        private const float WoundedDeployableProtectThreshold = 0.72f;
        private const uint TargetMotionForgetTicks = 90;

        private readonly BrawlerController _self;
        private readonly List<ISpatialEntity> _buffer = new List<ISpatialEntity>(24);
        private readonly List<ISpatialEntity> _scratchBuffer = new List<ISpatialEntity>(24);
        private readonly Dictionary<int, TargetMotionRecord> _targetMotion =
            new Dictionary<int, TargetMotionRecord>(16);
        private readonly Dictionary<int, TargetSynergyRecord> _targetSynergy =
            new Dictionary<int, TargetSynergyRecord>(16);
        private int _areaLayerIndex;

        private struct TargetMotionRecord
        {
            public Vector3 Position;
            public uint Tick;
        }

        private struct TargetSynergyRecord
        {
            public uint LastSetupTick;
            public Vector3 LastAimPoint;
            public int EnemyPressureCount;
            public int AllyRiskCount;
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

            AbilityDefinition ability = GetCurrentMainAttackDefinition();
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
                    currentTick,
                    out Vector3 targetVelocity);

                Vector3 point = BuildLayeredAreaDenialPoint(
                    targetPoint,
                    targetVelocity,
                    thrown.ThrowRange,
                    Mathf.Max(2.5f, thrown.ImpactRadius * 1.25f),
                    thrown.ImpactRadius,
                    out int enemyPressure);

                int allyRisk = CountAlliesNear(point, thrown.ImpactRadius);
                AIAmmoDisciplineDecision microDecision = EvaluateMainAttackMicro(
                    requestedTarget,
                    Mathf.Clamp01(0.46f + enemyPressure * 0.14f),
                    enemyPressure,
                    allyRisk,
                    isAreaDenial: true);
                if (microDecision.ShouldHoldFire)
                    return false;

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "thrown_area_denial");
                AppendAreaDenialDecision(
                    requestedTarget,
                    enemyPressure,
                    allyRisk,
                    thrown.LingeringHazard != null,
                    isSuper: false,
                    ref plan);
                AppendMicroDecision(microDecision, ref plan);
                RecordComboSetup(requestedTarget, currentTick, point, enemyPressure, allyRisk);
                return true;
            }

            if (ability is ThrownVolleyAoEAbilityDefinition volley)
            {
                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    volley.ThrowRange,
                    currentTick,
                    out Vector3 targetVelocity);

                Vector3 point = BuildLayeredAreaDenialPoint(
                    targetPoint,
                    targetVelocity,
                    volley.ThrowRange,
                    Mathf.Max(3.5f, volley.ImpactRadius * 1.5f),
                    volley.ImpactRadius,
                    out int enemyPressure);

                int allyRisk = CountAlliesNear(point, volley.ImpactRadius);
                AIAmmoDisciplineDecision microDecision = EvaluateMainAttackMicro(
                    requestedTarget,
                    Mathf.Clamp01(0.44f + enemyPressure * 0.16f),
                    enemyPressure,
                    allyRisk,
                    isAreaDenial: true);
                if (microDecision.ShouldHoldFire)
                    return false;

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "volley_area_denial");
                AppendAreaDenialDecision(
                    requestedTarget,
                    enemyPressure,
                    allyRisk,
                    volley.LingeringHazard != null,
                    isSuper: false,
                    ref plan);
                AppendMicroDecision(microDecision, ref plan);
                RecordComboSetup(requestedTarget, currentTick, point, enemyPressure, allyRisk);
                return true;
            }

            if (ability is ChainProjectileAbilityDefinition chain &&
                TryBuildChainBouncePlan(
                    chain,
                    requestedTarget,
                    abilityRange,
                    currentTick,
                    out plan))
            {
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
            AIAmmoDisciplineDecision defaultMicroDecision = EvaluateMainAttackMicro(
                requestedTarget,
                EstimateDirectShotQuality(requestedTarget, abilityRange),
                enemyCountInLane: 1,
                allyCountInLane: 0,
                isAreaDenial: false);
            if (defaultMicroDecision.ShouldHoldFire)
                return false;

            AppendMicroDecision(defaultMicroDecision, ref plan);
            RecordComboSetup(
                requestedTarget,
                currentTick,
                _self.Position + plan.Direction * abilityRange,
                1,
                0);
            return true;
        }

        public bool TryBuildSuperPlan(
            ISpatialEntity requestedTarget,
            float superRange,
            uint currentTick,
            out AIAbilityCastPlan plan)
        {
            plan = default;

            AbilityDefinition ability = GetCurrentSuperDefinition();
            if (ability == null)
                return false;

            float abilityRange = GetAbilityRange(ability, superRange);

            if (ability is EffectAbilityDefinition effect &&
                effect.TargetingType == AbilityTargetingType.Area)
            {
                bool hasOwnedDeployable = TryGetMostWoundedOwnedDeployable(
                    out DeployableController deployable,
                    out float deployableHealthRatio);

                if (hasOwnedDeployable &&
                    deployableHealthRatio > HealthyDeployableKeepThreshold &&
                    !IsOwnedDeployableThreatened(deployable, requestedTarget))
                {
                    return false;
                }

                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                Vector3 point = hasOwnedDeployable &&
                                deployableHealthRatio <= WoundedDeployableProtectThreshold
                    ? BuildDeployableProtectionPoint(deployable, requestedTarget.Position, abilityRange)
                    : BuildDeployablePressurePoint(requestedTarget.Position, abilityRange);

                plan = AIAbilityCastPlan.PointTarget(
                    requestedTarget,
                    _self.Position,
                    point,
                    hasOwnedDeployable && deployableHealthRatio <= WoundedDeployableProtectThreshold
                        ? "deployable_protection"
                        : "deployable_pressure");
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
                    currentTick,
                    out Vector3 targetVelocity);

                Vector3 point = BuildLayeredAreaDenialPoint(
                    targetPoint,
                    targetVelocity,
                    hybridAoE.ThrowRange,
                    Mathf.Max(3f, hybridAoE.ImpactRadius * 0.75f),
                    hybridAoE.ImpactRadius,
                    out int enemyPressure);

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "hybrid_super_damage");
                AppendAreaDenialDecision(
                    requestedTarget,
                    enemyPressure,
                    CountAlliesNear(point, hybridAoE.ImpactRadius),
                    hybridAoE.LingeringHazard != null,
                    isSuper: true,
                    ref plan);
                ApplyComboWindow(requestedTarget, currentTick, ref plan);
                return true;
            }

            if (ability is MinefieldAbilityDefinition minefield)
            {
                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    minefield.Range,
                    currentTick,
                    out Vector3 targetVelocity);

                float impactRadius = Mathf.Max(1f, minefield.ExplosionRadius);
                Vector3 point = BuildLayeredAreaDenialPoint(
                    targetPoint,
                    targetVelocity,
                    minefield.Range,
                    Mathf.Max(3.25f, impactRadius + minefield.MineSpacing * Mathf.Max(1, minefield.MineCount - 1)),
                    impactRadius,
                    out int enemyPressure);

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "minefield_area_control");
                AppendAreaDenialDecision(
                    requestedTarget,
                    enemyPressure,
                    CountAlliesNear(point, impactRadius),
                    true,
                    true,
                    ref plan);

                if (enemyPressure >= 2 || IsHighValueCarrier(requestedTarget))
                    plan.ForceUse = true;

                ApplyComboWindow(requestedTarget, currentTick, ref plan);
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
                    currentTick,
                    out Vector3 targetVelocity);

                Vector3 point = BuildLayeredAreaDenialPoint(
                    targetPoint,
                    targetVelocity,
                    volley.ThrowRange,
                    Mathf.Max(4f, volley.ImpactRadius * 1.5f),
                    volley.ImpactRadius,
                    out int enemyPressure);

                plan = AIAbilityCastPlan.PointTarget(requestedTarget, _self.Position, point, "volley_super_denial");
                AppendAreaDenialDecision(
                    requestedTarget,
                    enemyPressure,
                    CountAlliesNear(point, volley.ImpactRadius),
                    volley.LingeringHazard != null,
                    isSuper: true,
                    ref plan);
                ApplyComboWindow(requestedTarget, currentTick, ref plan);
                return true;
            }

            if (ability is LeapAbilityDefinition leap)
            {
                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                Vector3 targetPoint = GetPredictedTargetPoint(
                    ability,
                    requestedTarget,
                    leap.Range,
                    currentTick,
                    out _);
                targetPoint = ClampPointToRange(targetPoint, leap.Range);

                plan = AIAbilityCastPlan.PointTarget(
                    requestedTarget,
                    _self.Position,
                    targetPoint,
                    "leap_engage");
                ApplyComboWindow(requestedTarget, currentTick, ref plan);
                return true;
            }

            if (ability is BurstSequenceProjectileAbilityDefinition)
            {
                if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                    return false;

                bool builtPlan = TryBuildPredictiveProjectilePlan(
                    ability,
                    requestedTarget,
                    abilityRange,
                    currentTick,
                    requireQualityGate: false,
                    "super_line_pressure",
                    out plan);

                if (builtPlan)
                    ApplyComboWindow(requestedTarget, currentTick, ref plan);

                return builtPlan;
            }

            if (!SpatialEntityUtility.IsAlive(requestedTarget) || !IsWithinRange(requestedTarget.Position, abilityRange))
                return false;

            if (AIPredictiveCombatUtility.TryGetProjectileKinematics(
                    ability,
                    abilityRange,
                    out _,
                    out _))
            {
                bool builtPlan = TryBuildPredictiveProjectilePlan(
                    ability,
                    requestedTarget,
                    abilityRange,
                    currentTick,
                    requireQualityGate: false,
                    "predictive_super",
                    out plan);

                if (builtPlan)
                    ApplyComboWindow(requestedTarget, currentTick, ref plan);

                return builtPlan;
            }

            plan = BuildDirectionalPlan(requestedTarget, "default_super");
            ApplyComboWindow(requestedTarget, currentTick, ref plan);
            return true;
        }

        public bool ShouldHoldSuperForSpecialistValue()
        {
            AbilityDefinition ability = GetCurrentSuperDefinition();

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

            AIAmmoDisciplineDecision microDecision = default;
            if (ability.SlotType == AbilitySlotType.MainAttack)
            {
                microDecision = EvaluateMainAttackMicro(
                    target,
                    result.Quality,
                    enemiesInLane,
                    alliesInLane,
                    isAreaDenial: false);

                if (microDecision.ShouldHoldFire)
                    return false;
            }

            Vector3 direction = ability is BurstSequenceProjectileAbilityDefinition
                ? BuildLinePressureDirection(result.AimPoint, range)
                : result.AimPoint - _self.Position;
            direction.y = 0f;

            plan = AIAbilityCastPlan.Directional(
                target,
                direction,
                $"{reason}:{result.Reason}:q={result.Quality:0.00}");

            AIBrawlerPackDecision lineDecision =
                AIBrawlerIntelligencePackUtility.EvaluateLinePressureCommit(
                    enemiesInLane,
                    alliesInLane,
                    GetTargetHealthRatio(target),
                    targetControlled,
                    availableAmmo,
                    IsSuperAbility(ability));

            AppendPackDecision(lineDecision, ref plan);

            if (ability.SlotType == AbilitySlotType.MainAttack)
                AppendMicroDecision(microDecision, ref plan);

            if (ability.SlotType == AbilitySlotType.MainAttack)
                RecordComboSetup(target, currentTick, result.AimPoint, enemiesInLane, alliesInLane);

            return true;
        }

        private Vector3 GetPredictedTargetPoint(
            AbilityDefinition ability,
            ISpatialEntity target,
            float fallbackRange,
            uint currentTick,
            out Vector3 targetVelocity)
        {
            targetVelocity = Vector3.zero;

            if (!SpatialEntityUtility.IsAlive(target) ||
                !AIPredictiveCombatUtility.TryGetProjectileKinematics(
                    ability,
                    fallbackRange,
                    out float range,
                    out float projectileSpeed))
            {
                return target != null ? target.Position : _self.Position;
            }

            targetVelocity = EstimateTargetVelocity(target, currentTick);
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

        private bool TryBuildChainBouncePlan(
            ChainProjectileAbilityDefinition chain,
            ISpatialEntity requestedTarget,
            float abilityRange,
            uint currentTick,
            out AIAbilityCastPlan plan)
        {
            plan = default;

            if (chain == null || !SpatialEntityUtility.IsAlive(requestedTarget))
                return false;

            float range = Mathf.Max(1f, abilityRange);
            if (!IsWithinRange(requestedTarget.Position, range))
                return false;

            ISpatialEntity bestTarget = requestedTarget;
            int bestBounceTargets = CountEnemiesNear(
                requestedTarget.Position,
                chain.BounceRadius,
                includePrimaryTarget: true);
            float bestScore = ScoreChainBounceAnchor(
                requestedTarget,
                bestBounceTargets,
                requestedTargetBonus: true);

            if (SimulationClock.Grid != null)
            {
                _buffer.Clear();
                SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, range, _buffer);

                for (int i = 0; i < _buffer.Count; i++)
                {
                    ISpatialEntity entity = _buffer[i];
                    if (!IsLiveEnemy(entity))
                        continue;

                    int bounceTargets = CountEnemiesNear(
                        entity.Position,
                        chain.BounceRadius,
                        includePrimaryTarget: true);
                    float score = ScoreChainBounceAnchor(
                        entity,
                        bounceTargets,
                        SpatialEntityUtility.IsSameEntity(entity, requestedTarget));

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = entity;
                        bestBounceTargets = bounceTargets;
                    }
                }
            }

            Vector3 aimPoint = GetPredictedTargetPoint(
                chain,
                bestTarget,
                range,
                currentTick,
                out _);

            AIAmmoDisciplineDecision microDecision = EvaluateMainAttackMicro(
                bestTarget,
                Mathf.Clamp01(0.42f + bestBounceTargets * 0.18f),
                bestBounceTargets,
                CountAlliesNear(bestTarget.Position, chain.BounceRadius),
                isAreaDenial: false);
            if (microDecision.ShouldHoldFire)
                return false;

            plan = AIAbilityCastPlan.Directional(
                bestTarget,
                aimPoint - _self.Position,
                $"chain_bounce targets={bestBounceTargets}");

            AppendMicroDecision(microDecision, ref plan);
            RecordComboSetup(bestTarget, currentTick, aimPoint, bestBounceTargets, 0);
            return true;
        }

        private float ScoreChainBounceAnchor(
            ISpatialEntity entity,
            int bounceTargets,
            bool requestedTargetBonus)
        {
            return AIBrawlerIntelligencePackUtility.ScoreChainBounceAnchor(
                bounceTargets,
                requestedTargetBonus,
                GetTargetHealthRatio(entity),
                IsTargetControlled(entity));
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
            int nearbyEnemies = CountEnemiesNear(
                candidate.Position,
                DeployableProtectionRadius,
                includePrimaryTarget: false);
            float score =
                ((1f - healthRatio) * 100f) +
                Mathf.Clamp(missingHealth / Mathf.Max(1f, healAmount), 0f, 3f) * 18f -
                distance * 1.25f +
                candidate.State.CarriedGemCount * 6f +
                Mathf.Min(24f, nearbyEnemies * 8f);

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

        private int CountEnemiesNear(
            Vector3 position,
            float radius,
            bool includePrimaryTarget)
        {
            if (SimulationClock.Grid == null)
                return includePrimaryTarget ? 1 : 0;

            _scratchBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(position, radius, _scratchBuffer);

            int count = 0;
            for (int i = 0; i < _scratchBuffer.Count; i++)
            {
                if (IsLiveEnemy(_scratchBuffer[i]))
                    count++;
            }

            return includePrimaryTarget ? Mathf.Max(1, count) : count;
        }

        private int CountAlliesNear(Vector3 position, float radius)
        {
            if (SimulationClock.Grid == null)
                return 0;

            _scratchBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(position, radius, _scratchBuffer);

            int count = 0;
            for (int i = 0; i < _scratchBuffer.Count; i++)
            {
                ISpatialEntity entity = _scratchBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.EntityID == _self.EntityID ||
                    entity.Team != _self.Team)
                {
                    continue;
                }

                if (entity is BrawlerController brawler && (brawler.State == null || brawler.State.IsDead))
                    continue;

                count++;
            }

            return count;
        }

        private static bool IsHighValueCarrier(ISpatialEntity target)
        {
            return target is BrawlerController brawler &&
                   brawler.State != null &&
                   brawler.State.CarriedGemCount >= 2;
        }

        private void RecordComboSetup(
            ISpatialEntity target,
            uint currentTick,
            Vector3 aimPoint,
            int enemyPressureCount,
            int allyRiskCount)
        {
            if (!SpatialEntityUtility.TryGetEntityId(target, out int targetId))
                return;

            _targetSynergy[targetId] = new TargetSynergyRecord
            {
                LastSetupTick = currentTick,
                LastAimPoint = aimPoint,
                EnemyPressureCount = Mathf.Max(1, enemyPressureCount),
                AllyRiskCount = Mathf.Max(0, allyRiskCount)
            };
        }

        private void ApplyComboWindow(
            ISpatialEntity target,
            uint currentTick,
            ref AIAbilityCastPlan plan)
        {
            if (!SpatialEntityUtility.TryGetEntityId(target, out int targetId))
                return;

            if (!_targetSynergy.TryGetValue(targetId, out TargetSynergyRecord record))
                return;

            AIComboWindowResult combo = AIAbilitySynergyUtility.EvaluateComboWindow(
                hasSetup: true,
                currentTick: currentTick,
                setupTick: record.LastSetupTick,
                windowTicks: ComboWindowTicks,
                targetHealthRatio: GetTargetHealthRatio(target),
                targetControlled: IsTargetControlled(target),
                enemyPressureCount: record.EnemyPressureCount,
                allyRiskCount: record.AllyRiskCount);

            if (!combo.IsActive)
                return;

            if (combo.ShouldCommit)
                plan.ForceUse = true;

            plan.Reason = $"{plan.Reason}|combo={combo.Reason}:{combo.Score:0}";

            if (plan.HasTargetPoint)
            {
                Vector3 blendedPoint = Vector3.Lerp(plan.TargetPoint, record.LastAimPoint, 0.25f);
                plan.TargetPoint = ClampPointToRange(blendedPoint, GetAbilityRange(GetCurrentSuperDefinition(), 6f));
                Vector3 direction = plan.TargetPoint - _self.Position;
                direction.y = 0f;
                plan.Direction = direction.sqrMagnitude > 0.001f
                    ? direction.normalized
                    : plan.Direction;
            }
        }

        private void AppendAreaDenialDecision(
            ISpatialEntity target,
            int enemyPressureCount,
            int allyRiskCount,
            bool hasLingeringHazard,
            bool isSuper,
            ref AIAbilityCastPlan plan)
        {
            AIBrawlerPackDecision decision =
                AIBrawlerIntelligencePackUtility.EvaluateAreaDenialCommit(
                    enemyPressureCount,
                    allyRiskCount,
                    GetTargetHealthRatio(target),
                    IsTargetControlled(target),
                    hasLingeringHazard,
                    isSuper);

            AppendPackDecision(decision, ref plan);
        }

        private void AppendPackDecision(
            AIBrawlerPackDecision decision,
            ref AIAbilityCastPlan plan)
        {
            if (decision.ForceUse)
                plan.ForceUse = true;

            plan.Reason = $"{plan.Reason}|pack={decision.Reason}:{decision.Score:0}";
        }

        private AIAmmoDisciplineDecision EvaluateMainAttackMicro(
            ISpatialEntity target,
            float shotQuality,
            int enemyCountInLane,
            int allyCountInLane,
            bool isAreaDenial)
        {
            if (_self == null || _self.State == null || _self.State.Ammo == null)
                return new AIAmmoDisciplineDecision(false, "ammo_unknown");

            return AICombatMicroUtility.EvaluateAmmoDiscipline(
                GetAvailableAmmo(),
                GetMaxAmmo(),
                GetCurrentAmmo(),
                Mathf.Clamp01(shotQuality),
                GetTargetHealthRatio(target),
                IsTargetControlled(target),
                Mathf.Max(0, enemyCountInLane),
                Mathf.Max(0, allyCountInLane),
                isAreaDenial);
        }

        private void AppendMicroDecision(
            AIAmmoDisciplineDecision decision,
            ref AIAbilityCastPlan plan)
        {
            if (string.IsNullOrEmpty(decision.Reason))
                return;

            plan.Reason = $"{plan.Reason}|micro={decision.Reason}";
        }

        private float EstimateDirectShotQuality(ISpatialEntity target, float range)
        {
            if (!SpatialEntityUtility.IsAlive(target))
                return 0f;

            float distance = Vector3.Distance(_self.Position, target.Position);
            float rangeQuality = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, range)) * 0.35f;

            if (IsTargetControlled(target))
                rangeQuality += 0.20f;

            return Mathf.Clamp01(rangeQuality);
        }

        private float GetTargetHealthRatio(ISpatialEntity target)
        {
            if (target is not BrawlerController brawler || brawler.State == null)
                return 1f;

            return brawler.State.CurrentHealth / Mathf.Max(1f, brawler.State.MaxHealth.Value);
        }

        private Vector3 BuildLayeredAreaDenialPoint(
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float range,
            float clusterRadius,
            float impactRadius,
            out int enemyPressure)
        {
            Vector3 desired = targetPosition;
            enemyPressure = 1;

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

                if (count > 0)
                    enemyPressure = count;
            }

            Vector3 layeredPoint = AIAbilitySynergyUtility.ResolveLayeredAreaDenialPoint(
                _self.Position,
                desired,
                targetVelocity,
                impactRadius,
                range,
                enemyPressure,
                _areaLayerIndex++);

            return ClampPointToRange(layeredPoint, range);
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

        private Vector3 BuildDeployableProtectionPoint(
            DeployableController deployable,
            Vector3 threatPosition,
            float range)
        {
            if (!SpatialEntityUtility.IsAlive(deployable))
                return BuildDeployablePressurePoint(threatPosition, range);

            Vector3 deployablePosition = deployable.Position;
            Vector3 awayFromThreat = deployablePosition - threatPosition;
            awayFromThreat.y = 0f;

            if (awayFromThreat.sqrMagnitude <= 0.001f)
                awayFromThreat = deployablePosition - _self.Position;

            awayFromThreat.y = 0f;
            if (awayFromThreat.sqrMagnitude <= 0.001f)
                awayFromThreat = -_self.transform.forward;

            awayFromThreat.Normalize();

            Vector3 fromSelf = deployablePosition - _self.Position;
            fromSelf.y = 0f;
            Vector3 side = fromSelf.sqrMagnitude > 0.001f
                ? new Vector3(fromSelf.z, 0f, -fromSelf.x).normalized
                : _self.transform.right;

            float sideSign = (_self.EntityID & 1) == 0 ? 1f : -1f;
            Vector3 protectedPoint =
                deployablePosition +
                awayFromThreat * 1.4f +
                side * sideSign * 0.65f;

            return ClampPointToRange(protectedPoint, range);
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

            velocity = ClampTargetVelocity(target, velocity);

            if (_self != null &&
                AIOpponentModel.TryGetSnapshot(
                    _self.Team,
                    targetId,
                    currentTick,
                    360u,
                    out AIOpponentHabitSnapshot habit))
            {
                velocity = AIOpponentModel.ApplyDodgeHabitToVelocity(
                    _self.Position,
                    currentPosition,
                    velocity,
                    habit,
                    0.65f);
            }

            return velocity;
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

        private int GetMaxAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.MaxAmmo
                : 3;
        }

        private float GetCurrentAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.CurrentAmmo
                : GetAvailableAmmo();
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
            return TryGetMostWoundedOwnedDeployable(
                       out DeployableController deployable,
                       out float healthRatio) &&
                   healthRatio > HealthyDeployableKeepThreshold &&
                   !IsOwnedDeployableThreatened(deployable, null);
        }

        private bool TryGetMostWoundedOwnedDeployable(
            out DeployableController deployable,
            out float healthRatio)
        {
            deployable = null;
            healthRatio = 1f;

            if (!ServiceProvider.TryGet<IDeployableRegistry>(out var registry) || registry == null)
                return false;

            if (!registry.TryGetMostWoundedOwnedDeployable(_self, out deployable))
                return false;

            if (!SpatialEntityUtility.IsAlive(deployable) || deployable.State == null)
                return false;

            healthRatio = deployable.State.CurrentHealth / Mathf.Max(1f, deployable.State.MaxHealth);
            return true;
        }

        private bool IsOwnedDeployableThreatened(
            DeployableController deployable,
            ISpatialEntity requestedTarget)
        {
            if (!SpatialEntityUtility.IsAlive(deployable) || deployable.State == null)
                return false;

            int nearbyEnemyCount = CountEnemiesNear(
                deployable.Position,
                DeployableProtectionRadius,
                includePrimaryTarget: false);

            float threatDistance = DeployableProtectionRadius;
            if (SpatialEntityUtility.IsAlive(requestedTarget) &&
                requestedTarget.Team != _self.Team)
            {
                threatDistance = Vector3.Distance(deployable.Position, requestedTarget.Position);
                if (threatDistance <= DeployableProtectionRadius * 0.75f)
                    nearbyEnemyCount = Mathf.Max(nearbyEnemyCount, 1);
            }

            float healthRatio = deployable.State.CurrentHealth / Mathf.Max(1f, deployable.State.MaxHealth);
            float protectionScore = AIAbilitySynergyUtility.ScoreDeployableProtection(
                healthRatio,
                threatDistance,
                DeployableProtectionRadius,
                nearbyEnemyCount);

            return protectionScore >= 0.55f;
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

        private AbilityDefinition GetCurrentMainAttackDefinition()
        {
            return _self != null && _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self != null ? _self.Definition?.MainAttack : null;
        }

        private AbilityDefinition GetCurrentSuperDefinition()
        {
            return _self != null && _self.State != null
                ? _self.State.GetCurrentSuperDefinition()
                : _self != null ? _self.Definition?.SuperAbility : null;
        }

        private bool IsSuperAbility(AbilityDefinition ability)
        {
            if (ability == null)
                return false;

            AbilityDefinition currentSuper = GetCurrentSuperDefinition();
            return ability.SlotType == AbilitySlotType.Super || ability == currentSuper;
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
                case VolleyProjectileAbilityDefinition volleyProjectile:
                    return volleyProjectile.Range;
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
                case MeleeConeAbilityDefinition melee:
                    return melee.Range;
                case LeapAbilityDefinition leap:
                    return leap.Range;
                case MinefieldAbilityDefinition minefield:
                    return minefield.Range;
                case EffectAbilityDefinition effect:
                    return effect.PreviewRange;
                default:
                    return Mathf.Max(1f, fallbackRange);
            }
        }
    }
}
