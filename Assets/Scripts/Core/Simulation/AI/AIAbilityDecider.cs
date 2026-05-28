using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIAbilityDecider
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AICommandSource _commandSource;
        private readonly AIAbilitySpecialistPlanner _specialistPlanner;
        private readonly AIFailureRecoveryMemory _failureRecovery;
        private readonly List<ISpatialEntity> _gadgetBuffer = new List<ISpatialEntity>(12);

        private uint _nextPrimaryAttackTick;
        private uint _nextGadgetTick;

        public AIAbilityDecider(
            BrawlerController self,
            BrawlerAIProfile profile,
            AICommandSource commandSource,
            AIFailureRecoveryMemory failureRecovery = null)
        {
            _self = self;
            _profile = profile;
            _commandSource = commandSource;
            _failureRecovery = failureRecovery;
            _specialistPlanner = new AIAbilitySpecialistPlanner(self);
        }

        public void TryUseMainAttack(ISpatialEntity target, uint currentTick, float maxRange)
        {
            if (!SpatialEntityUtility.IsAlive(target))
                return;

            if (currentTick < _nextPrimaryAttackTick)
                return;

            if (_failureRecovery != null &&
                _failureRecovery.IsAbilitySuppressed(AbilitySlotType.MainAttack, currentTick))
            {
                return;
            }

            if (!_specialistPlanner.TryBuildMainAttackPlan(
                    target,
                    maxRange,
                    currentTick,
                    out AIAbilityCastPlan plan))
            {
                return;
            }

            plan = AIAimAccuracyUtility.ApplyAimError(
                plan,
                _self.Position,
                _profile.AimErrorDegrees);

            if (_commandSource != null)
            {
                _commandSource.QueueMainAttack(
                    plan.Direction,
                    plan.TargetPoint,
                    plan.HasTargetPoint);
                AIValidationGauntlet.RecordSignal(
                    AIValidationGauntletSignal.MainAttackCast,
                    currentTick);
            }

            _nextPrimaryAttackTick = currentTick + _profile.AttackCadenceTicks;
        }

        public void TryUseGadget(ISpatialEntity target, uint currentTick)
        {
            if (!_profile.EnableGadgetUsage)
                return;

            if (!SpatialEntityUtility.IsAlive(target) || _self.State == null)
                return;

            if (_self.State.RemainingGadgets <= 0)
                return;

            if (currentTick < _nextGadgetTick)
                return;

            if (_failureRecovery != null &&
                _failureRecovery.IsAbilitySuppressed(AbilitySlotType.Gadget, currentTick))
            {
                return;
            }

            GadgetDefinition gadget = GetCurrentGadgetDefinition();
            if (gadget == null)
                return;

            float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            float distance = Vector3.Distance(_self.Position, target.Position);

            if (TryBuildGadgetSynergyDirection(
                    target,
                    gadget,
                    selfHealthRatio,
                    distance,
                    out Vector3 synergyDirection))
            {
                if (_commandSource != null)
                {
                    _commandSource.QueueGadget(synergyDirection);
                    AIValidationGauntlet.RecordSignal(
                        AIValidationGauntletSignal.GadgetCast,
                        currentTick);
                }

                _nextGadgetTick = currentTick + _profile.GadgetCooldownTicks;
                return;
            }

            if (gadget is DashGadgetDefinition)
                return;

            if (gadget is AmmoRefillGadgetDefinition ||
                gadget is SuperChargeGadgetDefinition)
            {
                return;
            }

            bool lowHealthEmergency = selfHealthRatio <= _profile.GadgetLowHealthThreshold;
            bool closeDanger = distance <= _profile.GadgetEnemyDistanceThreshold;

            if (!lowHealthEmergency && !closeDanger)
                return;

            Vector3 dir = (target.Position - _self.Position).normalized;
            if (_commandSource != null)
            {
                _commandSource.QueueGadget(dir);
                AIValidationGauntlet.RecordSignal(
                    AIValidationGauntletSignal.GadgetCast,
                    currentTick);
            }

            _nextGadgetTick = currentTick + _profile.GadgetCooldownTicks;
        }

        public void Reset()
        {
            _nextPrimaryAttackTick = 0;
            _nextGadgetTick = 0;
        }

        private bool TryBuildGadgetSynergyDirection(
            ISpatialEntity target,
            GadgetDefinition gadget,
            float selfHealthRatio,
            float targetDistance,
            out Vector3 direction)
        {
            direction = Vector3.zero;

            if (gadget == null)
                return false;

            if (gadget is AllyHealPulseGadgetDefinition allyHeal &&
                TryFindAllyHealPulseTarget(allyHeal, out BrawlerController allyToHeal))
            {
                direction = ResolveDirectionTo(allyToHeal.Position);
                return true;
            }

            if (gadget is DashGadgetDefinition &&
                SpatialEntityUtility.IsAlive(target))
            {
                AIBrawlerPackDecision dashDecision =
                    AIBrawlerIntelligencePackUtility.EvaluateDashGadget(
                        selfHealthRatio,
                        targetDistance,
                        _profile.GadgetEnemyDistanceThreshold,
                        GetTargetHealthRatio(target),
                        IsTargetControlled(target),
                        selfHealthRatio <= _profile.GadgetLowHealthThreshold ||
                        targetDistance <= _profile.GadgetEnemyDistanceThreshold);

                if (dashDecision.ShouldUse)
                {
                    direction = dashDecision.Reason == "dash_escape"
                        ? ResolveDirectionAwayFrom(target.Position)
                        : ResolveDirectionTo(target.Position);
                    return true;
                }
            }

            if (gadget is AmmoRefillGadgetDefinition &&
                _self.State.Ammo != null &&
                SpatialEntityUtility.IsAlive(target))
            {
                float mainAttackRange = GetMainAttackRange();
                AIBrawlerPackDecision ammoDecision =
                    AIBrawlerIntelligencePackUtility.EvaluateAmmoRefillGadget(
                        _self.State.Ammo.AvailableBars,
                        _self.State.Ammo.MaxAmmo,
                        targetDistance,
                        mainAttackRange,
                        GetTargetHealthRatio(target),
                        IsTargetControlled(target),
                        CountEnemiesNear(target.Position, 2.25f));

                if (ammoDecision.ShouldUse)
                {
                    direction = ResolveDirectionTo(target.Position);
                    return true;
                }
            }

            if (gadget is SuperChargeGadgetDefinition superCharge &&
                SpatialEntityUtility.IsAlive(target))
            {
                float superRange = GetSuperRange() * _profile.SuperMaxRangeMultiplier;
                AIBrawlerPackDecision chargeDecision =
                    AIBrawlerIntelligencePackUtility.EvaluateSuperChargeGadget(
                        _self.State.SuperCharge.ChargePercent,
                        superCharge.ChargeFraction,
                        _self.State.SuperCharge.IsReady,
                        targetDistance,
                        superRange,
                        CountEnemiesNear(target.Position, 4f),
                        IsTargetControlled(target));

                if (chargeDecision.ShouldUse)
                {
                    direction = ResolveDirectionTo(target.Position);
                    return true;
                }
            }

            if (gadget is HealBurstGadgetDefinition)
            {
                AIBrawlerPackDecision healDecision =
                    AIBrawlerIntelligencePackUtility.EvaluateSelfHealGadget(
                        selfHealthRatio,
                        CountEnemiesNear(_self.Position, _profile.GadgetEnemyDistanceThreshold),
                        _self.State.CarriedGemCount > 0);

                if (healDecision.ShouldUse ||
                    selfHealthRatio <= Mathf.Min(0.7f, _profile.GadgetLowHealthThreshold + 0.12f))
                {
                    direction = SpatialEntityUtility.IsAlive(target)
                        ? ResolveDirectionTo(target.Position)
                        : _self.transform.forward;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindAllyHealPulseTarget(
            AllyHealPulseGadgetDefinition gadget,
            out BrawlerController allyToHeal)
        {
            allyToHeal = null;

            if (gadget == null || SimulationClock.Grid == null)
                return false;

            _gadgetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, gadget.Radius, _gadgetBuffer);

            float bestScore = float.MinValue;
            ScoreAllyHealPulseCandidate(_self, gadget, ref allyToHeal, ref bestScore);

            for (int i = 0; i < _gadgetBuffer.Count; i++)
            {
                if (_gadgetBuffer[i] is not BrawlerController ally)
                    continue;

                if (ally.Team != _self.Team || ally.State == null || ally.State.IsDead)
                    continue;

                ScoreAllyHealPulseCandidate(ally, gadget, ref allyToHeal, ref bestScore);
            }

            return allyToHeal != null;
        }

        private void ScoreAllyHealPulseCandidate(
            BrawlerController ally,
            AllyHealPulseGadgetDefinition gadget,
            ref BrawlerController best,
            ref float bestScore)
        {
            if (ally == null || ally.State == null || ally.State.IsDead)
                return;

            float healthRatio = ally.State.CurrentHealth / Mathf.Max(1f, ally.State.MaxHealth.Value);
            float missingHealth = ally.State.MaxHealth.Value - ally.State.CurrentHealth;

            if (healthRatio > 0.72f && missingHealth < gadget.HealAmount * 0.65f)
                return;

            float distance = Vector3.Distance(_self.Position, ally.Position);
            float score =
                ((1f - healthRatio) * 100f) +
                Mathf.Clamp01(missingHealth / Mathf.Max(1f, gadget.HealAmount)) * 32f -
                distance * 1.5f +
                ally.State.CarriedGemCount * 7f;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        private Vector3 ResolveDirectionTo(Vector3 position)
        {
            Vector3 direction = position - _self.Position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : _self.transform.forward;
        }

        private Vector3 ResolveDirectionAwayFrom(Vector3 position)
        {
            Vector3 direction = _self.Position - position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : -_self.transform.forward;
        }

        private float GetMainAttackRange()
        {
            AbilityDefinition attack = _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private float GetSuperRange()
        {
            AbilityDefinition super = _self.State != null
                ? _self.State.GetCurrentSuperDefinition()
                : _self.Definition?.SuperAbility;
            return super != null ? super.GetAIMaxRange() : 6f;
        }

        private GadgetDefinition GetCurrentGadgetDefinition()
        {
            return _self.State != null
                ? _self.State.GetCurrentGadgetDefinition() ?? _self.Definition?.Gadget
                : _self.Definition?.Gadget;
        }

        private float GetTargetHealthRatio(ISpatialEntity target)
        {
            if (target is not BrawlerController brawler || brawler.State == null)
                return 1f;

            return brawler.State.CurrentHealth / Mathf.Max(1f, brawler.State.MaxHealth.Value);
        }

        private bool IsTargetControlled(ISpatialEntity target)
        {
            return target is BrawlerController brawler &&
                   brawler.State != null &&
                   (brawler.State.HasStatus(StatusEffectType.Stun) ||
                    brawler.State.HasStatus(StatusEffectType.Slow));
        }

        private int CountEnemiesNear(Vector3 position, float radius)
        {
            if (SimulationClock.Grid == null)
                return 0;

            _gadgetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(position, radius, _gadgetBuffer);

            int count = 0;
            for (int i = 0; i < _gadgetBuffer.Count; i++)
            {
                ISpatialEntity entity = _gadgetBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) || entity.Team == _self.Team)
                    continue;

                if (entity is BrawlerController brawler && (brawler.State == null || brawler.State.IsDead))
                    continue;

                count++;
            }

            return count;
        }
    }
}
