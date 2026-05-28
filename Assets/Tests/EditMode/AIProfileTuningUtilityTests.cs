using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIProfileTuningUtilityTests
    {
        private readonly List<BrawlerAIProfile> _profiles = new List<BrawlerAIProfile>(8);

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i] != null)
                    Object.DestroyImmediate(_profiles[i]);
            }

            _profiles.Clear();
        }

        [Test]
        public void ApplyRuntimeTuning_EasyAndHardCreateReadableSkillGap()
        {
            BrawlerAIProfile easy = CreateProfile();
            BrawlerAIProfile hard = CreateProfile();

            AIProfileTuningUtility.ApplyRuntimeTuning(
                easy,
                AIDifficultyLevel.Easy,
                AIPersonalityType.Balanced);
            AIProfileTuningUtility.ApplyRuntimeTuning(
                hard,
                AIDifficultyLevel.Hard,
                AIPersonalityType.Balanced);

            Assert.Greater((int)easy.ReactionDelayTicks, (int)hard.ReactionDelayTicks);
            Assert.Greater(easy.AimErrorDegrees, hard.AimErrorDegrees);
            Assert.Greater((int)easy.CombatSenseIntervalTicks, (int)hard.CombatSenseIntervalTicks);
            Assert.Greater((int)easy.AttackCadenceTicks, (int)hard.AttackCadenceTicks);
            Assert.Greater(easy.ActionSwitchScoreMargin, hard.ActionSwitchScoreMargin);
            Assert.Greater((int)easy.CombatActionCommitmentTicks, (int)hard.CombatActionCommitmentTicks);
            Assert.Greater((int)easy.HumanizationReactionJitterTicks, (int)hard.HumanizationReactionJitterTicks);
            Assert.Greater(easy.HumanizationActionScoreJitter, hard.HumanizationActionScoreJitter);
            Assert.Greater(easy.HumanizationPressureMistakeChance, hard.HumanizationPressureMistakeChance);
        }

        [Test]
        public void ApplyRuntimeTuning_HardKeepsFairPlayGuardrails()
        {
            BrawlerAIProfile profile = CreateProfile();
            profile.AimErrorDegrees = 0f;
            profile.CombatSenseIntervalTicks = 1;
            profile.DangerRefreshIntervalTicks = 1;

            AIProfileTuningUtility.ApplyRuntimeTuning(
                profile,
                AIDifficultyLevel.Hard,
                AIPersonalityType.Aggressive);

            Assert.GreaterOrEqual(profile.AimErrorDegrees, 1f);
            Assert.GreaterOrEqual((int)profile.ReactionDelayTicks, 1);
            Assert.GreaterOrEqual((int)profile.CombatSenseIntervalTicks, 2);
            Assert.GreaterOrEqual((int)profile.DangerRefreshIntervalTicks, 2);
        }

        [Test]
        public void ApplyRuntimeTuning_PersonalitiesShapeRiskAndTeamplay()
        {
            BrawlerAIProfile aggressive = CreateProfile();
            BrawlerAIProfile cautious = CreateProfile();
            BrawlerAIProfile teamPlayer = CreateProfile();
            BrawlerAIProfile balanced = CreateProfile();

            AIProfileTuningUtility.ApplyRuntimeTuning(
                aggressive,
                AIDifficultyLevel.Normal,
                AIPersonalityType.Aggressive);
            AIProfileTuningUtility.ApplyRuntimeTuning(
                cautious,
                AIDifficultyLevel.Normal,
                AIPersonalityType.Cautious);
            AIProfileTuningUtility.ApplyRuntimeTuning(
                teamPlayer,
                AIDifficultyLevel.Normal,
                AIPersonalityType.TeamPlayer);
            AIProfileTuningUtility.ApplyRuntimeTuning(
                balanced,
                AIDifficultyLevel.Normal,
                AIPersonalityType.Balanced);

            Assert.Greater(aggressive.ApproachWeight, cautious.ApproachWeight);
            Assert.Less(aggressive.RetreatWeight, cautious.RetreatWeight);
            Assert.Greater(teamPlayer.PeelWeight, balanced.PeelWeight);
            Assert.Greater(teamPlayer.RegroupWeight, balanced.RegroupWeight);
            Assert.Greater(teamPlayer.TeamRoleCoordinationWeight, balanced.TeamRoleCoordinationWeight);
            Assert.Greater(teamPlayer.OverFocusedTargetPenaltyPerAlly, aggressive.OverFocusedTargetPenaltyPerAlly);
        }

        [Test]
        public void ApplyRuntimeTuning_NormalizesCommitmentAndPreservesTeamplayFloors()
        {
            BrawlerAIProfile profile = CreateProfile();
            profile.MinimumCommittedActionScore = 99f;
            profile.ActionSwitchScoreMargin = 99f;
            profile.CombatActionCommitmentTicks = 999;
            profile.NonCombatActionCommitmentTicks = 999;
            profile.EmergencyOverrideScore = 999f;
            profile.TeamRoleCoordinationWeight = 0f;
            profile.TeamActionCrowdingPenalty = 0f;
            profile.OverFocusedTargetPenaltyPerAlly = 0f;
            profile.MaxOverFocusedTargetPenalty = 0f;
            profile.DebugSnapshotIntervalTicks = 999;
            profile.MaxMapResolvesPerTick = -5;
            profile.MaxPathQueriesPerTick = -5;
            profile.MaxPathTouchedNodesPerTick = -5;
            profile.HumanizationReactionJitterTicks = 999;
            profile.HumanizationActionScoreJitter = 99f;
            profile.HumanizationFakeOutScoreBonus = 99f;
            profile.HumanizationPressureMistakePenalty = 99f;

            AIProfileTuningUtility.ApplyRuntimeTuning(
                profile,
                AIDifficultyLevel.Easy,
                AIPersonalityType.Aggressive);

            Assert.LessOrEqual(profile.MinimumCommittedActionScore, 40f);
            Assert.LessOrEqual(profile.ActionSwitchScoreMargin, 35f);
            Assert.LessOrEqual((int)profile.CombatActionCommitmentTicks, 60);
            Assert.LessOrEqual((int)profile.NonCombatActionCommitmentTicks, 120);
            Assert.LessOrEqual(profile.EmergencyOverrideScore, 120f);
            Assert.GreaterOrEqual(profile.TeamRoleCoordinationWeight, 0.65f);
            Assert.GreaterOrEqual(profile.TeamActionCrowdingPenalty, 6f);
            Assert.GreaterOrEqual(profile.OverFocusedTargetPenaltyPerAlly, 8f);
            Assert.GreaterOrEqual(
                profile.MaxOverFocusedTargetPenalty,
                profile.OverFocusedTargetPenaltyPerAlly * 1.75f);
            Assert.LessOrEqual((int)profile.DebugSnapshotIntervalTicks, 60);
            Assert.GreaterOrEqual(profile.MaxMapResolvesPerTick, 1);
            Assert.GreaterOrEqual(profile.MaxPathQueriesPerTick, 1);
            Assert.GreaterOrEqual(profile.MaxPathTouchedNodesPerTick, 64);
            Assert.LessOrEqual((int)profile.HumanizationReactionJitterTicks, 8);
            Assert.LessOrEqual(profile.HumanizationActionScoreJitter, 6f);
            Assert.LessOrEqual(profile.HumanizationFakeOutScoreBonus, 16f);
            Assert.LessOrEqual(profile.HumanizationPressureMistakePenalty, 24f);
        }

        private BrawlerAIProfile CreateProfile()
        {
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.ApplyArchetypeDefaults(BrawlerArchetype.Fighter);
            _profiles.Add(profile);
            return profile;
        }
    }
}
