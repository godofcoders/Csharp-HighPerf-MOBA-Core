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
            AITuningCatalog tuningCatalog,
            AIBotPerformanceTier performanceTier = AIBotPerformanceTier.Custom)
        {
            if (profile == null)
                return;

            profile.Difficulty = difficulty;
            profile.Personality = personality;

            EnsureTacticalStabilizationDefaults(profile);
            EnsureTargetContextDefaults(profile);
            EnsureGemGrabObjectiveDefaults(profile);
            EnsureLaneDisciplineDefaults(profile);
            EnsureMapGeometryDefaults(profile);
            EnsureDangerAvoidanceDefaults(profile);
            EnsureOpponentResourceAwarenessDefaults(profile);
            EnsureAdvancedTeamFightDefaults(profile);
            EnsureRoleMacroDefaults(profile);
            EnsureEngagementRiskDefaults(profile);
            EnsurePressureRotationDefaults(profile);
            EnsureDecisionConfidenceDefaults(profile);
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

            ApplyPerformanceTierOverlay(profile, performanceTier);
            Normalize(profile);
            ApplyFairPlayGuardrails(profile);
            Normalize(profile);
        }

        public static void RebuildRuntimeTuning(
            BrawlerAIProfile sourceProfile,
            BrawlerAIProfile runtimeProfile,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            AITuningCatalog tuningCatalog,
            AIBotPerformanceTier performanceTier = AIBotPerformanceTier.Custom)
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
                tuningCatalog,
                performanceTier);
        }

        public static string GetPerformanceTierDebugSummary(
            AIBotPerformanceTier performanceTier,
            BrawlerAIProfile profile)
        {
            if (profile == null)
                return $"Tier={performanceTier} Profile=None";

            return
                $"Tier={performanceTier} " +
                $"React={profile.ReactionDelayTicks}+{profile.HumanizationReactionJitterTicks}t " +
                $"AimErr={profile.AimErrorDegrees:0.0} " +
                $"Sense={profile.IdleSenseIntervalTicks}/{profile.CombatSenseIntervalTicks} " +
                $"Obj={profile.ObjectiveWeight:0.00} Team={profile.TeamRoleCoordinationWeight:0.00} " +
                $"TgtCtx={profile.TargetContextAwarenessWeight:0.00} " +
                $"Focus={profile.FocusFireWeight:0.0} Map={profile.MapOpenShotPreference:0.0}/{profile.MapCoverDancePreference:0.0} " +
                $"Res={profile.OpponentResourceAwarenessWeight:0.00} " +
                $"Fight={profile.TeamFightCoordinationWeight:0.00} " +
                $"Role={profile.RoleMacroBehaviorWeight:0.00} " +
                $"Risk={profile.EngagementRiskAwarenessWeight:0.00} " +
                $"Rot={profile.PressureRotationAwarenessWeight:0.00} " +
                $"Conf={profile.DecisionAmbiguityScoreWindow:0.0}/{profile.DecisionAmbiguitySwitchPenalty:0.0} " +
                $"Move={profile.AIMoveSpeedScale:0.00}/{profile.AIMoveInputTurnRateDegreesPerTick:0} " +
                $"Mistake={profile.HumanizationPressureMistakeChance:0.00}";
        }

        private static void ApplyPerformanceTierOverlay(
            BrawlerAIProfile profile,
            AIBotPerformanceTier performanceTier)
        {
            switch (performanceTier)
            {
                case AIBotPerformanceTier.Amateur:
                    profile.ReactionDelayTicks = ClampTicks(profile.ReactionDelayTicks + 4u, 8u, 24u);
                    profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 11.5f);
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 1.20f, 4u);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 1.25f, 2u);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 1.12f, 1u);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 1.20f, 1u);
                    profile.MinimumCommittedActionScore *= 1.12f;
                    profile.ActionSwitchScoreMargin *= 1.18f;
                    profile.DecisionAmbiguityScoreWindow *= 1.35f;
                    profile.DecisionAmbiguitySwitchPenalty *= 1.35f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 1.45f, 1u);
                    profile.CombatActionCommitmentTicks = ScaleTicks(profile.CombatActionCommitmentTicks, 1.15f, 1u);
                    profile.NonCombatActionCommitmentTicks = ScaleTicks(profile.NonCombatActionCommitmentTicks, 1.18f, 1u);
                    profile.ObjectiveWeight *= 0.78f;
                    profile.MacroActionBiasWeight *= 0.75f;
                    profile.FocusFireWeight *= 0.78f;
                    profile.LowHealthTargetBias *= 0.76f;
                    profile.FinisherBonus *= 0.75f;
                    profile.GemPickupMinimumScore *= 1.18f;
                    profile.GemPickupSecureThresholdBonus *= 0.76f;
                    profile.GemPickupDenyThresholdBonus *= 0.76f;
                    profile.LaneDisciplineWeight *= 0.78f;
                    profile.TeamRoleCoordinationWeight *= 0.72f;
                    profile.TeamActionCrowdingPenalty *= 0.72f;
                    profile.OverFocusedTargetPenaltyPerAlly *= 0.70f;
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 1.25f, 1u);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 1.20f, 1u);
                    profile.TacticalDestinationBlend *= 0.82f;
                    profile.TacticalDestinationSwitchDistance *= 1.18f;
                    profile.AIMoveInputTurnRateDegreesPerTick *= 0.78f;
                    profile.AIHighPriorityMoveInputTurnRateDegreesPerTick *= 0.82f;
                    profile.AIMoveSpeedScale *= 0.88f;
                    profile.AIHighPriorityMoveSpeedScale *= 0.90f;
                    profile.DangerScanRadius *= 0.82f;
                    profile.DangerEvadeScoreBonus *= 0.78f;
                    profile.DangerRefreshIntervalTicks = ScaleTicks(profile.DangerRefreshIntervalTicks, 1.25f, 1u);
                    profile.MapOpenShotPreference *= 0.74f;
                    profile.MapCoverDancePreference *= 0.72f;
                    profile.MapEscapeSpacePreference *= 0.74f;
                    profile.MapFireLanePressurePreference *= 0.74f;
                    profile.MapThrowerSpacingPreference *= 0.74f;
                    if (profile.HumanizationReactionJitterTicks < 6u)
                        profile.HumanizationReactionJitterTicks = 6u;
                    profile.HumanizationActionScoreJitter = Mathf.Max(profile.HumanizationActionScoreJitter, 4.8f);
                    profile.HumanizationFakeOutChance = Mathf.Max(profile.HumanizationFakeOutChance, 0.16f);
                    profile.HumanizationPressureMistakeChance = Mathf.Max(profile.HumanizationPressureMistakeChance, 0.14f);
                    profile.HumanizationPressureMistakePenalty *= 1.25f;
                    profile.OpponentResourceAwarenessWeight *= 0.42f;
                    profile.EnemyLowAmmoOpportunityBonus *= 0.62f;
                    profile.EnemyNoAttackApproachBonus *= 0.58f;
                    profile.EnemySuperReadyThreatPenalty *= 0.62f;
                    profile.EnemyNearlySuperThreatPenalty *= 0.58f;
                    profile.EnemySuperRespectBonus *= 0.62f;
                    profile.TeamFightCoordinationWeight *= 0.50f;
                    profile.TeamCollapseFocusBonus *= 0.62f;
                    profile.TeamFlankRepositionBonus *= 0.62f;
                    profile.TeamBaitHoldBonus *= 0.70f;
                    profile.TeamPeelAssistBonus *= 0.68f;
                    profile.TeamOvercommitApproachPenalty *= 0.58f;
                    profile.TargetContextAwarenessWeight *= 0.42f;
                    profile.IsolatedTargetBonus *= 0.62f;
                    profile.ProtectedTargetPenalty *= 0.62f;
                    profile.AllyCollapseTargetBonus *= 0.62f;
                    profile.RoleMacroBehaviorWeight *= 0.48f;
                    profile.RoleTankSpaceCreationBonus *= 0.62f;
                    profile.RoleBacklineAnchorMacroBonus *= 0.62f;
                    profile.RoleSupportPeelMacroBonus *= 0.62f;
                    profile.RoleAssassinPickPressureBonus *= 0.60f;
                    profile.RoleControllerZoneMacroBonus *= 0.62f;
                    profile.RoleArtilleryDenialMacroBonus *= 0.62f;
                    profile.RoleFighterFlexMacroBonus *= 0.64f;
                    profile.EngagementRiskAwarenessWeight *= 0.42f;
                    profile.OutnumberedApproachPenalty *= 0.58f;
                    profile.SupportedFightCommitBonus *= 0.62f;
                    profile.BadDiveRepositionBonus *= 0.58f;
                    profile.EngagementRiskSafetyBonus *= 0.58f;
                    profile.PressureRotationAwarenessWeight *= 0.45f;
                    profile.EnemyHotspotRotationBonus *= 0.62f;
                    profile.ThreatCenterRotationBonus *= 0.62f;
                    profile.ThreatCenterDivePenalty *= 0.58f;
                    break;

                case AIBotPerformanceTier.Veteran:
                    if (profile.ReactionDelayTicks > 1u)
                        profile.ReactionDelayTicks = 1u;
                    profile.AimErrorDegrees = Mathf.Min(profile.AimErrorDegrees, 1.25f);
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 0.85f, 2u);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 0.85f, 2u);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 0.95f, 1u);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 0.90f, 1u);
                    profile.MinimumCommittedActionScore *= 0.92f;
                    profile.ActionSwitchScoreMargin *= 0.92f;
                    profile.DecisionAmbiguityScoreWindow *= 0.82f;
                    profile.DecisionAmbiguitySwitchPenalty *= 0.82f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 0.75f, 1u);
                    profile.ObjectiveWeight *= 1.14f;
                    profile.MacroActionBiasWeight *= 1.12f;
                    profile.FocusFireWeight *= 1.12f;
                    profile.LowHealthTargetBias *= 1.10f;
                    profile.FinisherBonus *= 1.10f;
                    profile.GemPickupMinimumScore *= 0.92f;
                    profile.GemPickupSecureThresholdBonus *= 1.12f;
                    profile.GemPickupDenyThresholdBonus *= 1.12f;
                    profile.LaneDisciplineWeight *= 1.10f;
                    profile.TeamRoleCoordinationWeight *= 1.14f;
                    profile.TeamActionCrowdingPenalty *= 1.08f;
                    profile.OverFocusedTargetPenaltyPerAlly *= 1.10f;
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 0.88f, 1u);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 0.90f, 1u);
                    profile.TacticalDestinationBlend *= 1.08f;
                    profile.AIMoveInputTurnRateDegreesPerTick *= 1.06f;
                    profile.AIHighPriorityMoveInputTurnRateDegreesPerTick *= 1.06f;
                    profile.DangerScanRadius *= 1.08f;
                    profile.DangerEvadeScoreBonus *= 1.08f;
                    profile.DangerRefreshIntervalTicks = ScaleTicks(profile.DangerRefreshIntervalTicks, 0.90f, 1u);
                    profile.MapOpenShotPreference *= 1.12f;
                    profile.MapCoverDancePreference *= 1.12f;
                    profile.MapEscapeSpacePreference *= 1.10f;
                    profile.MapFireLanePressurePreference *= 1.12f;
                    profile.MapThrowerSpacingPreference *= 1.10f;
                    if (profile.HumanizationReactionJitterTicks > 1u)
                        profile.HumanizationReactionJitterTicks = 1u;
                    profile.HumanizationActionScoreJitter = Mathf.Min(profile.HumanizationActionScoreJitter, 0.8f);
                    profile.HumanizationFakeOutChance *= 0.72f;
                    profile.HumanizationPressureMistakeChance *= 0.68f;
                    profile.OpponentResourceAwarenessWeight *= 1.24f;
                    profile.EnemyLowAmmoOpportunityBonus *= 1.16f;
                    profile.EnemyNoAttackApproachBonus *= 1.16f;
                    profile.EnemySuperReadyThreatPenalty *= 1.16f;
                    profile.EnemyNearlySuperThreatPenalty *= 1.12f;
                    profile.EnemySuperRespectBonus *= 1.18f;
                    profile.TeamFightCoordinationWeight *= 1.20f;
                    profile.TeamCollapseFocusBonus *= 1.14f;
                    profile.TeamFlankRepositionBonus *= 1.14f;
                    profile.TeamBaitHoldBonus *= 1.10f;
                    profile.TeamPeelAssistBonus *= 1.12f;
                    profile.TeamOvercommitApproachPenalty *= 1.12f;
                    profile.TargetContextAwarenessWeight *= 1.18f;
                    profile.IsolatedTargetBonus *= 1.12f;
                    profile.ProtectedTargetPenalty *= 1.12f;
                    profile.AllyCollapseTargetBonus *= 1.12f;
                    profile.RoleMacroBehaviorWeight *= 1.18f;
                    profile.RoleTankSpaceCreationBonus *= 1.12f;
                    profile.RoleBacklineAnchorMacroBonus *= 1.12f;
                    profile.RoleSupportPeelMacroBonus *= 1.12f;
                    profile.RoleAssassinPickPressureBonus *= 1.12f;
                    profile.RoleControllerZoneMacroBonus *= 1.12f;
                    profile.RoleArtilleryDenialMacroBonus *= 1.12f;
                    profile.RoleFighterFlexMacroBonus *= 1.10f;
                    profile.EngagementRiskAwarenessWeight *= 1.18f;
                    profile.OutnumberedApproachPenalty *= 1.12f;
                    profile.SupportedFightCommitBonus *= 1.12f;
                    profile.BadDiveRepositionBonus *= 1.12f;
                    profile.EngagementRiskSafetyBonus *= 1.10f;
                    profile.PressureRotationAwarenessWeight *= 1.16f;
                    profile.EnemyHotspotRotationBonus *= 1.12f;
                    profile.ThreatCenterRotationBonus *= 1.12f;
                    profile.ThreatCenterDivePenalty *= 1.10f;
                    break;

                case AIBotPerformanceTier.Elite:
                    profile.ReactionDelayTicks = 0u;
                    profile.AimErrorDegrees = Mathf.Min(profile.AimErrorDegrees, 1.0f);
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 0.72f, 1u);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 0.72f, 2u);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 0.88f, 1u);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 0.78f, 1u);
                    profile.MinimumCommittedActionScore *= 0.84f;
                    profile.ActionSwitchScoreMargin *= 0.82f;
                    profile.DecisionAmbiguityScoreWindow *= 0.65f;
                    profile.DecisionAmbiguitySwitchPenalty *= 0.60f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 0.55f, 1u);
                    profile.CombatActionCommitmentTicks = ScaleTicks(profile.CombatActionCommitmentTicks, 0.82f, 1u);
                    profile.NonCombatActionCommitmentTicks = ScaleTicks(profile.NonCombatActionCommitmentTicks, 0.86f, 1u);
                    profile.ObjectiveWeight *= 1.18f;
                    profile.MacroActionBiasWeight *= 1.16f;
                    profile.FocusFireWeight *= 1.22f;
                    profile.LowHealthTargetBias *= 1.20f;
                    profile.FinisherBonus *= 1.22f;
                    profile.GemPickupMinimumScore *= 0.86f;
                    profile.GemPickupSecureThresholdBonus *= 1.18f;
                    profile.GemPickupDenyThresholdBonus *= 1.18f;
                    profile.LaneDisciplineWeight *= 1.04f;
                    profile.TeamRoleCoordinationWeight *= 1.08f;
                    profile.TeamActionCrowdingPenalty *= 1.05f;
                    profile.OverFocusedTargetPenaltyPerAlly *= 1.16f;
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 0.78f, 1u);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 0.82f, 1u);
                    profile.TacticalDestinationBlend *= 1.14f;
                    profile.TacticalDestinationSwitchDistance *= 0.90f;
                    profile.AIMoveInputTurnRateDegreesPerTick *= 1.12f;
                    profile.AIHighPriorityMoveInputTurnRateDegreesPerTick *= 1.12f;
                    profile.DangerScanRadius *= 1.14f;
                    profile.DangerEvadePressureThreshold *= 0.90f;
                    profile.DangerEvadeScoreBonus *= 1.16f;
                    profile.DangerRefreshIntervalTicks = ScaleTicks(profile.DangerRefreshIntervalTicks, 0.82f, 1u);
                    profile.MapOpenShotPreference *= 1.20f;
                    profile.MapCoverDancePreference *= 1.18f;
                    profile.MapEscapeSpacePreference *= 1.16f;
                    profile.MapFireLanePressurePreference *= 1.20f;
                    profile.MapThrowerSpacingPreference *= 1.16f;
                    profile.HumanizationReactionJitterTicks = 0u;
                    profile.HumanizationActionScoreJitter = Mathf.Min(profile.HumanizationActionScoreJitter, 0.45f);
                    profile.HumanizationFakeOutChance *= 0.55f;
                    profile.HumanizationPressureMistakeChance *= 0.45f;
                    profile.OpponentResourceAwarenessWeight *= 1.45f;
                    profile.EnemyLowAmmoOpportunityBonus *= 1.28f;
                    profile.EnemyNoAttackApproachBonus *= 1.30f;
                    profile.EnemySuperReadyThreatPenalty *= 1.28f;
                    profile.EnemyNearlySuperThreatPenalty *= 1.20f;
                    profile.EnemySuperRespectBonus *= 1.30f;
                    profile.TeamFightCoordinationWeight *= 1.36f;
                    profile.TeamCollapseFocusBonus *= 1.24f;
                    profile.TeamFlankRepositionBonus *= 1.22f;
                    profile.TeamBaitHoldBonus *= 1.18f;
                    profile.TeamPeelAssistBonus *= 1.20f;
                    profile.TeamOvercommitApproachPenalty *= 1.22f;
                    profile.TargetContextAwarenessWeight *= 1.34f;
                    profile.IsolatedTargetBonus *= 1.22f;
                    profile.ProtectedTargetPenalty *= 1.22f;
                    profile.AllyCollapseTargetBonus *= 1.24f;
                    profile.RoleMacroBehaviorWeight *= 1.34f;
                    profile.RoleTankSpaceCreationBonus *= 1.24f;
                    profile.RoleBacklineAnchorMacroBonus *= 1.22f;
                    profile.RoleSupportPeelMacroBonus *= 1.24f;
                    profile.RoleAssassinPickPressureBonus *= 1.25f;
                    profile.RoleControllerZoneMacroBonus *= 1.24f;
                    profile.RoleArtilleryDenialMacroBonus *= 1.22f;
                    profile.RoleFighterFlexMacroBonus *= 1.18f;
                    profile.EngagementRiskAwarenessWeight *= 1.34f;
                    profile.OutnumberedApproachPenalty *= 1.24f;
                    profile.SupportedFightCommitBonus *= 1.22f;
                    profile.BadDiveRepositionBonus *= 1.24f;
                    profile.EngagementRiskSafetyBonus *= 1.20f;
                    profile.PressureRotationAwarenessWeight *= 1.32f;
                    profile.EnemyHotspotRotationBonus *= 1.22f;
                    profile.ThreatCenterRotationBonus *= 1.24f;
                    profile.ThreatCenterDivePenalty *= 1.20f;
                    break;

                case AIBotPerformanceTier.Regular:
                case AIBotPerformanceTier.Custom:
                default:
                    break;
            }
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
                    profile.DecisionAmbiguityScoreWindow *= 1.20f;
                    profile.DecisionAmbiguitySwitchPenalty *= 1.20f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 1.25f, 1);
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
                    profile.AIMoveSpeedScale *= 0.90f;
                    profile.AIHighPriorityMoveSpeedScale *= 0.92f;
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
                    profile.OpponentResourceAwarenessWeight *= 0.58f;
                    profile.EnemyLowAmmoOpportunityBonus *= 0.72f;
                    profile.EnemyNoAttackApproachBonus *= 0.70f;
                    profile.EnemySuperReadyThreatPenalty *= 0.72f;
                    profile.EnemyNearlySuperThreatPenalty *= 0.70f;
                    profile.EnemySuperRespectBonus *= 0.75f;
                    profile.TeamFightCoordinationWeight *= 0.72f;
                    profile.TeamCollapseFocusBonus *= 0.78f;
                    profile.TeamFlankRepositionBonus *= 0.80f;
                    profile.TeamPeelAssistBonus *= 0.80f;
                    profile.TargetContextAwarenessWeight *= 0.58f;
                    profile.IsolatedTargetBonus *= 0.70f;
                    profile.ProtectedTargetPenalty *= 0.68f;
                    profile.AllyCollapseTargetBonus *= 0.72f;
                    profile.RoleMacroBehaviorWeight *= 0.70f;
                    profile.RoleTankSpaceCreationBonus *= 0.78f;
                    profile.RoleBacklineAnchorMacroBonus *= 0.78f;
                    profile.RoleSupportPeelMacroBonus *= 0.78f;
                    profile.RoleAssassinPickPressureBonus *= 0.76f;
                    profile.RoleControllerZoneMacroBonus *= 0.78f;
                    profile.RoleArtilleryDenialMacroBonus *= 0.78f;
                    profile.EngagementRiskAwarenessWeight *= 0.68f;
                    profile.OutnumberedApproachPenalty *= 0.75f;
                    profile.SupportedFightCommitBonus *= 0.78f;
                    profile.BadDiveRepositionBonus *= 0.75f;
                    profile.PressureRotationAwarenessWeight *= 0.70f;
                    profile.EnemyHotspotRotationBonus *= 0.78f;
                    profile.ThreatCenterRotationBonus *= 0.78f;
                    profile.ThreatCenterDivePenalty *= 0.74f;
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
                    profile.DecisionAmbiguityScoreWindow *= 0.78f;
                    profile.DecisionAmbiguitySwitchPenalty *= 0.78f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 0.75f, 1);
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
                    profile.AIMoveSpeedScale *= 1.04f;
                    profile.AIHighPriorityMoveSpeedScale *= 1.04f;
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
                    profile.OpponentResourceAwarenessWeight *= 1.15f;
                    profile.EnemyLowAmmoOpportunityBonus *= 1.10f;
                    profile.EnemyNoAttackApproachBonus *= 1.12f;
                    profile.EnemySuperReadyThreatPenalty *= 1.12f;
                    profile.EnemyNearlySuperThreatPenalty *= 1.10f;
                    profile.EnemySuperRespectBonus *= 1.12f;
                    profile.TeamFightCoordinationWeight *= 1.10f;
                    profile.TeamCollapseFocusBonus *= 1.08f;
                    profile.TeamFlankRepositionBonus *= 1.08f;
                    profile.TeamPeelAssistBonus *= 1.10f;
                    profile.TeamOvercommitApproachPenalty *= 1.08f;
                    profile.TargetContextAwarenessWeight *= 1.12f;
                    profile.IsolatedTargetBonus *= 1.10f;
                    profile.ProtectedTargetPenalty *= 1.10f;
                    profile.AllyCollapseTargetBonus *= 1.12f;
                    profile.RoleMacroBehaviorWeight *= 1.10f;
                    profile.RoleTankSpaceCreationBonus *= 1.08f;
                    profile.RoleBacklineAnchorMacroBonus *= 1.08f;
                    profile.RoleSupportPeelMacroBonus *= 1.10f;
                    profile.RoleAssassinPickPressureBonus *= 1.10f;
                    profile.RoleControllerZoneMacroBonus *= 1.10f;
                    profile.RoleArtilleryDenialMacroBonus *= 1.08f;
                    profile.RoleFighterFlexMacroBonus *= 1.08f;
                    profile.EngagementRiskAwarenessWeight *= 1.10f;
                    profile.OutnumberedApproachPenalty *= 1.10f;
                    profile.SupportedFightCommitBonus *= 1.10f;
                    profile.BadDiveRepositionBonus *= 1.08f;
                    profile.EngagementRiskSafetyBonus *= 1.08f;
                    profile.PressureRotationAwarenessWeight *= 1.10f;
                    profile.EnemyHotspotRotationBonus *= 1.10f;
                    profile.ThreatCenterRotationBonus *= 1.10f;
                    profile.ThreatCenterDivePenalty *= 1.08f;
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
                    profile.DecisionAmbiguitySwitchPenalty *= 0.88f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 0.85f, 1);
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
                    profile.EnemyLowAmmoOpportunityBonus *= 1.15f;
                    profile.EnemyNoAttackApproachBonus *= 1.18f;
                    profile.EnemySuperReadyThreatPenalty *= 0.82f;
                    profile.EnemyNearlySuperThreatPenalty *= 0.85f;
                    profile.EnemySuperRespectBonus *= 0.85f;
                    profile.IsolatedTargetBonus *= 1.10f;
                    profile.ProtectedTargetPenalty *= 0.85f;
                    profile.AllyCollapseTargetBonus *= 1.08f;
                    profile.TeamCollapseFocusBonus *= 1.16f;
                    profile.TeamFlankRepositionBonus *= 1.08f;
                    profile.TeamOvercommitApproachPenalty *= 0.82f;
                    profile.RoleTankSpaceCreationBonus *= 1.12f;
                    profile.RoleAssassinPickPressureBonus *= 1.16f;
                    profile.RoleFighterFlexMacroBonus *= 1.10f;
                    profile.RoleBacklineAnchorMacroBonus *= 0.88f;
                    profile.RoleSupportPeelMacroBonus *= 0.90f;
                    profile.OutnumberedApproachPenalty *= 0.82f;
                    profile.SupportedFightCommitBonus *= 1.16f;
                    profile.BadDiveRepositionBonus *= 0.86f;
                    profile.EngagementRiskSafetyBonus *= 0.88f;
                    profile.EnemyHotspotRotationBonus *= 1.12f;
                    profile.ThreatCenterDivePenalty *= 0.86f;
                    break;

                case AIPersonalityType.Cautious:
                    profile.RetreatWeight *= 1.25f;
                    profile.HoldRangeWeight *= 1.15f;
                    profile.RepositionWeight *= 1.20f;
                    profile.ApproachWeight *= 0.82f;
                    profile.ActionSwitchScoreMargin *= 1.10f;
                    profile.DecisionAmbiguityScoreWindow *= 1.10f;
                    profile.DecisionAmbiguitySwitchPenalty *= 1.16f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 1.15f, 1);
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
                    profile.OpponentResourceAwarenessWeight *= 1.08f;
                    profile.EnemyLowAmmoOpportunityBonus *= 0.88f;
                    profile.EnemyNoAttackApproachBonus *= 0.88f;
                    profile.EnemySuperReadyThreatPenalty *= 1.18f;
                    profile.EnemyNearlySuperThreatPenalty *= 1.15f;
                    profile.EnemySuperRespectBonus *= 1.18f;
                    profile.TargetContextAwarenessWeight *= 1.08f;
                    profile.IsolatedTargetBonus *= 0.86f;
                    profile.ProtectedTargetPenalty *= 1.14f;
                    profile.AllyCollapseTargetBonus *= 0.92f;
                    profile.TeamBaitHoldBonus *= 1.18f;
                    profile.TeamPeelAssistBonus *= 1.16f;
                    profile.TeamOvercommitApproachPenalty *= 1.14f;
                    profile.RoleBacklineAnchorMacroBonus *= 1.16f;
                    profile.RoleSupportPeelMacroBonus *= 1.14f;
                    profile.RoleControllerZoneMacroBonus *= 1.08f;
                    profile.RoleArtilleryDenialMacroBonus *= 1.10f;
                    profile.RoleAssassinPickPressureBonus *= 0.82f;
                    profile.EngagementRiskAwarenessWeight *= 1.10f;
                    profile.OutnumberedApproachPenalty *= 1.18f;
                    profile.BadDiveRepositionBonus *= 1.16f;
                    profile.EngagementRiskSafetyBonus *= 1.14f;
                    profile.SupportedFightCommitBonus *= 0.88f;
                    profile.PressureRotationAwarenessWeight *= 1.08f;
                    profile.ThreatCenterRotationBonus *= 1.16f;
                    profile.ThreatCenterDivePenalty *= 1.18f;
                    break;

                case AIPersonalityType.TeamPlayer:
                    profile.PeelWeight *= 1.30f;
                    profile.RegroupWeight *= 1.22f;
                    profile.FocusFireWeight *= 1.15f;
                    profile.ObjectiveWeight *= 1.10f;
                    profile.MinimumCommittedActionScore *= 0.96f;
                    profile.ActionSwitchScoreMargin *= 0.95f;
                    profile.DecisionAmbiguitySwitchPenalty *= 0.92f;
                    profile.DecisionAmbiguityExtraHoldTicks =
                        ScaleTicks(profile.DecisionAmbiguityExtraHoldTicks, 0.90f, 1);
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
                    profile.OpponentResourceAwarenessWeight *= 1.12f;
                    profile.EnemyLowAmmoOpportunityBonus *= 1.06f;
                    profile.EnemyNoAttackApproachBonus *= 1.04f;
                    profile.EnemySuperReadyThreatPenalty *= 1.08f;
                    profile.EnemyNearlySuperThreatPenalty *= 1.08f;
                    profile.EnemySuperRespectBonus *= 1.12f;
                    profile.TargetContextAwarenessWeight *= 1.12f;
                    profile.AllyCollapseTargetBonus *= 1.16f;
                    profile.ProtectedTargetPenalty *= 1.06f;
                    profile.TeamFightCoordinationWeight *= 1.18f;
                    profile.TeamCollapseFocusBonus *= 1.12f;
                    profile.TeamFlankRepositionBonus *= 1.12f;
                    profile.TeamBaitHoldBonus *= 1.12f;
                    profile.TeamPeelAssistBonus *= 1.22f;
                    profile.TeamOvercommitApproachPenalty *= 1.14f;
                    profile.RoleMacroBehaviorWeight *= 1.12f;
                    profile.RoleSupportPeelMacroBonus *= 1.20f;
                    profile.RoleControllerZoneMacroBonus *= 1.14f;
                    profile.RoleBacklineAnchorMacroBonus *= 1.12f;
                    profile.RoleTankSpaceCreationBonus *= 1.08f;
                    profile.EngagementRiskAwarenessWeight *= 1.12f;
                    profile.SupportedFightCommitBonus *= 1.16f;
                    profile.BadDiveRepositionBonus *= 1.08f;
                    profile.EngagementRiskSafetyBonus *= 1.08f;
                    profile.PressureRotationAwarenessWeight *= 1.12f;
                    profile.EnemyHotspotRotationBonus *= 1.14f;
                    profile.ThreatCenterRotationBonus *= 1.10f;
                    break;

                case AIPersonalityType.Balanced:
                default:
                    break;
            }
        }

        private static void Normalize(BrawlerAIProfile profile)
        {
            EnsureTacticalStabilizationDefaults(profile);
            EnsureTargetContextDefaults(profile);
            EnsureGemGrabObjectiveDefaults(profile);
            EnsureLaneDisciplineDefaults(profile);
            EnsureMapGeometryDefaults(profile);
            EnsureDangerAvoidanceDefaults(profile);
            EnsureOpponentResourceAwarenessDefaults(profile);
            EnsureAdvancedTeamFightDefaults(profile);
            EnsureRoleMacroDefaults(profile);
            EnsureEngagementRiskDefaults(profile);
            EnsurePressureRotationDefaults(profile);
            EnsureDecisionConfidenceDefaults(profile);

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
            profile.TargetContextAwarenessWeight =
                Mathf.Clamp(profile.TargetContextAwarenessWeight, 0f, 2.5f);
            profile.IsolatedTargetBonus =
                Mathf.Clamp(profile.IsolatedTargetBonus, 0f, 45f);
            profile.ProtectedTargetPenalty =
                Mathf.Clamp(profile.ProtectedTargetPenalty, 0f, 55f);
            profile.AllyCollapseTargetBonus =
                Mathf.Clamp(profile.AllyCollapseTargetBonus, 0f, 45f);
            profile.TargetContextRadius =
                Mathf.Clamp(profile.TargetContextRadius, 2f, 8f);
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
            profile.CloseRangeCatchDistanceMultiplier =
                Mathf.Clamp(profile.CloseRangeCatchDistanceMultiplier, 1.25f, 4.0f);
            profile.CloseRangeOutrangedChasePenalty =
                Mathf.Clamp(profile.CloseRangeOutrangedChasePenalty, 0f, 90f);
            profile.CloseRangeCoverRepositionBonus =
                Mathf.Clamp(profile.CloseRangeCoverRepositionBonus, 0f, 70f);
            profile.CloseRangeEvasivePressureBonus =
                Mathf.Clamp(profile.CloseRangeEvasivePressureBonus, 0f, 40f);
            profile.OpponentResourceAwarenessWeight =
                Mathf.Clamp(profile.OpponentResourceAwarenessWeight, 0f, 2.5f);
            profile.EnemyLowAmmoOpportunityBonus =
                Mathf.Clamp(profile.EnemyLowAmmoOpportunityBonus, 0f, 45f);
            profile.EnemyNoAttackApproachBonus =
                Mathf.Clamp(profile.EnemyNoAttackApproachBonus, 0f, 35f);
            profile.EnemySuperReadyThreatPenalty =
                Mathf.Clamp(profile.EnemySuperReadyThreatPenalty, 0f, 55f);
            profile.EnemyNearlySuperThreatPenalty =
                Mathf.Clamp(profile.EnemyNearlySuperThreatPenalty, 0f, 30f);
            profile.EnemySuperRespectBonus =
                Mathf.Clamp(profile.EnemySuperRespectBonus, 0f, 45f);
            profile.TeamFightCoordinationWeight =
                Mathf.Clamp(profile.TeamFightCoordinationWeight, 0f, 2.5f);
            profile.TeamCollapseFocusBonus =
                Mathf.Clamp(profile.TeamCollapseFocusBonus, 0f, 45f);
            profile.TeamFlankRepositionBonus =
                Mathf.Clamp(profile.TeamFlankRepositionBonus, 0f, 40f);
            profile.TeamBaitHoldBonus =
                Mathf.Clamp(profile.TeamBaitHoldBonus, 0f, 35f);
            profile.TeamPeelAssistBonus =
                Mathf.Clamp(profile.TeamPeelAssistBonus, 0f, 45f);
            profile.TeamOvercommitApproachPenalty =
                Mathf.Clamp(profile.TeamOvercommitApproachPenalty, 0f, 45f);
            profile.RoleMacroBehaviorWeight =
                Mathf.Clamp(profile.RoleMacroBehaviorWeight, 0f, 2.5f);
            profile.RoleTankSpaceCreationBonus =
                Mathf.Clamp(profile.RoleTankSpaceCreationBonus, 0f, 45f);
            profile.RoleBacklineAnchorMacroBonus =
                Mathf.Clamp(profile.RoleBacklineAnchorMacroBonus, 0f, 45f);
            profile.RoleSupportPeelMacroBonus =
                Mathf.Clamp(profile.RoleSupportPeelMacroBonus, 0f, 45f);
            profile.RoleAssassinPickPressureBonus =
                Mathf.Clamp(profile.RoleAssassinPickPressureBonus, 0f, 45f);
            profile.RoleControllerZoneMacroBonus =
                Mathf.Clamp(profile.RoleControllerZoneMacroBonus, 0f, 45f);
            profile.RoleArtilleryDenialMacroBonus =
                Mathf.Clamp(profile.RoleArtilleryDenialMacroBonus, 0f, 45f);
            profile.RoleFighterFlexMacroBonus =
                Mathf.Clamp(profile.RoleFighterFlexMacroBonus, 0f, 45f);
            profile.EngagementRiskAwarenessWeight =
                Mathf.Clamp(profile.EngagementRiskAwarenessWeight, 0f, 2.5f);
            profile.EngagementRiskRadius =
                Mathf.Clamp(profile.EngagementRiskRadius, 2.5f, 9f);
            profile.OutnumberedApproachPenalty =
                Mathf.Clamp(profile.OutnumberedApproachPenalty, 0f, 70f);
            profile.SupportedFightCommitBonus =
                Mathf.Clamp(profile.SupportedFightCommitBonus, 0f, 45f);
            profile.BadDiveRepositionBonus =
                Mathf.Clamp(profile.BadDiveRepositionBonus, 0f, 55f);
            profile.EngagementRiskSafetyBonus =
                Mathf.Clamp(profile.EngagementRiskSafetyBonus, 0f, 45f);
            profile.PressureRotationAwarenessWeight =
                Mathf.Clamp(profile.PressureRotationAwarenessWeight, 0f, 2.5f);
            profile.EnemyHotspotRotationBonus =
                Mathf.Clamp(profile.EnemyHotspotRotationBonus, 0f, 45f);
            profile.ThreatCenterRotationBonus =
                Mathf.Clamp(profile.ThreatCenterRotationBonus, 0f, 55f);
            profile.ThreatCenterDivePenalty =
                Mathf.Clamp(profile.ThreatCenterDivePenalty, 0f, 60f);
            profile.PressureRotationRadius =
                Mathf.Clamp(profile.PressureRotationRadius, 3f, 14f);

            profile.TacticalMoveRetargetTicks = ClampTicks(profile.TacticalMoveRetargetTicks, 1, 45);
            profile.TacticalMoveHeartbeatTicks = ClampTicks(profile.TacticalMoveHeartbeatTicks, 1, 60);
            profile.TacticalDestinationStaleDistance = Mathf.Clamp(profile.TacticalDestinationStaleDistance, 0.25f, 4f);
            profile.TacticalDirectionFlipCooldownTicks = ClampTicks(profile.TacticalDirectionFlipCooldownTicks, 1, 90);
            profile.TacticalDestinationSwitchDistance = Mathf.Clamp(profile.TacticalDestinationSwitchDistance, 0.25f, 4f);
            profile.TacticalDestinationBlend = Mathf.Clamp(profile.TacticalDestinationBlend, 0.05f, 1f);
            profile.AIMoveInputTurnRateDegreesPerTick = Mathf.Clamp(profile.AIMoveInputTurnRateDegreesPerTick, 8f, 120f);
            profile.AIHighPriorityMoveInputTurnRateDegreesPerTick = Mathf.Clamp(profile.AIHighPriorityMoveInputTurnRateDegreesPerTick, 20f, 180f);
            profile.AIMoveSpeedScale = Mathf.Clamp(profile.AIMoveSpeedScale, 0.35f, 1f);
            profile.AIHighPriorityMoveSpeedScale = Mathf.Clamp(profile.AIHighPriorityMoveSpeedScale, 0.35f, 1f);
            profile.TacticalMinimumStepDistance = Mathf.Clamp(profile.TacticalMinimumStepDistance, 0.25f, 2f);
            profile.TacticalStopMaxHoldTicks = ClampTicks(profile.TacticalStopMaxHoldTicks, 1, 90);
            profile.TacticalStrafeDistance = Mathf.Clamp(profile.TacticalStrafeDistance, 0.4f, 4f);
            profile.TacticalKiteDistance = Mathf.Clamp(profile.TacticalKiteDistance, 0.5f, 5f);

            profile.MinimumCommittedActionScore = Mathf.Clamp(profile.MinimumCommittedActionScore, 0f, 40f);
            profile.ActionSwitchScoreMargin = Mathf.Clamp(profile.ActionSwitchScoreMargin, 4f, 35f);
            profile.DecisionAmbiguityScoreWindow =
                Mathf.Clamp(profile.DecisionAmbiguityScoreWindow, 0f, 20f);
            profile.DecisionAmbiguitySwitchPenalty =
                Mathf.Clamp(profile.DecisionAmbiguitySwitchPenalty, 0f, 25f);
            profile.DecisionAmbiguityExtraHoldTicks =
                ClampTicks(profile.DecisionAmbiguityExtraHoldTicks, 0, 30);
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
            profile.IdleHesitationRecoveryTicks = ClampTicks(profile.IdleHesitationRecoveryTicks, 6, 90);
            profile.IdleHesitationCooldownTicks = ClampTicks(profile.IdleHesitationCooldownTicks, 6, 120);
            profile.IdleHesitationLowScoreThreshold = Mathf.Clamp(profile.IdleHesitationLowScoreThreshold, 0f, 30f);
            profile.FailedCastMemoryTicks = ClampTicks(profile.FailedCastMemoryTicks, 5, 240);
            profile.FailedCastRecoveryLimit = Mathf.Clamp(profile.FailedCastRecoveryLimit, 1, 5);
            profile.FailedCastSuppressionTicks = ClampTicks(profile.FailedCastSuppressionTicks, 5, 180);

            profile.DebugSnapshotIntervalTicks = ClampTicks(profile.DebugSnapshotIntervalTicks, 1, 60);
            profile.MaxMapResolvesPerTick = Mathf.Clamp(profile.MaxMapResolvesPerTick, 1, 256);
            profile.MaxPathQueriesPerTick = Mathf.Clamp(profile.MaxPathQueriesPerTick, 1, 128);
            profile.MaxPathTouchedNodesPerTick = Mathf.Clamp(profile.MaxPathTouchedNodesPerTick, 64, 50000);
            profile.PathBudgetStarvationLimit = Mathf.Clamp(profile.PathBudgetStarvationLimit, 1, 12);

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
                profile.AIMoveInputTurnRateDegreesPerTick = 22f;

            if (profile.AIHighPriorityMoveInputTurnRateDegreesPerTick <= 0f)
                profile.AIHighPriorityMoveInputTurnRateDegreesPerTick = 54f;

            if (profile.AIMoveSpeedScale <= 0f)
                profile.AIMoveSpeedScale = 0.86f;

            if (profile.AIHighPriorityMoveSpeedScale <= 0f)
                profile.AIHighPriorityMoveSpeedScale = 0.90f;

            if (profile.NavigationStuckSampleIntervalTicks == 0u)
                profile.NavigationStuckSampleIntervalTicks = 8u;

            if (profile.NavigationStuckMoveThreshold <= 0f)
                profile.NavigationStuckMoveThreshold = 0.08f;
        }

        private static void EnsureTargetContextDefaults(BrawlerAIProfile profile)
        {
            if (profile.TargetContextAwarenessWeight <= 0f)
                profile.TargetContextAwarenessWeight = 1f;

            if (profile.IsolatedTargetBonus <= 0f)
                profile.IsolatedTargetBonus = 16f;

            if (profile.ProtectedTargetPenalty <= 0f)
                profile.ProtectedTargetPenalty = 18f;

            if (profile.AllyCollapseTargetBonus <= 0f)
                profile.AllyCollapseTargetBonus = 14f;

            if (profile.TargetContextRadius <= 0f)
                profile.TargetContextRadius = 4.75f;
        }

        private static void EnsureDangerAvoidanceDefaults(BrawlerAIProfile profile)
        {
            if (profile.DangerScanRadius <= 0f)
                profile.DangerScanRadius = 7f;

            if (profile.DangerRefreshIntervalTicks == 0u)
                profile.DangerRefreshIntervalTicks = 4u;

            if (profile.DangerPersonalSpace <= 0f)
                profile.DangerPersonalSpace = 0.45f;

            if (profile.DangerReactionTimeSeconds <= 0f)
                profile.DangerReactionTimeSeconds = 0.55f;

            if (profile.DangerEvadePressureThreshold <= 0f)
                profile.DangerEvadePressureThreshold = 0.36f;

            if (profile.DangerEvadeScoreBonus <= 0f)
                profile.DangerEvadeScoreBonus = 50f;

            if (profile.DangerEvadeDistance <= 0f)
                profile.DangerEvadeDistance = 1.8f;

            if (profile.DangerEvadeRetargetTicks == 0u)
                profile.DangerEvadeRetargetTicks = 10u;

            if (profile.DangerThreatStaleDistance <= 0f)
                profile.DangerThreatStaleDistance = 1.25f;

            if (profile.DangerMapSearchRadius <= 0f)
                profile.DangerMapSearchRadius = 1.2f;
        }

        private static void EnsureOpponentResourceAwarenessDefaults(BrawlerAIProfile profile)
        {
            if (profile.OpponentResourceAwarenessWeight <= 0f)
                profile.OpponentResourceAwarenessWeight = 1f;

            if (profile.EnemyLowAmmoOpportunityBonus <= 0f)
                profile.EnemyLowAmmoOpportunityBonus = 18f;

            if (profile.EnemyNoAttackApproachBonus <= 0f)
                profile.EnemyNoAttackApproachBonus = 12f;

            if (profile.EnemySuperReadyThreatPenalty <= 0f)
                profile.EnemySuperReadyThreatPenalty = 20f;

            if (profile.EnemyNearlySuperThreatPenalty <= 0f)
                profile.EnemyNearlySuperThreatPenalty = 8f;

            if (profile.EnemySuperRespectBonus <= 0f)
                profile.EnemySuperRespectBonus = 14f;
        }

        private static void EnsureAdvancedTeamFightDefaults(BrawlerAIProfile profile)
        {
            if (profile.TeamFightCoordinationWeight <= 0f)
                profile.TeamFightCoordinationWeight = 1f;

            if (profile.TeamCollapseFocusBonus <= 0f)
                profile.TeamCollapseFocusBonus = 16f;

            if (profile.TeamFlankRepositionBonus <= 0f)
                profile.TeamFlankRepositionBonus = 12f;

            if (profile.TeamBaitHoldBonus <= 0f)
                profile.TeamBaitHoldBonus = 10f;

            if (profile.TeamPeelAssistBonus <= 0f)
                profile.TeamPeelAssistBonus = 14f;

            if (profile.TeamOvercommitApproachPenalty <= 0f)
                profile.TeamOvercommitApproachPenalty = 14f;
        }

        private static void EnsureRoleMacroDefaults(BrawlerAIProfile profile)
        {
            if (profile.RoleMacroBehaviorWeight <= 0f)
                profile.RoleMacroBehaviorWeight = 1f;

            if (profile.RoleTankSpaceCreationBonus <= 0f)
                profile.RoleTankSpaceCreationBonus = profile.Archetype == BrawlerArchetype.Tank ? 18f : 14f;

            if (profile.RoleBacklineAnchorMacroBonus <= 0f)
                profile.RoleBacklineAnchorMacroBonus =
                    profile.Archetype == BrawlerArchetype.Sniper ||
                    profile.Archetype == BrawlerArchetype.Artillery
                        ? 18f
                        : 13f;

            if (profile.RoleSupportPeelMacroBonus <= 0f)
                profile.RoleSupportPeelMacroBonus =
                    profile.Archetype == BrawlerArchetype.Support ? 18f : 13f;

            if (profile.RoleAssassinPickPressureBonus <= 0f)
                profile.RoleAssassinPickPressureBonus =
                    profile.Archetype == BrawlerArchetype.Assassin ? 20f : 14f;

            if (profile.RoleControllerZoneMacroBonus <= 0f)
                profile.RoleControllerZoneMacroBonus =
                    profile.Archetype == BrawlerArchetype.Controller ? 19f : 14f;

            if (profile.RoleArtilleryDenialMacroBonus <= 0f)
                profile.RoleArtilleryDenialMacroBonus =
                    profile.Archetype == BrawlerArchetype.Artillery ? 19f : 13f;

            if (profile.RoleFighterFlexMacroBonus <= 0f)
                profile.RoleFighterFlexMacroBonus =
                    profile.Archetype == BrawlerArchetype.Fighter ? 14f : 10f;
        }

        private static void EnsureEngagementRiskDefaults(BrawlerAIProfile profile)
        {
            if (profile.EngagementRiskAwarenessWeight <= 0f)
                profile.EngagementRiskAwarenessWeight = 1f;

            if (profile.EngagementRiskRadius <= 0f)
                profile.EngagementRiskRadius = 5.25f;

            if (profile.OutnumberedApproachPenalty <= 0f)
                profile.OutnumberedApproachPenalty = 24f;

            if (profile.SupportedFightCommitBonus <= 0f)
                profile.SupportedFightCommitBonus = 14f;

            if (profile.BadDiveRepositionBonus <= 0f)
                profile.BadDiveRepositionBonus = 18f;

            if (profile.EngagementRiskSafetyBonus <= 0f)
                profile.EngagementRiskSafetyBonus = 12f;
        }

        private static void EnsurePressureRotationDefaults(BrawlerAIProfile profile)
        {
            if (profile.PressureRotationAwarenessWeight <= 0f)
                profile.PressureRotationAwarenessWeight = 1f;

            if (profile.EnemyHotspotRotationBonus <= 0f)
                profile.EnemyHotspotRotationBonus = 16f;

            if (profile.ThreatCenterRotationBonus <= 0f)
                profile.ThreatCenterRotationBonus = 18f;

            if (profile.ThreatCenterDivePenalty <= 0f)
                profile.ThreatCenterDivePenalty = 20f;

            if (profile.PressureRotationRadius <= 0f)
                profile.PressureRotationRadius = 7f;
        }

        private static void EnsureDecisionConfidenceDefaults(BrawlerAIProfile profile)
        {
            if (profile.DecisionAmbiguityScoreWindow <= 0f)
                profile.DecisionAmbiguityScoreWindow = 6f;

            if (profile.DecisionAmbiguitySwitchPenalty <= 0f)
                profile.DecisionAmbiguitySwitchPenalty = 7f;

            if (profile.DecisionAmbiguityExtraHoldTicks == 0u)
                profile.DecisionAmbiguityExtraHoldTicks = 5u;
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

            if (profile.CloseRangeCatchDistanceMultiplier <= 0f)
                profile.CloseRangeCatchDistanceMultiplier = 2.35f;

            if (profile.CloseRangeOutrangedChasePenalty <= 0f)
                profile.CloseRangeOutrangedChasePenalty = 52f;

            if (profile.CloseRangeCoverRepositionBonus <= 0f)
                profile.CloseRangeCoverRepositionBonus = 26f;

            if (profile.CloseRangeEvasivePressureBonus <= 0f)
                profile.CloseRangeEvasivePressureBonus = 12f;
        }

        private static void ApplyFairPlayGuardrails(BrawlerAIProfile profile)
        {
            if (profile.Difficulty == AIDifficultyLevel.Hard)
            {
                profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 1f);
                profile.CombatSenseIntervalTicks = ClampTicks(profile.CombatSenseIntervalTicks, 2, 30);
                profile.DangerRefreshIntervalTicks = ClampTicks(profile.DangerRefreshIntervalTicks, 2, 12);
                profile.AIMoveSpeedScale = Mathf.Min(profile.AIMoveSpeedScale, 0.96f);
                profile.AIHighPriorityMoveSpeedScale = Mathf.Min(profile.AIHighPriorityMoveSpeedScale, 0.98f);
            }
            else if (profile.Difficulty == AIDifficultyLevel.Normal)
            {
                profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 2f);
                profile.AIMoveSpeedScale = Mathf.Min(profile.AIMoveSpeedScale, 0.92f);
                profile.AIHighPriorityMoveSpeedScale = Mathf.Min(profile.AIHighPriorityMoveSpeedScale, 0.94f);
            }
            else
            {
                profile.AimErrorDegrees = Mathf.Max(profile.AimErrorDegrees, 6f);
                profile.ReactionDelayTicks = ClampTicks(profile.ReactionDelayTicks, 6, 24);
                profile.AIMoveSpeedScale = Mathf.Min(profile.AIMoveSpeedScale, 0.82f);
                profile.AIHighPriorityMoveSpeedScale = Mathf.Min(profile.AIHighPriorityMoveSpeedScale, 0.86f);
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
