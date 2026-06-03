using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public static class AIProfileTuningUtility
    {
        public static void ApplyRuntimeTuning(
            BrawlerAIProfile profile,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            ApplyRuntimeTuning(profile, difficulty, personality, null);
        }

        public static void ApplyRuntimeTuning(
            BrawlerAIProfile profile,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            AITuningCatalog tuningCatalog)
        {
            if (profile == null)
                return;

            profile.Difficulty = difficulty;
            profile.Personality = personality;

            EnsureTacticalStabilizationDefaults(profile);
            EnsureGemGrabObjectiveDefaults(profile);
            EnsureLaneDisciplineDefaults(profile);
            EnsureMapGeometryDefaults(profile);
            ApplyDifficulty(profile, difficulty);
            ApplyPersonality(profile, personality);

            Normalize(profile);
            ApplyFairPlayGuardrails(profile);
            Normalize(profile);

            tuningCatalog?.ApplyTo(profile, difficulty, personality);
            AITuningRuntimeOverrides.ApplyTo(profile);
            Normalize(profile);
            ApplyFairPlayGuardrails(profile);
            Normalize(profile);
        }

        public static void RebuildRuntimeTuning(
            BrawlerAIProfile sourceProfile,
            BrawlerAIProfile runtimeProfile,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            AITuningCatalog tuningCatalog)
        {
            if (sourceProfile == null || runtimeProfile == null)
                return;

            string runtimeName = runtimeProfile.name;
            JsonUtility.FromJsonOverwrite(
                JsonUtility.ToJson(sourceProfile),
                runtimeProfile);
            runtimeProfile.name = runtimeName;

            ApplyRuntimeTuning(
                runtimeProfile,
                difficulty,
                personality,
                tuningCatalog);
        }

        private static void ApplyDifficulty(BrawlerAIProfile profile, AIDifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case AIDifficultyLevel.Easy:
                    profile.ReactionDelayTicks = 10;
                    profile.AimErrorDegrees = 9.5f;
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 1.55f, 4);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 2.35f, 2);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 1.35f, 1);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 1.4f, 1);
                    profile.GadgetCooldownTicks = ScaleTicks(profile.GadgetCooldownTicks, 1.25f, 1);
                    profile.MinimumCommittedActionScore *= 1.10f;
                    profile.ActionSwitchScoreMargin *= 1.25f;
                    profile.CombatActionCommitmentTicks = ScaleTicks(profile.CombatActionCommitmentTicks, 1.25f, 1);
                    profile.NonCombatActionCommitmentTicks = ScaleTicks(profile.NonCombatActionCommitmentTicks, 1.20f, 1);
                    profile.CurrentTargetStickiness *= 1.25f;
                    profile.FocusFireWeight *= 0.75f;
                    profile.ClusterTargetBonus *= 0.75f;
                    profile.InRangeTargetBonus *= 0.80f;
                    profile.LowHealthTargetBias *= 0.80f;
                    profile.FinisherBonus *= 0.80f;
                    profile.OverFocusedTargetPenaltyPerAlly *= 0.70f;
                    profile.TeamRoleCoordinationWeight *= 0.75f;
                    profile.TeamActionCrowdingPenalty *= 0.75f;
                    profile.TeamFrontlineNeedBonus *= 0.75f;
                    profile.TeamBacklineAnchorBonus *= 0.75f;
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 1.35f, 1);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 1.25f, 1);
                    profile.TacticalDirectionFlipCooldownTicks =
                        ScaleTicks(profile.TacticalDirectionFlipCooldownTicks, 1.20f, 1);
                    profile.TacticalDestinationSwitchDistance *= 1.10f;
                    profile.TacticalDestinationBlend *= 0.90f;
                    profile.AIMoveInputTurnRateDegreesPerTick *= 0.85f;
                    profile.AIHighPriorityMoveInputTurnRateDegreesPerTick *= 0.90f;
                    profile.NavigationStuckSampleIntervalTicks =
                        ScaleTicks(profile.NavigationStuckSampleIntervalTicks, 1.15f, 1);
                    profile.DangerScanRadius *= 0.85f;
                    profile.DangerReactionTimeSeconds *= 0.80f;
                    profile.DangerEvadePressureThreshold *= 1.15f;
                    profile.DangerEvadeScoreBonus *= 0.80f;
                    profile.DangerRefreshIntervalTicks = ScaleTicks(profile.DangerRefreshIntervalTicks, 1.5f, 1);
                    profile.DangerEvadeRetargetTicks = ScaleTicks(profile.DangerEvadeRetargetTicks, 1.35f, 1);
                    profile.MapLineOfSightCoverPreference *= 0.80f;
                    profile.MapExposedPositionPenalty *= 0.75f;
                    profile.MapOpenShotPreference *= 0.80f;
                    profile.MapEscapeSpacePreference *= 0.90f;
                    profile.MapCoverDancePreference *= 0.85f;
                    profile.MapFireLanePressurePreference *= 0.80f;
                    profile.MapThrowerSpacingPreference *= 0.85f;
                    profile.MapWallHugPenalty *= 0.90f;
                    profile.NavigationStuckSampleLimit += 1;
                    profile.StaleDestinationRecoveryTicks = ScaleTicks(profile.StaleDestinationRecoveryTicks, 1.25f, 1);
                    profile.FailureRecoveryCooldownTicks = ScaleTicks(profile.FailureRecoveryCooldownTicks, 1.25f, 1);
                    profile.FailedCastSuppressionTicks = ScaleTicks(profile.FailedCastSuppressionTicks, 1.15f, 1);
                    profile.HumanizationReactionJitterTicks = 5;
                    profile.HumanizationActionScoreJitter = 3.5f;
                    profile.HumanizationFakeOutChance *= 1.65f;
                    profile.HumanizationFakeOutScoreBonus *= 1.25f;
                    profile.HumanizationPressureMistakeChance *= 2.25f;
                    profile.HumanizationPressureMistakePenalty *= 1.35f;
                    profile.HumanizationPressureMistakeCooldownTicks =
                        ScaleTicks(profile.HumanizationPressureMistakeCooldownTicks, 0.75f, 1);
                    profile.HumanizationPersonalityExpression *= 1.15f;
                    profile.GemPickupMinimumScore *= 1.10f;
                    profile.GemPickupSecureThresholdBonus *= 0.90f;
                    profile.GemPickupDenyThresholdBonus *= 0.90f;
                    profile.GemPickupCarrierSafetyPenalty *= 1.15f;
                    profile.GemPickupThreatPenalty *= 1.12f;
                    profile.LaneDisciplineWeight *= 1.10f;
                    profile.LowHealthChaseMaxDistance *= 0.92f;
                    profile.LowHealthChaseApproachBonus *= 0.85f;
                    profile.UnsafeChasePenalty *= 1.15f;
                    profile.LowHealthChaseMaxTicks =
                        ScaleTicks(profile.LowHealthChaseMaxTicks, 0.85f, 1);
                    profile.LowHealthChaseCooldownTicks =
                        ScaleTicks(profile.LowHealthChaseCooldownTicks, 1.20f, 1);
                    profile.ChaseCommitScoreBonus *= 0.85f;
                    profile.ChaseDisengageScorePenalty *= 1.12f;
                    profile.BadMapChasePenalty *= 1.15f;
                    break;

                case AIDifficultyLevel.Hard:
                    profile.ReactionDelayTicks = 1;
                    profile.AimErrorDegrees = 1.5f;
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 0.75f, 2);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 0.65f, 2);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 0.85f, 1);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 0.75f, 1);
                    profile.GadgetCooldownTicks = ScaleTicks(profile.GadgetCooldownTicks, 0.85f, 1);
                    profile.MinimumCommittedActionScore *= 0.90f;
                    profile.ActionSwitchScoreMargin *= 0.85f;
                    profile.CombatActionCommitmentTicks = ScaleTicks(profile.CombatActionCommitmentTicks, 0.85f, 1);
                    profile.NonCombatActionCommitmentTicks = ScaleTicks(profile.NonCombatActionCommitmentTicks, 0.90f, 1);
                    profile.EmergencyOverrideScore *= 0.96f;
                    profile.CurrentTargetStickiness *= 0.92f;
                    profile.FocusFireWeight *= 1.15f;
                    profile.ClusterTargetBonus *= 1.20f;
                    profile.InRangeTargetBonus *= 1.15f;
                    profile.LowHealthTargetBias *= 1.15f;
                    profile.FinisherBonus *= 1.20f;
                    profile.OverFocusedTargetPenaltyPerAlly *= 1.15f;
                    profile.TeamRoleCoordinationWeight *= 1.15f;
                    profile.TeamActionCrowdingPenalty *= 1.10f;
                    profile.TeamFrontlineNeedBonus *= 1.10f;
                    profile.TeamBacklineAnchorBonus *= 1.10f;
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 0.80f, 1);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 0.80f, 1);
                    profile.TacticalDirectionFlipCooldownTicks =
                        ScaleTicks(profile.TacticalDirectionFlipCooldownTicks, 0.85f, 1);
                    profile.TacticalDestinationSwitchDistance *= 0.90f;
                    profile.TacticalDestinationBlend *= 1.10f;
                    profile.AIMoveInputTurnRateDegreesPerTick *= 1.10f;
                    profile.AIHighPriorityMoveInputTurnRateDegreesPerTick *= 1.10f;
                    profile.NavigationStuckSampleIntervalTicks =
                        ScaleTicks(profile.NavigationStuckSampleIntervalTicks, 0.85f, 1);
                    profile.TacticalMinimumStepDistance *= 1.10f;
                    profile.DangerScanRadius *= 1.10f;
                    profile.DangerReactionTimeSeconds *= 1.15f;
                    profile.DangerEvadePressureThreshold *= 0.90f;
                    profile.DangerEvadeScoreBonus *= 1.12f;
                    profile.DangerRefreshIntervalTicks = ScaleTicks(profile.DangerRefreshIntervalTicks, 0.75f, 1);
                    profile.DangerEvadeRetargetTicks = ScaleTicks(profile.DangerEvadeRetargetTicks, 0.80f, 1);
                    profile.MapLineOfSightCoverPreference *= 1.12f;
                    profile.MapExposedPositionPenalty *= 1.10f;
                    profile.MapOpenShotPreference *= 1.12f;
                    profile.MapEscapeSpacePreference *= 1.08f;
                    profile.MapCoverDancePreference *= 1.12f;
                    profile.MapFireLanePressurePreference *= 1.15f;
                    profile.MapThrowerSpacingPreference *= 1.12f;
                    profile.MapWallHugPenalty *= 1.08f;
                    profile.StaleDestinationRecoveryTicks = ScaleTicks(profile.StaleDestinationRecoveryTicks, 0.85f, 1);
                    profile.FailureRecoveryCooldownTicks = ScaleTicks(profile.FailureRecoveryCooldownTicks, 0.85f, 1);
                    profile.FailedCastSuppressionTicks = ScaleTicks(profile.FailedCastSuppressionTicks, 0.85f, 1);
                    profile.HumanizationReactionJitterTicks = 1;
                    profile.HumanizationActionScoreJitter = 0.9f;
                    profile.HumanizationFakeOutChance *= 0.55f;
                    profile.HumanizationFakeOutScoreBonus *= 0.65f;
                    profile.HumanizationPressureMistakeChance *= 0.45f;
                    profile.HumanizationPressureMistakePenalty *= 0.65f;
                    profile.HumanizationPressureMistakeCooldownTicks =
                        ScaleTicks(profile.HumanizationPressureMistakeCooldownTicks, 1.4f, 1);
                    profile.HumanizationPersonalityExpression *= 0.80f;
                    profile.GemPickupMinimumScore *= 0.92f;
                    profile.GemPickupSecureThresholdBonus *= 1.10f;
                    profile.GemPickupDenyThresholdBonus *= 1.10f;
                    profile.GemPickupCarrierSafetyPenalty *= 0.92f;
                    profile.GemPickupThreatPenalty *= 0.90f;
                    profile.LaneDisciplineWeight *= 0.95f;
                    profile.LowHealthChaseMaxDistance *= 1.08f;
                    profile.LowHealthChaseApproachBonus *= 1.12f;
                    profile.UnsafeChasePenalty *= 0.90f;
                    profile.LowHealthChaseMaxTicks =
                        ScaleTicks(profile.LowHealthChaseMaxTicks, 1.12f, 1);
                    profile.LowHealthChaseCooldownTicks =
                        ScaleTicks(profile.LowHealthChaseCooldownTicks, 0.85f, 1);
                    profile.ChaseCommitScoreBonus *= 1.12f;
                    profile.ChaseDisengageScorePenalty *= 0.92f;
                    profile.BadMapChasePenalty *= 0.90f;
                    break;

                case AIDifficultyLevel.Normal:
                default:
                    profile.ReactionDelayTicks = 3;
                    profile.AimErrorDegrees = 3f;
                    if (profile.HumanizationReactionJitterTicks < 2u)
                        profile.HumanizationReactionJitterTicks = 2u;
                    break;
            }
        }

        private static void ApplyPersonality(BrawlerAIProfile profile, AIPersonalityType personality)
        {
            switch (personality)
            {
                case AIPersonalityType.Aggressive:
                    profile.ApproachWeight *= 1.25f;
                    profile.SuperWeight *= 1.15f;
                    profile.FocusFireWeight *= 1.20f;
                    profile.InRangeTargetBonus *= 1.10f;
                    profile.MinimumCommittedActionScore *= 0.95f;
                    profile.CombatActionCommitmentTicks = ScaleTicks(profile.CombatActionCommitmentTicks, 0.90f, 1);
                    profile.EmergencyOverrideScore *= 0.97f;
                    profile.RetreatWeight *= 0.80f;
                    profile.RegroupWeight *= 0.85f;
                    profile.PeelWeight *= 0.90f;
                    profile.LowHealthRetreatRatio *= 0.85f;
                    profile.PreferredAttackRangeRatio *= 0.92f;
                    profile.TacticalKiteDistance *= 0.85f;
                    profile.DangerEvadePressureThreshold *= 1.10f;
                    profile.DangerEvadeScoreBonus *= 0.90f;
                    profile.DangerEvadeDistance *= 0.90f;
                    profile.TeamActionCrowdingPenalty *= 0.85f;
                    profile.TeamFrontlineNeedBonus *= 1.20f;
                    profile.TeamBacklineAnchorBonus *= 0.80f;
                    profile.MapLineOfSightCoverPreference *= 0.85f;
                    profile.MapExposedPositionPenalty *= 0.85f;
                    profile.MapOpenShotPreference *= 1.15f;
                    profile.MapEscapeSpacePreference *= 0.92f;
                    profile.MapCoverDancePreference *= 1.12f;
                    profile.MapFireLanePressurePreference *= 1.15f;
                    profile.MapThrowerSpacingPreference *= 0.92f;
                    profile.MapWallHugPenalty *= 0.92f;
                    profile.FailureRecoveryDetourDistance *= 0.95f;
                    profile.HumanizationFakeOutChance *= 1.25f;
                    profile.HumanizationFakeOutScoreBonus *= 1.18f;
                    profile.HumanizationPressureMistakeChance *= 1.10f;
                    profile.HumanizationPersonalityExpression *= 1.20f;
                    profile.GemPickupMinimumScore *= 0.90f;
                    profile.GemPickupDenyThresholdBonus *= 1.12f;
                    profile.GemPickupThreatPenalty *= 0.90f;
                    profile.LaneDisciplineWeight *= 0.85f;
                    profile.LowHealthChaseMaxDistance *= 1.10f;
                    profile.LowHealthChaseApproachBonus *= 1.18f;
                    profile.UnsafeChasePenalty *= 0.85f;
                    profile.LowHealthChaseCommitTicks =
                        ScaleTicks(profile.LowHealthChaseCommitTicks, 1.12f, 1);
                    profile.LowHealthChaseMaxTicks =
                        ScaleTicks(profile.LowHealthChaseMaxTicks, 1.15f, 1);
                    profile.LowHealthChaseCooldownTicks =
                        ScaleTicks(profile.LowHealthChaseCooldownTicks, 0.85f, 1);
                    profile.ChaseCommitScoreBonus *= 1.18f;
                    profile.ChaseDisengageScorePenalty *= 0.85f;
                    profile.BadMapChasePenalty *= 0.85f;
                    break;

                case AIPersonalityType.Cautious:
                    profile.RetreatWeight *= 1.25f;
                    profile.HoldRangeWeight *= 1.15f;
                    profile.RepositionWeight *= 1.20f;
                    profile.ApproachWeight *= 0.82f;
                    profile.ActionSwitchScoreMargin *= 1.10f;
                    profile.CombatActionCommitmentTicks = ScaleTicks(profile.CombatActionCommitmentTicks, 1.10f, 1);
                    profile.NonCombatActionCommitmentTicks = ScaleTicks(profile.NonCombatActionCommitmentTicks, 1.08f, 1);
                    profile.CurrentTargetStickiness *= 0.90f;
                    profile.LowHealthRetreatRatio *= 1.18f;
                    profile.RegroupHealthThreshold *= 1.15f;
                    profile.PreferredAttackRangeRatio *= 1.08f;
                    profile.TacticalKiteDistance *= 1.15f;
                    profile.FragileRangePadding += 0.25f;
                    profile.DangerScanRadius *= 1.08f;
                    profile.DangerEvadePressureThreshold *= 0.90f;
                    profile.DangerEvadeScoreBonus *= 1.10f;
                    profile.DangerEvadeDistance *= 1.08f;
                    profile.TeamActionCrowdingPenalty *= 1.10f;
                    profile.TeamFrontlineNeedBonus *= 0.80f;
                    profile.TeamBacklineAnchorBonus *= 1.20f;
                    profile.MapLineOfSightCoverPreference *= 1.18f;
                    profile.MapExposedPositionPenalty *= 1.15f;
                    profile.MapOpenShotPreference *= 0.92f;
                    profile.MapEscapeSpacePreference *= 1.15f;
                    profile.MapCoverDancePreference *= 1.08f;
                    profile.MapFireLanePressurePreference *= 0.92f;
                    profile.MapThrowerSpacingPreference *= 1.10f;
                    profile.MapWallHugPenalty *= 1.15f;
                    profile.FailureRecoveryCooldownTicks = ScaleTicks(profile.FailureRecoveryCooldownTicks, 0.90f, 1);
                    profile.FailureRecoveryDetourDistance *= 1.08f;
                    profile.HumanizationFakeOutChance *= 0.90f;
                    profile.HumanizationPressureMistakeChance *= 0.85f;
                    profile.HumanizationPressureMistakePenalty *= 1.10f;
                    profile.HumanizationPersonalityExpression *= 1.10f;
                    profile.GemPickupMinimumScore *= 1.12f;
                    profile.GemPickupCarrierSafetyPenalty *= 1.15f;
                    profile.GemPickupThreatPenalty *= 1.15f;
                    profile.LaneDisciplineWeight *= 1.15f;
                    profile.LowHealthChaseMaxDistance *= 0.90f;
                    profile.LowHealthChaseApproachBonus *= 0.85f;
                    profile.UnsafeChasePenalty *= 1.18f;
                    profile.LowHealthChaseCommitTicks =
                        ScaleTicks(profile.LowHealthChaseCommitTicks, 0.90f, 1);
                    profile.LowHealthChaseMaxTicks =
                        ScaleTicks(profile.LowHealthChaseMaxTicks, 0.82f, 1);
                    profile.LowHealthChaseCooldownTicks =
                        ScaleTicks(profile.LowHealthChaseCooldownTicks, 1.18f, 1);
                    profile.ChaseCommitScoreBonus *= 0.85f;
                    profile.ChaseDisengageScorePenalty *= 1.15f;
                    profile.BadMapChasePenalty *= 1.15f;
                    break;

                case AIPersonalityType.TeamPlayer:
                    profile.PeelWeight *= 1.30f;
                    profile.RegroupWeight *= 1.22f;
                    profile.FocusFireWeight *= 1.15f;
                    profile.ObjectiveWeight *= 1.10f;
                    profile.MinimumCommittedActionScore *= 0.96f;
                    profile.ActionSwitchScoreMargin *= 0.95f;
                    profile.OverFocusedTargetPenaltyPerAlly *= 1.20f;
                    profile.AllyAvoidanceWeight *= 1.15f;
                    profile.AllySupportRange *= 1.12f;
                    profile.ApproachWeight *= 0.95f;
                    profile.DangerEvadeScoreBonus *= 1.04f;
                    profile.TeamRoleCoordinationWeight *= 1.25f;
                    profile.TeamActionCrowdingPenalty *= 1.15f;
                    profile.TeamBacklineAnchorBonus *= 1.10f;
                    profile.MapLineOfSightCoverPreference *= 1.08f;
                    profile.MapOpenShotPreference *= 1.05f;
                    profile.MapEscapeSpacePreference *= 1.08f;
                    profile.MapCoverDancePreference *= 1.05f;
                    profile.MapFireLanePressurePreference *= 1.05f;
                    profile.MapThrowerSpacingPreference *= 1.05f;
                    profile.MapWallHugPenalty *= 1.05f;
                    profile.FailedCastSuppressionTicks = ScaleTicks(profile.FailedCastSuppressionTicks, 1.10f, 1);
                    profile.HumanizationFakeOutChance *= 0.80f;
                    profile.HumanizationPressureMistakeChance *= 0.80f;
                    profile.HumanizationPersonalityExpression *= 1.05f;
                    profile.GemPickupSecureThresholdBonus *= 1.08f;
                    profile.GemPickupDenyThresholdBonus *= 1.08f;
                    profile.GemPickupCountdownResetBonus *= 1.08f;
                    profile.LaneDisciplineWeight *= 1.12f;
                    profile.LaneHoldObjectiveBonus *= 1.12f;
                    profile.LaneHoldSearchScore *= 1.08f;
                    profile.ChaseDisengageScorePenalty *= 1.08f;
                    profile.BadMapChasePenalty *= 1.08f;
                    break;

                case AIPersonalityType.Balanced:
                default:
                    break;
            }
        }

        private static void Normalize(BrawlerAIProfile profile)
        {
            EnsureTacticalStabilizationDefaults(profile);
            EnsureGemGrabObjectiveDefaults(profile);
            EnsureLaneDisciplineDefaults(profile);
            EnsureMapGeometryDefaults(profile);

            profile.ReactionDelayTicks = ClampTicks(profile.ReactionDelayTicks, 0, 24);
            profile.AimErrorDegrees = Mathf.Clamp(profile.AimErrorDegrees, 0f, 15f);

            profile.IdleSenseIntervalTicks = ClampTicks(profile.IdleSenseIntervalTicks, 1, 60);
            profile.CombatSenseIntervalTicks = ClampTicks(profile.CombatSenseIntervalTicks, 1, 30);
            profile.AttackCadenceTicks = ClampTicks(profile.AttackCadenceTicks, 1, 60);
            profile.SuperDecisionCooldownTicks = ClampTicks(profile.SuperDecisionCooldownTicks, 1, 90);
            profile.GadgetCooldownTicks = ClampTicks(profile.GadgetCooldownTicks, 1, 240);

            profile.LowHealthRetreatRatio = Mathf.Clamp(profile.LowHealthRetreatRatio, 0.10f, 0.80f);
            profile.RegroupHealthThreshold = Mathf.Clamp(profile.RegroupHealthThreshold, 0.10f, 0.85f);
            profile.PreferredAttackRangeRatio = Mathf.Clamp(profile.PreferredAttackRangeRatio, 0.55f, 1.20f);
            profile.TooCloseRangeRatio = Mathf.Clamp(profile.TooCloseRangeRatio, 0.20f, 0.75f);
            profile.GemPickupSearchRadius = Mathf.Clamp(profile.GemPickupSearchRadius, 2f, 18f);
            profile.GemPickupBaseScore = Mathf.Clamp(profile.GemPickupBaseScore, 0f, 90f);
            profile.GemPickupValueScore = Mathf.Clamp(profile.GemPickupValueScore, 0f, 30f);
            profile.GemPickupCloseRangeBonus = Mathf.Clamp(profile.GemPickupCloseRangeBonus, 0f, 60f);
            profile.GemPickupClusterRadius = Mathf.Clamp(profile.GemPickupClusterRadius, 0.5f, 4f);
            profile.GemPickupMinimumScore = Mathf.Clamp(profile.GemPickupMinimumScore, 0f, 100f);
            profile.GemPickupSecureThresholdBonus = Mathf.Clamp(profile.GemPickupSecureThresholdBonus, 0f, 80f);
            profile.GemPickupDenyThresholdBonus = Mathf.Clamp(profile.GemPickupDenyThresholdBonus, 0f, 80f);
            profile.GemPickupCountdownResetBonus = Mathf.Clamp(profile.GemPickupCountdownResetBonus, 0f, 90f);
            profile.GemPickupCarrierSafetyPenalty = Mathf.Clamp(profile.GemPickupCarrierSafetyPenalty, 0f, 40f);
            profile.GemPickupThreatRadius = Mathf.Clamp(profile.GemPickupThreatRadius, 1f, 10f);
            profile.GemPickupThreatPenalty = Mathf.Clamp(profile.GemPickupThreatPenalty, 0f, 70f);
            profile.LaneDisciplineWeight = Mathf.Clamp(profile.LaneDisciplineWeight, 0f, 2f);
            profile.LaneHoldObjectiveBonus = Mathf.Clamp(profile.LaneHoldObjectiveBonus, 0f, 35f);
            profile.LaneHoldSearchScore = Mathf.Clamp(profile.LaneHoldSearchScore, 0f, 45f);
            profile.LaneHoldSearchRadius = Mathf.Clamp(profile.LaneHoldSearchRadius, 1f, 20f);
            profile.LaneSideOffset = Mathf.Clamp(profile.LaneSideOffset, 0.5f, 12f);
            profile.LaneForwardOffset = Mathf.Clamp(profile.LaneForwardOffset, -4f, 6f);
            profile.LowHealthChaseHealthThreshold = Mathf.Clamp(profile.LowHealthChaseHealthThreshold, 0.10f, 0.70f);
            profile.LowHealthChaseMaxDistance = Mathf.Clamp(profile.LowHealthChaseMaxDistance, 2f, 16f);
            profile.LowHealthChaseApproachBonus = Mathf.Clamp(profile.LowHealthChaseApproachBonus, 0f, 70f);
            profile.UnsafeChasePenalty = Mathf.Clamp(profile.UnsafeChasePenalty, 0f, 80f);
            profile.LowHealthChaseCommitTicks = ClampTicks(profile.LowHealthChaseCommitTicks, 1, 90);
            profile.LowHealthChaseMaxTicks = ClampTicks(profile.LowHealthChaseMaxTicks, 10, 240);
            profile.LowHealthChaseCooldownTicks = ClampTicks(profile.LowHealthChaseCooldownTicks, 1, 180);
            profile.LowHealthChaseBreakDistanceMultiplier =
                Mathf.Clamp(profile.LowHealthChaseBreakDistanceMultiplier, 1.05f, 2.5f);
            profile.ChaseCommitScoreBonus = Mathf.Clamp(profile.ChaseCommitScoreBonus, 0f, 35f);
            profile.ChaseDisengageScorePenalty = Mathf.Clamp(profile.ChaseDisengageScorePenalty, 0f, 90f);
            profile.BadMapChasePenalty = Mathf.Clamp(profile.BadMapChasePenalty, 0f, 60f);

            profile.TacticalMoveRetargetTicks = ClampTicks(profile.TacticalMoveRetargetTicks, 1, 45);
            profile.TacticalMoveHeartbeatTicks = ClampTicks(profile.TacticalMoveHeartbeatTicks, 1, 60);
            profile.TacticalDestinationStaleDistance = Mathf.Clamp(profile.TacticalDestinationStaleDistance, 0.25f, 4f);
            profile.TacticalDirectionFlipCooldownTicks = ClampTicks(profile.TacticalDirectionFlipCooldownTicks, 1, 90);
            profile.TacticalDestinationSwitchDistance = Mathf.Clamp(profile.TacticalDestinationSwitchDistance, 0.25f, 4f);
            profile.TacticalDestinationBlend = Mathf.Clamp(profile.TacticalDestinationBlend, 0.05f, 1f);
            profile.AIMoveInputTurnRateDegreesPerTick = Mathf.Clamp(profile.AIMoveInputTurnRateDegreesPerTick, 8f, 120f);
            profile.AIHighPriorityMoveInputTurnRateDegreesPerTick = Mathf.Clamp(profile.AIHighPriorityMoveInputTurnRateDegreesPerTick, 20f, 180f);
            profile.TacticalMinimumStepDistance = Mathf.Clamp(profile.TacticalMinimumStepDistance, 0.25f, 2f);
            profile.TacticalStrafeDistance = Mathf.Clamp(profile.TacticalStrafeDistance, 0.4f, 4f);
            profile.TacticalKiteDistance = Mathf.Clamp(profile.TacticalKiteDistance, 0.5f, 5f);

            profile.MinimumCommittedActionScore = Mathf.Clamp(profile.MinimumCommittedActionScore, 0f, 40f);
            profile.ActionSwitchScoreMargin = Mathf.Clamp(profile.ActionSwitchScoreMargin, 4f, 35f);
            profile.CombatActionCommitmentTicks = ClampTicks(profile.CombatActionCommitmentTicks, 1, 60);
            profile.NonCombatActionCommitmentTicks = ClampTicks(profile.NonCombatActionCommitmentTicks, 1, 120);
            profile.EmergencyOverrideScore = Mathf.Clamp(profile.EmergencyOverrideScore, 60f, 120f);
            profile.FocusFireWeight = Mathf.Clamp(profile.FocusFireWeight, 0f, 45f);
            profile.MacroActionBiasWeight = Mathf.Clamp(profile.MacroActionBiasWeight, 0f, 2f);
            profile.OverFocusedTargetPenaltyPerAlly = Mathf.Clamp(profile.OverFocusedTargetPenaltyPerAlly, 0f, 45f);
            profile.MaxOverFocusedTargetPenalty = Mathf.Clamp(profile.MaxOverFocusedTargetPenalty, 0f, 70f);
            profile.TeamRoleCoordinationWeight = Mathf.Clamp(profile.TeamRoleCoordinationWeight, 0f, 2f);
            profile.TeamActionCrowdingPenalty = Mathf.Clamp(profile.TeamActionCrowdingPenalty, 0f, 35f);
            profile.TeamFrontlineNeedBonus = Mathf.Clamp(profile.TeamFrontlineNeedBonus, 0f, 35f);
            profile.TeamBacklineAnchorBonus = Mathf.Clamp(profile.TeamBacklineAnchorBonus, 0f, 30f);
            profile.MaxTeamApproachers = Mathf.Clamp(profile.MaxTeamApproachers, 0, 3);
            profile.MaxTeamPeelResponders = Mathf.Clamp(profile.MaxTeamPeelResponders, 0, 3);
            profile.MaxTeamRegroupResponders = Mathf.Clamp(profile.MaxTeamRegroupResponders, 0, 3);
            profile.MaxTeamObjectiveMovers = Mathf.Clamp(profile.MaxTeamObjectiveMovers, 0, 3);

            profile.MapDestinationSearchRadius = Mathf.Clamp(profile.MapDestinationSearchRadius, 0.5f, 5f);
            profile.MapBushPreference = Mathf.Clamp(profile.MapBushPreference, 0f, 30f);
            profile.MapCoverPreference = Mathf.Clamp(profile.MapCoverPreference, 0f, 30f);
            profile.MapLineOfSightCoverPreference = Mathf.Clamp(profile.MapLineOfSightCoverPreference, 0f, 35f);
            profile.MapExposedPositionPenalty = Mathf.Clamp(profile.MapExposedPositionPenalty, 0f, 35f);
            profile.MapOpenShotPreference = Mathf.Clamp(profile.MapOpenShotPreference, 0f, 30f);
            profile.MapChokepointPenalty = Mathf.Clamp(profile.MapChokepointPenalty, 0f, 35f);
            profile.MapThreatAvoidanceWeight = Mathf.Clamp(profile.MapThreatAvoidanceWeight, 0f, 12f);
            profile.MapPathCostWeight = Mathf.Clamp(profile.MapPathCostWeight, 0f, 2f);
            profile.MapCoverPeekPreference = Mathf.Clamp(profile.MapCoverPeekPreference, 0f, 35f);
            profile.MapLaneControlPreference = Mathf.Clamp(profile.MapLaneControlPreference, 0f, 35f);
            profile.MapChokeControlPreference = Mathf.Clamp(profile.MapChokeControlPreference, 0f, 40f);
            profile.MapThrowerSafePositionPreference = Mathf.Clamp(profile.MapThrowerSafePositionPreference, 0f, 40f);
            profile.MapWallPressurePreference = Mathf.Clamp(profile.MapWallPressurePreference, 0f, 35f);
            profile.MapWallHugPenalty = Mathf.Clamp(profile.MapWallHugPenalty, 0f, 35f);
            profile.MapEscapeSpacePreference = Mathf.Clamp(profile.MapEscapeSpacePreference, 0f, 30f);
            profile.MapCoverDancePreference = Mathf.Clamp(profile.MapCoverDancePreference, 0f, 30f);
            profile.MapFireLanePressurePreference = Mathf.Clamp(profile.MapFireLanePressurePreference, 0f, 35f);
            profile.MapThrowerSpacingPreference = Mathf.Clamp(profile.MapThrowerSpacingPreference, 0f, 35f);

            profile.DangerScanRadius = Mathf.Clamp(profile.DangerScanRadius, 3f, 12f);
            profile.DangerPersonalSpace = Mathf.Clamp(profile.DangerPersonalSpace, 0.1f, 1.5f);
            profile.DangerReactionTimeSeconds = Mathf.Clamp(profile.DangerReactionTimeSeconds, 0.20f, 1.75f);
            profile.DangerEvadePressureThreshold = Mathf.Clamp(profile.DangerEvadePressureThreshold, 0.05f, 0.85f);
            profile.DangerEvadeScoreBonus = Mathf.Clamp(profile.DangerEvadeScoreBonus, 10f, 110f);
            profile.DangerEvadeDistance = Mathf.Clamp(profile.DangerEvadeDistance, 0.75f, 5f);
            profile.DangerRefreshIntervalTicks = ClampTicks(profile.DangerRefreshIntervalTicks, 1, 12);
            profile.DangerEvadeRetargetTicks = ClampTicks(profile.DangerEvadeRetargetTicks, 1, 20);
            profile.DangerThreatStaleDistance = Mathf.Clamp(profile.DangerThreatStaleDistance, 0.25f, 3f);
            profile.DangerMapSearchRadius = Mathf.Clamp(profile.DangerMapSearchRadius, 0.5f, 3f);

            profile.NavigationStuckSampleLimit = Mathf.Clamp(profile.NavigationStuckSampleLimit, 1, 5);
            profile.NavigationStuckSampleIntervalTicks = ClampTicks(profile.NavigationStuckSampleIntervalTicks, 3, 30);
            profile.NavigationStuckMoveThreshold = Mathf.Clamp(profile.NavigationStuckMoveThreshold, 0.03f, 0.35f);
            profile.BlockedRouteRecoveryLimit = Mathf.Clamp(profile.BlockedRouteRecoveryLimit, 1, 4);
            profile.StaleDestinationRecoveryTicks = ClampTicks(profile.StaleDestinationRecoveryTicks, 20, 240);
            profile.StaleDestinationProgressThreshold = Mathf.Clamp(profile.StaleDestinationProgressThreshold, 0.1f, 3f);
            profile.FailureRecoveryCooldownTicks = ClampTicks(profile.FailureRecoveryCooldownTicks, 4, 90);
            profile.FailureRecoveryDetourDistance = Mathf.Clamp(profile.FailureRecoveryDetourDistance, 0.75f, 4f);
            profile.FailedCastMemoryTicks = ClampTicks(profile.FailedCastMemoryTicks, 5, 240);
            profile.FailedCastRecoveryLimit = Mathf.Clamp(profile.FailedCastRecoveryLimit, 1, 5);
            profile.FailedCastSuppressionTicks = ClampTicks(profile.FailedCastSuppressionTicks, 5, 180);

            profile.DebugSnapshotIntervalTicks = ClampTicks(profile.DebugSnapshotIntervalTicks, 1, 60);
            profile.MaxMapResolvesPerTick = Mathf.Clamp(profile.MaxMapResolvesPerTick, 1, 256);
            profile.MaxPathQueriesPerTick = Mathf.Clamp(profile.MaxPathQueriesPerTick, 1, 128);
            profile.MaxPathTouchedNodesPerTick = Mathf.Clamp(profile.MaxPathTouchedNodesPerTick, 64, 50000);

            profile.HumanizationReactionJitterTicks =
                ClampTicks(profile.HumanizationReactionJitterTicks, 0, 8);
            profile.HumanizationActionScoreJitter =
                Mathf.Clamp(profile.HumanizationActionScoreJitter, 0f, 6f);
            profile.HumanizationFakeOutChance =
                Mathf.Clamp01(profile.HumanizationFakeOutChance);
            profile.HumanizationFakeOutScoreBonus =
                Mathf.Clamp(profile.HumanizationFakeOutScoreBonus, 0f, 16f);
            profile.HumanizationFakeOutDurationTicks =
                ClampTicks(profile.HumanizationFakeOutDurationTicks, 1, 30);
            profile.HumanizationFakeOutCooldownTicks =
                ClampTicks(profile.HumanizationFakeOutCooldownTicks, 15, 240);
            profile.HumanizationPressureMistakeChance =
                Mathf.Clamp01(profile.HumanizationPressureMistakeChance);
            profile.HumanizationPressureMistakePenalty =
                Mathf.Clamp(profile.HumanizationPressureMistakePenalty, 0f, 24f);
            profile.HumanizationPressureHealthThreshold =
                Mathf.Clamp(profile.HumanizationPressureHealthThreshold, 0.15f, 0.80f);
            profile.HumanizationPressureMistakeDurationTicks =
                ClampTicks(profile.HumanizationPressureMistakeDurationTicks, 1, 30);
            profile.HumanizationPressureMistakeCooldownTicks =
                ClampTicks(profile.HumanizationPressureMistakeCooldownTicks, 20, 300);
            profile.HumanizationPersonalityExpression =
                Mathf.Clamp(profile.HumanizationPersonalityExpression, 0f, 2f);
        }

        private static void EnsureTacticalStabilizationDefaults(BrawlerAIProfile profile)
        {
            if (profile.TacticalDirectionFlipCooldownTicks == 0u)
                profile.TacticalDirectionFlipCooldownTicks = 24u;

            if (profile.TacticalDestinationSwitchDistance <= 0f)
                profile.TacticalDestinationSwitchDistance = 1.2f;

            if (profile.TacticalDestinationBlend <= 0f)
                profile.TacticalDestinationBlend = 0.55f;

            if (profile.AIMoveInputTurnRateDegreesPerTick <= 0f)
                profile.AIMoveInputTurnRateDegreesPerTick = 28f;

            if (profile.AIHighPriorityMoveInputTurnRateDegreesPerTick <= 0f)
                profile.AIHighPriorityMoveInputTurnRateDegreesPerTick = 80f;

            if (profile.NavigationStuckSampleIntervalTicks == 0u)
                profile.NavigationStuckSampleIntervalTicks = 8u;

            if (profile.NavigationStuckMoveThreshold <= 0f)
                profile.NavigationStuckMoveThreshold = 0.08f;
        }

        private static void EnsureMapGeometryDefaults(BrawlerAIProfile profile)
        {
            float wallHugPenalty = 9f;
            float escapeSpace = 5f;
            float coverDance = 5f;
            float fireLane = 5f;
            float throwerSpacing = 0f;

            switch (profile.Archetype)
            {
                case BrawlerArchetype.Sniper:
                    wallHugPenalty = 12f;
                    escapeSpace = 7f;
                    coverDance = 8f;
                    fireLane = 11f;
                    break;

                case BrawlerArchetype.Tank:
                    wallHugPenalty = 5f;
                    escapeSpace = 4f;
                    coverDance = 2f;
                    fireLane = 3f;
                    break;

                case BrawlerArchetype.Assassin:
                    wallHugPenalty = 8f;
                    escapeSpace = 6f;
                    coverDance = 7f;
                    fireLane = 5f;
                    break;

                case BrawlerArchetype.Support:
                    wallHugPenalty = 11f;
                    escapeSpace = 8f;
                    coverDance = 6f;
                    fireLane = 5f;
                    break;

                case BrawlerArchetype.Controller:
                    wallHugPenalty = 9f;
                    escapeSpace = 6f;
                    coverDance = 7f;
                    fireLane = 8f;
                    break;

                case BrawlerArchetype.Artillery:
                    wallHugPenalty = 11f;
                    escapeSpace = 7f;
                    coverDance = 5f;
                    fireLane = 0f;
                    throwerSpacing = 13f;
                    break;
            }

            if (profile.MapWallHugPenalty <= 0f)
                profile.MapWallHugPenalty = wallHugPenalty;

            if (profile.MapEscapeSpacePreference <= 0f)
                profile.MapEscapeSpacePreference = escapeSpace;

            if (profile.MapCoverDancePreference <= 0f)
                profile.MapCoverDancePreference = coverDance;

            if (profile.MapFireLanePressurePreference <= 0f && fireLane > 0f)
                profile.MapFireLanePressurePreference = fireLane;

            if (profile.MapThrowerSpacingPreference <= 0f && throwerSpacing > 0f)
                profile.MapThrowerSpacingPreference = throwerSpacing;
        }

        private static void EnsureGemGrabObjectiveDefaults(BrawlerAIProfile profile)
        {
            if (profile.GemPickupSearchRadius <= 0f)
                profile.GemPickupSearchRadius = 11f;

            if (profile.GemPickupBaseScore <= 0f)
                profile.GemPickupBaseScore = 42f;

            if (profile.GemPickupValueScore <= 0f)
                profile.GemPickupValueScore = 8f;

            if (profile.GemPickupCloseRangeBonus <= 0f)
                profile.GemPickupCloseRangeBonus = 22f;

            if (profile.GemPickupClusterRadius <= 0f)
                profile.GemPickupClusterRadius = 1.75f;

            if (profile.GemPickupMinimumScore <= 0f)
                profile.GemPickupMinimumScore = 30f;

            if (profile.GemPickupSecureThresholdBonus <= 0f)
                profile.GemPickupSecureThresholdBonus = 34f;

            if (profile.GemPickupDenyThresholdBonus <= 0f)
                profile.GemPickupDenyThresholdBonus = 30f;

            if (profile.GemPickupCountdownResetBonus <= 0f)
                profile.GemPickupCountdownResetBonus = 38f;

            if (profile.GemPickupCarrierSafetyPenalty <= 0f)
                profile.GemPickupCarrierSafetyPenalty = 10f;

            if (profile.GemPickupThreatRadius <= 0f)
                profile.GemPickupThreatRadius = 4.5f;

            if (profile.GemPickupThreatPenalty <= 0f)
                profile.GemPickupThreatPenalty = 24f;
        }

        private static void EnsureLaneDisciplineDefaults(BrawlerAIProfile profile)
        {
            if (profile.LaneDisciplineWeight <= 0f)
                profile.LaneDisciplineWeight = 1f;

            if (profile.LaneHoldObjectiveBonus <= 0f)
                profile.LaneHoldObjectiveBonus = 10f;

            if (profile.LaneHoldSearchScore <= 0f)
                profile.LaneHoldSearchScore = 18f;

            if (profile.LaneHoldSearchRadius <= 0f)
                profile.LaneHoldSearchRadius = 7f;

            if (profile.LaneSideOffset <= 0f)
                profile.LaneSideOffset = 5f;

            if (profile.LowHealthChaseHealthThreshold <= 0f)
                profile.LowHealthChaseHealthThreshold = 0.32f;

            if (profile.LowHealthChaseMaxDistance <= 0f)
                profile.LowHealthChaseMaxDistance = 8.5f;

            if (profile.LowHealthChaseApproachBonus <= 0f)
                profile.LowHealthChaseApproachBonus = 28f;

            if (profile.UnsafeChasePenalty <= 0f)
                profile.UnsafeChasePenalty = 30f;

            if (profile.LowHealthChaseCommitTicks == 0u)
                profile.LowHealthChaseCommitTicks = 18u;

            if (profile.LowHealthChaseMaxTicks == 0u)
                profile.LowHealthChaseMaxTicks = 90u;

            if (profile.LowHealthChaseCooldownTicks == 0u)
                profile.LowHealthChaseCooldownTicks = 28u;

            if (profile.LowHealthChaseBreakDistanceMultiplier <= 0f)
                profile.LowHealthChaseBreakDistanceMultiplier = 1.35f;

            if (profile.ChaseCommitScoreBonus <= 0f)
                profile.ChaseCommitScoreBonus = 10f;

            if (profile.ChaseDisengageScorePenalty <= 0f)
                profile.ChaseDisengageScorePenalty = 42f;

            if (profile.BadMapChasePenalty <= 0f)
                profile.BadMapChasePenalty = 24f;
        }

        private static void ApplyFairPlayGuardrails(BrawlerAIProfile profile)
        {
            if (profile.Difficulty == AIDifficultyLevel.Hard)
            {
                profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 1f);
                profile.CombatSenseIntervalTicks = ClampTicks(profile.CombatSenseIntervalTicks, 2, 30);
                profile.DangerRefreshIntervalTicks = ClampTicks(profile.DangerRefreshIntervalTicks, 2, 12);
            }
            else if (profile.Difficulty == AIDifficultyLevel.Normal)
            {
                profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 2f);
            }
            else
            {
                profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 6f);
                profile.ReactionDelayTicks = ClampTicks(profile.ReactionDelayTicks, 6, 24);
            }

            if (profile.UseTeamRoleCoordination)
            {
                profile.TeamRoleCoordinationWeight = Mathf.Max(profile.TeamRoleCoordinationWeight, 0.65f);
                profile.TeamActionCrowdingPenalty = Mathf.Max(profile.TeamActionCrowdingPenalty, 6f);
            }

            profile.OverFocusedTargetPenaltyPerAlly =
                Mathf.Max(profile.OverFocusedTargetPenaltyPerAlly, 8f);
            profile.MaxOverFocusedTargetPenalty = Mathf.Max(
                profile.MaxOverFocusedTargetPenalty,
                profile.OverFocusedTargetPenaltyPerAlly * 1.75f);
        }

        private static uint ScaleTicks(uint value, float multiplier, uint minimum)
        {
            return (uint)Mathf.Max(minimum, Mathf.RoundToInt(value * multiplier));
        }

        private static uint ClampTicks(uint value, uint min, uint max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
