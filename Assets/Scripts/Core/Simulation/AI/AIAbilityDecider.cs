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

            float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            float distance = Vector3.Distance(_self.Position, target.Position);

            if (TryBuildGadgetSynergyDirection(target, selfHealthRatio, out Vector3 synergyDirection))
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
            float selfHealthRatio,
            out Vector3 direction)
        {
            direction = Vector3.zero;

            GadgetDefinition gadget = _self.Definition != null ? _self.Definition.Gadget : null;
            if (gadget == null)
                return false;

            if (gadget is AllyHealPulseGadgetDefinition allyHeal &&
                TryFindAllyHealPulseTarget(allyHeal, out BrawlerController allyToHeal))
            {
                direction = ResolveDirectionTo(allyToHeal.Position);
                return true;
            }

            if (gadget is AmmoRefillGadgetDefinition &&
                _self.State.Ammo != null &&
                _self.State.Ammo.AvailableBars <= 1 &&
                SpatialEntityUtility.IsAlive(target) &&
                Vector3.Distance(_self.Position, target.Position) <= GetMainAttackRange() * 0.95f)
            {
                direction = ResolveDirectionTo(target.Position);
                return true;
            }

            if (gadget is SuperChargeGadgetDefinition superCharge &&
                !_self.State.SuperCharge.IsReady &&
                _self.State.SuperCharge.ChargePercent + Mathf.Max(0f, superCharge.ChargeFraction) >= 1f &&
                SpatialEntityUtility.IsAlive(target) &&
                Vector3.Distance(_self.Position, target.Position) <= GetSuperRange() * _profile.SuperMaxRangeMultiplier)
            {
                direction = ResolveDirectionTo(target.Position);
                return true;
            }

            if (gadget is HealBurstGadgetDefinition &&
                selfHealthRatio <= Mathf.Min(0.7f, _profile.GadgetLowHealthThreshold + 0.12f))
            {
                direction = SpatialEntityUtility.IsAlive(target)
                    ? ResolveDirectionTo(target.Position)
                    : _self.transform.forward;
                return true;
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
            for (int i = 0; i < _gadgetBuffer.Count; i++)
            {
                if (_gadgetBuffer[i] is not BrawlerController ally)
                    continue;

                if (ally.Team != _self.Team || ally.State == null || ally.State.IsDead)
                    continue;

                float healthRatio = ally.State.CurrentHealth / Mathf.Max(1f, ally.State.MaxHealth.Value);
                float missingHealth = ally.State.MaxHealth.Value - ally.State.CurrentHealth;

                if (healthRatio > 0.72f && missingHealth < gadget.HealAmount * 0.65f)
                    continue;

                float distance = Vector3.Distance(_self.Position, ally.Position);
                float score =
                    ((1f - healthRatio) * 100f) +
                    Mathf.Clamp01(missingHealth / Mathf.Max(1f, gadget.HealAmount)) * 32f -
                    distance * 1.5f +
                    ally.State.CarriedGemCount * 7f;

                if (score > bestScore)
                {
                    bestScore = score;
                    allyToHeal = ally;
                }
            }

            return allyToHeal != null;
        }

        private Vector3 ResolveDirectionTo(Vector3 position)
        {
            Vector3 direction = position - _self.Position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : _self.transform.forward;
        }

        private float GetMainAttackRange()
        {
            AbilityDefinition attack = _self.Definition != null ? _self.Definition.MainAttack : null;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private float GetSuperRange()
        {
            AbilityDefinition super = _self.Definition != null ? _self.Definition.SuperAbility : null;
            return super != null ? super.GetAIMaxRange() : 6f;
        }
    }
}
