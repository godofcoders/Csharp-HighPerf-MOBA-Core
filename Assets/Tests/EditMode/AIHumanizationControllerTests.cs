using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIHumanizationControllerTests
    {
        private readonly List<BrawlerAIProfile> _profiles = new List<BrawlerAIProfile>(4);

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
        public void ShapeActionScores_BoostsPersonalityFakeOutMovement()
        {
            BrawlerAIProfile profile = CreateProfile(AIPersonalityType.Balanced);
            profile.HumanizationActionScoreJitter = 0f;
            profile.HumanizationFakeOutChance = 1f;
            profile.HumanizationFakeOutScoreBonus = 10f;
            profile.HumanizationPressureMistakeChance = 0f;

            AIHumanizationController humanization = new AIHumanizationController(profile, ownerEntityId: 42u);
            List<AIActionScore> scores = MakeScores(
                new AIActionScore(AIActionType.Approach, 20f),
                new AIActionScore(AIActionType.Reposition, 15f));

            humanization.ShapeActionScores(
                scores,
                currentTick: 0u,
                hasLiveTarget: true,
                healthRatio: 1f,
                hasDanger: false);

            Assert.AreEqual(25f, FindScore(scores, AIActionType.Reposition), 0.001f);
            Assert.AreEqual(20f, FindScore(scores, AIActionType.Approach), 0.001f);
        }

        [Test]
        public void ShapeActionScores_PressureMistakeSoftensOffenseAndOverDefends()
        {
            BrawlerAIProfile profile = CreateProfile(AIPersonalityType.Balanced);
            profile.HumanizationActionScoreJitter = 0f;
            profile.HumanizationFakeOutChance = 0f;
            profile.HumanizationPressureMistakeChance = 1f;
            profile.HumanizationPressureMistakePenalty = 12f;
            profile.HumanizationPressureHealthThreshold = 0.5f;

            AIHumanizationController humanization = new AIHumanizationController(profile, ownerEntityId: 17u);
            List<AIActionScore> scores = MakeScores(
                new AIActionScore(AIActionType.Approach, 50f),
                new AIActionScore(AIActionType.Reposition, 20f));

            humanization.ShapeActionScores(
                scores,
                currentTick: 0u,
                hasLiveTarget: true,
                healthRatio: 0.25f,
                hasDanger: false);

            Assert.AreEqual(38f, FindScore(scores, AIActionType.Approach), 0.001f);
            Assert.AreEqual(24.8f, FindScore(scores, AIActionType.Reposition), 0.001f);
        }

        [Test]
        public void ShapeActionScores_ProtectsEmergencyActions()
        {
            BrawlerAIProfile profile = CreateProfile(AIPersonalityType.Aggressive);
            profile.HumanizationActionScoreJitter = 0f;
            profile.HumanizationFakeOutChance = 1f;
            profile.HumanizationPressureMistakeChance = 1f;
            profile.EmergencyOverrideScore = 90f;

            AIHumanizationController humanization = new AIHumanizationController(profile, ownerEntityId: 9u);
            List<AIActionScore> scores = MakeScores(
                new AIActionScore(AIActionType.Evade, 80f),
                new AIActionScore(AIActionType.Approach, 70f));

            humanization.ShapeActionScores(
                scores,
                currentTick: 0u,
                hasLiveTarget: true,
                healthRatio: 0.2f,
                hasDanger: true);

            Assert.AreEqual(80f, FindScore(scores, AIActionType.Evade), 0.001f);
            Assert.AreEqual(70f, FindScore(scores, AIActionType.Approach), 0.001f);
        }

        [Test]
        public void GetReactionJitterTicks_IsDeterministicAndBounded()
        {
            BrawlerAIProfile profile = CreateProfile(AIPersonalityType.Balanced);
            profile.HumanizationReactionJitterTicks = 5;

            AIHumanizationController humanization = new AIHumanizationController(profile, ownerEntityId: 21u);

            uint first = humanization.GetReactionJitterTicks(currentTick: 123u, hasLiveTarget: true);
            uint second = humanization.GetReactionJitterTicks(currentTick: 123u, hasLiveTarget: true);

            Assert.AreEqual(first, second);
            Assert.LessOrEqual((int)first, 5);
        }

        private BrawlerAIProfile CreateProfile(AIPersonalityType personality)
        {
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.ApplyArchetypeDefaults(BrawlerArchetype.Fighter);
            profile.Personality = personality;
            profile.EnableHumanization = true;
            _profiles.Add(profile);
            return profile;
        }

        private static List<AIActionScore> MakeScores(params AIActionScore[] scores)
        {
            return new List<AIActionScore>(scores);
        }

        private static float FindScore(List<AIActionScore> scores, AIActionType actionType)
        {
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i].ActionType == actionType)
                    return scores[i].Score;
            }

            return 0f;
        }
    }
}
