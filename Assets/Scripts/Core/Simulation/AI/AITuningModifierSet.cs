using System;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    [Serializable]
    public struct AITuningModifierSet
    {
        [Header("Skill")]
        [Tooltip("Signed offset applied after difficulty tuning. Positive values make bots react later.")]
        public int ReactionDelayOffsetTicks;
        [Tooltip("Signed aim-error offset applied after difficulty tuning.")]
        public float AimErrorOffsetDegrees;
        [Tooltip("Multiplier for idle/combat perception intervals. Values above 1 make bots sense less often.")]
        [Range(0.25f, 3f)] public float SenseIntervalMultiplier;
        [Tooltip("Multiplier for main attack cadence. Values above 1 fire less often.")]
        [Range(0.25f, 3f)] public float AttackCadenceMultiplier;
        [Tooltip("Multiplier for super/gadget decision cooldowns. Values above 1 use abilities less often.")]
        [Range(0.25f, 3f)] public float AbilityCooldownMultiplier;

        [Header("Behavior")]
        [Tooltip("Scales approach, super pressure, focus fire, and finisher pressure.")]
        [Range(0.25f, 3f)] public float AggressionMultiplier;
        [Tooltip("Scales retreat, hold-range, reposition, danger, and defensive recovery pressure.")]
        [Range(0.25f, 3f)] public float SafetyMultiplier;
        [Tooltip("Scales peel, regroup, ally spacing, anti-overfocus, and team role coordination.")]
        [Range(0.25f, 3f)] public float TeamplayMultiplier;
        [Tooltip("Scales objective and lane-control pressure.")]
        [Range(0.25f, 3f)] public float ObjectiveMultiplier;

        [Header("Movement / Map")]
        [Tooltip("Scales tactical step distances and inversely scales tactical retarget interval.")]
        [Range(0.25f, 3f)] public float TacticalMovementMultiplier;
        [Tooltip("Scales projectile/hazard avoidance pressure.")]
        [Range(0.25f, 3f)] public float DangerAvoidanceMultiplier;
        [Tooltip("Scales map-cover, anti-exposure, and safe-position preferences.")]
        [Range(0.25f, 3f)] public float MapSafetyMultiplier;

        [Header("Humanization")]
        [Tooltip("Scales bounded human imperfection: action jitter, fake-outs, and pressure mistakes.")]
        [Range(0.25f, 3f)] public float HumanizationMultiplier;

        [Header("Debug / Validation")]
        public bool OverrideDebugFlags;
        public bool EnableValidationTelemetry;
        public bool EnableDebugSnapshots;
        public uint DebugSnapshotIntervalTicks;
        public bool LogDecisionTicks;
        public bool LogTacticalMovement;
        public bool LogMapIntelligence;
        public bool LogDangerAvoidance;
        public bool LogFailureRecovery;
        public bool LogHumanization;
        public bool LogBudgetWarnings;

        public void ApplyTo(BrawlerAIProfile profile)
        {
            if (profile == null)
                return;

            ApplySkill(profile);
            ApplyBehavior(profile);
            ApplyMovementAndMap(profile);
            ApplyHumanization(profile);
            ApplyDebug(profile);
        }

        public string GetDebugSummary(string label)
        {
            return
                $"{label} " +
                $"react={ReactionDelayOffsetTicks:+#;-#;0} " +
                $"aim={AimErrorOffsetDegrees:+0.0;-0.0;0.0} " +
                $"sense={MultiplierOrOne(SenseIntervalMultiplier):0.00} " +
                $"atk={MultiplierOrOne(AttackCadenceMultiplier):0.00} " +
                $"abil={MultiplierOrOne(AbilityCooldownMultiplier):0.00} " +
                $"agg={MultiplierOrOne(AggressionMultiplier):0.00} " +
                $"safe={MultiplierOrOne(SafetyMultiplier):0.00} " +
                $"team={MultiplierOrOne(TeamplayMultiplier):0.00} " +
                $"obj={MultiplierOrOne(ObjectiveMultiplier):0.00} " +
                $"move={MultiplierOrOne(TacticalMovementMultiplier):0.00} " +
                $"danger={MultiplierOrOne(DangerAvoidanceMultiplier):0.00} " +
                $"map={MultiplierOrOne(MapSafetyMultiplier):0.00} " +
                $"human={HumanizationOrOne(HumanizationMultiplier):0.00}";
        }

        private void ApplySkill(BrawlerAIProfile profile)
        {
            profile.ReactionDelayTicks = AddTicks(profile.ReactionDelayTicks, ReactionDelayOffsetTicks);
            profile.AimErrorDegrees += AimErrorOffsetDegrees;

            float sense = MultiplierOrOne(SenseIntervalMultiplier);
            profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, sense, 1u);
            profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, sense, 1u);

            profile.AttackCadenceTicks = ScaleTicks(
                profile.AttackCadenceTicks,
                MultiplierOrOne(AttackCadenceMultiplier),
                1u);

            float ability = MultiplierOrOne(AbilityCooldownMultiplier);
            profile.SuperDecisionCooldownTicks = ScaleTicks(
                profile.SuperDecisionCooldownTicks,
                ability,
                1u);
            profile.GadgetCooldownTicks = ScaleTicks(
                profile.GadgetCooldownTicks,
                ability,
                1u);
        }

        private void ApplyBehavior(BrawlerAIProfile profile)
        {
            float aggression = MultiplierOrOne(AggressionMultiplier);
            profile.ApproachWeight *= aggression;
            profile.SuperWeight *= aggression;
            profile.FocusFireWeight *= aggression;
            profile.InRangeTargetBonus *= aggression;
            profile.LowHealthTargetBias *= aggression;
            profile.FinisherBonus *= aggression;

            float safety = MultiplierOrOne(SafetyMultiplier);
            profile.RetreatWeight *= safety;
            profile.HoldRangeWeight *= safety;
            profile.RepositionWeight *= safety;
            profile.LowHealthRetreatRatio *= safety;
            profile.RegroupHealthThreshold *= safety;
            profile.ReactiveRetreatPressureBonus *= safety;
            profile.ReactiveRepositionPressureBonus *= safety;
            profile.FailureRecoveryDetourDistance *= safety;

            float teamplay = MultiplierOrOne(TeamplayMultiplier);
            profile.PeelWeight *= teamplay;
            profile.RegroupWeight *= teamplay;
            profile.TeamRoleCoordinationWeight *= teamplay;
            profile.TeamActionCrowdingPenalty *= teamplay;
            profile.TeamBacklineAnchorBonus *= teamplay;
            profile.OverFocusedTargetPenaltyPerAlly *= teamplay;
            profile.AllyAvoidanceWeight *= teamplay;
            profile.AllySupportRange *= teamplay;

            float objective = MultiplierOrOne(ObjectiveMultiplier);
            profile.ObjectiveWeight *= objective;
            profile.MacroActionBiasWeight *= objective;
            profile.MapLaneControlPreference *= objective;
            profile.MapChokeControlPreference *= objective;
        }

        private void ApplyMovementAndMap(BrawlerAIProfile profile)
        {
            float movement = MultiplierOrOne(TacticalMovementMultiplier);
            profile.TacticalStrafeDistance *= movement;
            profile.TacticalKiteDistance *= movement;
            profile.TacticalMinimumStepDistance *= movement;
            profile.RepositionStepDistance *= movement;
            profile.TacticalMoveRetargetTicks = ScaleTicks(
                profile.TacticalMoveRetargetTicks,
                InverseMultiplier(movement),
                1u);
            profile.TacticalMoveHeartbeatTicks = ScaleTicks(
                profile.TacticalMoveHeartbeatTicks,
                InverseMultiplier(movement),
                1u);

            float danger = MultiplierOrOne(DangerAvoidanceMultiplier);
            profile.DangerScanRadius *= danger;
            profile.DangerEvadeScoreBonus *= danger;
            profile.DangerEvadeDistance *= danger;
            profile.DangerEvadePressureThreshold *= InverseMultiplier(danger);
            profile.DangerRefreshIntervalTicks = ScaleTicks(
                profile.DangerRefreshIntervalTicks,
                InverseMultiplier(danger),
                1u);

            float mapSafety = MultiplierOrOne(MapSafetyMultiplier);
            profile.MapCoverPreference *= mapSafety;
            profile.MapLineOfSightCoverPreference *= mapSafety;
            profile.MapExposedPositionPenalty *= mapSafety;
            profile.MapChokepointPenalty *= mapSafety;
            profile.MapThreatAvoidanceWeight *= mapSafety;
            profile.MapThrowerSafePositionPreference *= mapSafety;
            profile.MapCoverPeekPreference *= mapSafety;
        }

        private void ApplyHumanization(BrawlerAIProfile profile)
        {
            float human = HumanizationOrOne(HumanizationMultiplier);
            profile.HumanizationActionScoreJitter *= human;
            profile.HumanizationFakeOutChance *= human;
            profile.HumanizationFakeOutScoreBonus *= human;
            profile.HumanizationPressureMistakeChance *= human;
            profile.HumanizationPressureMistakePenalty *= human;
            profile.HumanizationPersonalityExpression *= human;
            profile.HumanizationReactionJitterTicks = ScaleTicks(
                profile.HumanizationReactionJitterTicks,
                human,
                0u);
        }

        private void ApplyDebug(BrawlerAIProfile profile)
        {
            if (!OverrideDebugFlags)
                return;

            profile.EnableValidationTelemetry = EnableValidationTelemetry;
            profile.EnableDebugSnapshots = EnableDebugSnapshots;

            if (DebugSnapshotIntervalTicks > 0u)
                profile.DebugSnapshotIntervalTicks = DebugSnapshotIntervalTicks;

            profile.LogDecisionTicks = LogDecisionTicks;
            profile.LogTacticalMovement = LogTacticalMovement;
            profile.LogMapIntelligence = LogMapIntelligence;
            profile.LogDangerAvoidance = LogDangerAvoidance;
            profile.LogFailureRecovery = LogFailureRecovery;
            profile.LogHumanization = LogHumanization;
            profile.LogBudgetWarnings = LogBudgetWarnings;
        }

        private static float MultiplierOrOne(float value)
        {
            return value <= 0f ? 1f : value;
        }

        private static float HumanizationOrOne(float value)
        {
            return MultiplierOrOne(value);
        }

        private static float InverseMultiplier(float value)
        {
            float safe = Mathf.Max(0.01f, value);
            return 1f / safe;
        }

        private static uint ScaleTicks(uint value, float multiplier, uint minimum)
        {
            int scaled = Mathf.RoundToInt(value * multiplier);
            return (uint)Mathf.Max((int)minimum, scaled);
        }

        private static uint AddTicks(uint value, int offset)
        {
            long result = (long)value + offset;
            if (result <= 0L)
                return 0u;

            if (result > uint.MaxValue)
                return uint.MaxValue;

            return (uint)result;
        }
    }
}
