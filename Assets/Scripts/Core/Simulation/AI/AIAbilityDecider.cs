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

        private uint _nextPrimaryAttackTick;
        private uint _nextGadgetTick;

        public AIAbilityDecider(BrawlerController self, BrawlerAIProfile profile, AICommandSource commandSource)
        {
            _self = self;
            _profile = profile;
            _commandSource = commandSource;
            _specialistPlanner = new AIAbilitySpecialistPlanner(self);
        }

        public void TryUseMainAttack(ISpatialEntity target, uint currentTick, float maxRange)
        {
            if (!SpatialEntityUtility.IsAlive(target))
                return;

            if (currentTick < _nextPrimaryAttackTick)
                return;

            if (!_specialistPlanner.TryBuildMainAttackPlan(
                    target,
                    maxRange,
                    out AIAbilityCastPlan plan))
            {
                return;
            }

            plan = AIAimAccuracyUtility.ApplyAimError(
                plan,
                _self.Position,
                _profile.AimErrorDegrees);

            _commandSource?.QueueMainAttack(
                plan.Direction,
                plan.TargetPoint,
                plan.HasTargetPoint);

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

            float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            float distance = Vector3.Distance(_self.Position, target.Position);

            bool lowHealthEmergency = selfHealthRatio <= _profile.GadgetLowHealthThreshold;
            bool closeDanger = distance <= _profile.GadgetEnemyDistanceThreshold;

            if (!lowHealthEmergency && !closeDanger)
                return;

            Vector3 dir = (target.Position - _self.Position).normalized;
            _commandSource?.QueueGadget(dir);
            _nextGadgetTick = currentTick + _profile.GadgetCooldownTicks;
        }

        public void Reset()
        {
            _nextPrimaryAttackTick = 0;
            _nextGadgetTick = 0;
        }
    }
}
