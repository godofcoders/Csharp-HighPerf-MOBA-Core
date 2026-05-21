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
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 1.35f, 1);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 1.25f, 1);
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
                    profile.TacticalMoveRetargetTicks = ScaleTicks(profile.TacticalMoveRetargetTicks, 0.80f, 1);
                    profile.TacticalMoveHeartbeatTicks = ScaleTicks(profile.TacticalMoveHeartbeatTicks, 0.80f, 1);
                    profile.TacticalMinimumStepDistance *= 1.10f;
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
