using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class AIProfileTuningUtility
    {
        public static void ApplyRuntimeTuning(
            BrawlerAIProfile profile,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            if (profile == null)
                return;

            profile.Difficulty = difficulty;
            profile.Personality = personality;

            ApplyDifficulty(profile, difficulty);
            ApplyPersonality(profile, personality);

            Normalize(profile);
        }

        private static void ApplyDifficulty(BrawlerAIProfile profile, AIDifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case AIDifficultyLevel.Easy:
                    profile.ReactionDelayTicks = 8;
                    profile.AimErrorDegrees = 8f;
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 1.55f, 4);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 2.35f, 2);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 1.35f, 1);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 1.4f, 1);
                    profile.GadgetCooldownTicks = ScaleTicks(profile.GadgetCooldownTicks, 1.25f, 1);
                    profile.ActionSwitchScoreMargin *= 1.25f;
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
                    profile.DangerScanRadius *= 0.85f;
                    profile.DangerReactionTimeSeconds *= 0.80f;
                    profile.DangerEvadePressureThreshold *= 1.15f;
                    profile.DangerEvadeScoreBonus *= 0.80f;
                    profile.DangerRefreshIntervalTicks = ScaleTicks(profile.DangerRefreshIntervalTicks, 1.5f, 1);
                    profile.DangerEvadeRetargetTicks = ScaleTicks(profile.DangerEvadeRetargetTicks, 1.35f, 1);
                    profile.MapLineOfSightCoverPreference *= 0.80f;
                    profile.MapExposedPositionPenalty *= 0.75f;
                    profile.MapOpenShotPreference *= 0.80f;
                    profile.NavigationStuckSampleLimit += 1;
                    profile.StaleDestinationRecoveryTicks = ScaleTicks(profile.StaleDestinationRecoveryTicks, 1.25f, 1);
                    profile.FailureRecoveryCooldownTicks = ScaleTicks(profile.FailureRecoveryCooldownTicks, 1.25f, 1);
                    profile.FailedCastSuppressionTicks = ScaleTicks(profile.FailedCastSuppressionTicks, 1.15f, 1);
                    break;

                case AIDifficultyLevel.Hard:
                    profile.ReactionDelayTicks = 0;
                    profile.AimErrorDegrees = 1.25f;
                    profile.IdleSenseIntervalTicks = ScaleTicks(profile.IdleSenseIntervalTicks, 0.75f, 2);
                    profile.CombatSenseIntervalTicks = ScaleTicks(profile.CombatSenseIntervalTicks, 0.65f, 1);
                    profile.AttackCadenceTicks = ScaleTicks(profile.AttackCadenceTicks, 0.85f, 1);
                    profile.SuperDecisionCooldownTicks = ScaleTicks(profile.SuperDecisionCooldownTicks, 0.75f, 1);
                    profile.GadgetCooldownTicks = ScaleTicks(profile.GadgetCooldownTicks, 0.85f, 1);
                    profile.ActionSwitchScoreMargin *= 0.85f;
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
                    profile.StaleDestinationRecoveryTicks = ScaleTicks(profile.StaleDestinationRecoveryTicks, 0.85f, 1);
                    profile.FailureRecoveryCooldownTicks = ScaleTicks(profile.FailureRecoveryCooldownTicks, 0.85f, 1);
                    profile.FailedCastSuppressionTicks = ScaleTicks(profile.FailedCastSuppressionTicks, 0.85f, 1);
                    break;

                case AIDifficultyLevel.Normal:
                default:
                    profile.ReactionDelayTicks = 3;
                    profile.AimErrorDegrees = 3f;
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
                    profile.FailureRecoveryDetourDistance *= 0.95f;
                    break;

                case AIPersonalityType.Cautious:
                    profile.RetreatWeight *= 1.25f;
                    profile.HoldRangeWeight *= 1.15f;
                    profile.RepositionWeight *= 1.20f;
                    profile.ApproachWeight *= 0.82f;
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
                    profile.FailureRecoveryCooldownTicks = ScaleTicks(profile.FailureRecoveryCooldownTicks, 0.90f, 1);
                    profile.FailureRecoveryDetourDistance *= 1.08f;
                    break;

                case AIPersonalityType.TeamPlayer:
                    profile.PeelWeight *= 1.30f;
                    profile.RegroupWeight *= 1.22f;
                    profile.FocusFireWeight *= 1.15f;
                    profile.ObjectiveWeight *= 1.10f;
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
                    profile.FailedCastSuppressionTicks = ScaleTicks(profile.FailedCastSuppressionTicks, 1.10f, 1);
                    break;

                case AIPersonalityType.Balanced:
                default:
                    break;
            }
        }

        private static void Normalize(BrawlerAIProfile profile)
        {
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

            profile.TacticalMoveRetargetTicks = ClampTicks(profile.TacticalMoveRetargetTicks, 1, 45);
            profile.TacticalMoveHeartbeatTicks = ClampTicks(profile.TacticalMoveHeartbeatTicks, 1, 60);
            profile.TacticalDestinationStaleDistance = Mathf.Clamp(profile.TacticalDestinationStaleDistance, 0.25f, 4f);
            profile.TacticalMinimumStepDistance = Mathf.Clamp(profile.TacticalMinimumStepDistance, 0.25f, 2f);
            profile.TacticalStrafeDistance = Mathf.Clamp(profile.TacticalStrafeDistance, 0.4f, 4f);
            profile.TacticalKiteDistance = Mathf.Clamp(profile.TacticalKiteDistance, 0.5f, 5f);

            profile.ActionSwitchScoreMargin = Mathf.Clamp(profile.ActionSwitchScoreMargin, 4f, 35f);
            profile.FocusFireWeight = Mathf.Clamp(profile.FocusFireWeight, 0f, 45f);
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
            profile.BlockedRouteRecoveryLimit = Mathf.Clamp(profile.BlockedRouteRecoveryLimit, 1, 4);
            profile.StaleDestinationRecoveryTicks = ClampTicks(profile.StaleDestinationRecoveryTicks, 20, 240);
            profile.StaleDestinationProgressThreshold = Mathf.Clamp(profile.StaleDestinationProgressThreshold, 0.1f, 3f);
            profile.FailureRecoveryCooldownTicks = ClampTicks(profile.FailureRecoveryCooldownTicks, 4, 90);
            profile.FailureRecoveryDetourDistance = Mathf.Clamp(profile.FailureRecoveryDetourDistance, 0.75f, 4f);
            profile.FailedCastMemoryTicks = ClampTicks(profile.FailedCastMemoryTicks, 5, 240);
            profile.FailedCastRecoveryLimit = Mathf.Clamp(profile.FailedCastRecoveryLimit, 1, 5);
            profile.FailedCastSuppressionTicks = ClampTicks(profile.FailedCastSuppressionTicks, 5, 180);
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
