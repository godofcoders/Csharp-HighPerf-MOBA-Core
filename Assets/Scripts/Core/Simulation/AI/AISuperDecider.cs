using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AISuperDecider
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AICommandSource _commandSource;
        private readonly AIAbilitySpecialistPlanner _specialistPlanner;
        private readonly List<ISpatialEntity> _clusterBuffer;
        private readonly AIFailureRecoveryMemory _failureRecovery;

        private uint _nextSuperDecisionTick;

        public AISuperDecider(
            BrawlerController self,
            BrawlerAIProfile profile,
            AICommandSource commandSource,
            AIFailureRecoveryMemory failureRecovery = null,
            int bufferCapacity = 16)
        {
            _self = self;
            _profile = profile;
            _commandSource = commandSource;
            _failureRecovery = failureRecovery;
            _specialistPlanner = new AIAbilitySpecialistPlanner(self);
            _clusterBuffer = new List<ISpatialEntity>(bufferCapacity);
        }

        public void TryUseSuper(ISpatialEntity target, uint currentTick, float superRange)
        {
            if (!_profile.EnableSuperUsage)
                return;

            if (!SpatialEntityUtility.IsAlive(target) || _self.State == null)
                return;

            if (!_self.State.SuperCharge.IsReady)
                return;

            if (currentTick < _nextSuperDecisionTick)
                return;

            if (_failureRecovery != null &&
                _failureRecovery.IsAbilitySuppressed(AbilitySlotType.Super, currentTick))
            {
                return;
            }

            AbilityDefinition super = GetCurrentSuperDefinition();
            if (super == null)
                return;

            if (_specialistPlanner.ShouldHoldSuperForSpecialistValue())
                return;

            AIAbilityCastPlan plan;
            bool hasSpecialistPlan = _specialistPlanner.TryBuildSuperPlan(
                target,
                superRange,
                currentTick,
                out plan);

            if (super is LeapAbilityDefinition && !hasSpecialistPlan)
                return;

            Vector3 aimPoint = hasSpecialistPlan && plan.HasTargetPoint
                ? plan.TargetPoint
                : target.Position;

            Vector3 toTarget = aimPoint - _self.Position;
            float distance = toTarget.magnitude;

            float minRange = Mathf.Max(1f, superRange * _profile.SuperMinRangeRatio);
            float maxRange = Mathf.Max(minRange, superRange * _profile.SuperMaxRangeMultiplier);

            if (distance > maxRange)
                return;

            if (distance < minRange && !(hasSpecialistPlan && plan.ForceUse))
                return;

            bool shouldUse = hasSpecialistPlan && plan.ForceUse
                ? true
                : ShouldUseSuperOnTarget(target, superRange);

            if (!shouldUse)
                return;

            if (!hasSpecialistPlan)
            {
                plan = AIAbilityCastPlan.Directional(
                    target,
                    toTarget,
                    "default_super");
            }

            bool preserveSpecialistLanding =
                super is LeapAbilityDefinition &&
                hasSpecialistPlan &&
                plan.ForceUse &&
                plan.HasTargetPoint;
            if (!preserveSpecialistLanding)
            {
                plan = AIAimAccuracyUtility.ApplyAimError(
                    plan,
                    _self.Position,
                    _profile.AimErrorDegrees);
            }

            if (!AIShotObstacleUtility.CanFireAtTarget(
                    _self,
                    super,
                    plan,
                    plan.Target,
                    superRange,
                    out _))
            {
                return;
            }

            if (_commandSource != null)
            {
                _commandSource.QueueSuper(
                    plan.Direction,
                    plan.TargetPoint,
                    plan.HasTargetPoint);
                AIValidationGauntlet.RecordSignal(
                    AIValidationGauntletSignal.SuperCast,
                    currentTick);
            }

            _nextSuperDecisionTick = currentTick + _profile.SuperDecisionCooldownTicks;
        }

        private bool ShouldUseSuperOnTarget(ISpatialEntity target, float superRange)
        {
            var super = GetCurrentSuperDefinition();
            if (super == null)
                return false;

            if (target is not BrawlerController targetBrawler || targetBrawler.State == null)
                return false;

            float healthRatio = targetBrawler.State.CurrentHealth /
                                Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);

            if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                return true;

            if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                return true;

            if (AIAbilityIntentUtility.IsFinisher(super) &&
                healthRatio <= _profile.SuperLowHealthTargetThreshold)
            {
                return true;
            }

            if (AIAbilityIntentUtility.IsEscape(super))
            {
                float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
                if (selfHealthRatio <= _profile.LowHealthRetreatRatio)
                    return true;

                if (_self.State.ThreatTracker != null)
                {
                    float threat = _self.State.ThreatTracker.GetThreat(
                        target.EntityID,
                        ServiceProvider.Get<ISimulationClock>().CurrentTick,
                        240);

                    if (threat > 0f)
                        return true;
                }
            }

            if (AIAbilityIntentUtility.IsEngage(super))
                return true;

            if (super is AoEAbilityDefinition aoe)
            {
                int clusterCount = CountEnemiesNear(target.Position, aoe.Radius * 1.15f);
                if (clusterCount >= _profile.SuperMinClusterCount)
                    return true;
            }

            if (super is ThrownVolleyAoEAbilityDefinition volley)
            {
                int clusterCount = CountEnemiesNear(
                    target.Position,
                    Mathf.Max(3.5f, volley.ImpactRadius * 1.6f));

                return clusterCount >= _profile.SuperMinClusterCount ||
                       healthRatio <= Mathf.Max(0.42f, _profile.SuperLowHealthTargetThreshold);
            }

            if (super is MinefieldAbilityDefinition minefield)
            {
                int clusterCount = CountEnemiesNear(
                    target.Position,
                    Mathf.Max(3.25f, minefield.ExplosionRadius + minefield.MineSpacing * Mathf.Max(1, minefield.MineCount - 1)));

                int carriedGems = targetBrawler.State.CarriedGemCount;
                return clusterCount >= _profile.SuperMinClusterCount ||
                       carriedGems >= 2 ||
                       healthRatio <= Mathf.Max(0.45f, _profile.SuperLowHealthTargetThreshold);
            }

            if (super is ThrownHybridAoEAbilityDefinition thrown)
            {
                int clusterCount = CountEnemiesNear(
                    target.Position,
                    Mathf.Max(3f, thrown.ImpactRadius));

                return clusterCount >= _profile.SuperMinClusterCount ||
                       healthRatio <= Mathf.Max(0.42f, _profile.SuperLowHealthTargetThreshold);
            }

            if (super is LeapAbilityDefinition)
            {
                float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
                return healthRatio <= Mathf.Max(0.45f, _profile.SuperLowHealthTargetThreshold) ||
                       selfHealthRatio >= _profile.LowHealthRetreatRatio;
            }

            if (super is BurstSequenceProjectileAbilityDefinition)
            {
                int laneCluster = CountEnemiesNear(target.Position, 2.75f);
                return laneCluster >= _profile.SuperMinClusterCount ||
                       healthRatio <= Mathf.Max(0.38f, _profile.SuperLowHealthTargetThreshold);
            }

            if (super is ProjectileAbilityDefinition)
                return true;

            return superRange >= 6f;
        }

        private AbilityDefinition GetCurrentSuperDefinition()
        {
            return _self != null && _self.State != null
                ? _self.State.GetCurrentSuperDefinition()
                : _self != null ? _self.Definition?.SuperAbility : null;
        }

        private int CountEnemiesNear(Vector3 position, float radius)
        {
            if (SimulationClock.Grid == null)
                return 0;

            _clusterBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(position, radius, _clusterBuffer);

            int count = 0;

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                ISpatialEntity entity = _clusterBuffer[i];

                if (!SpatialEntityUtility.IsAlive(entity) || entity.Team == _self.Team)
                    continue;

                if (entity is BrawlerController bc && (bc.State == null || bc.State.IsDead))
                    continue;

                count++;
            }

            return count;
        }
    }
}
