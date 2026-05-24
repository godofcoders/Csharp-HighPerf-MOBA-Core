using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using System.Collections.Generic;

namespace MOBA.Tests.EditMode
{
    public class AIValidationTelemetryTests
    {
        [SetUp]
        public void SetUp()
        {
            AIValidationTelemetry.ResetForTests();
        }

        [Test]
        public void RecordDecision_TracksActionDistributionAndTargetContext()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Approach, 42f),
                true,
                MakeScores(
                    new AIActionScore(AIActionType.Approach, 42f),
                    new AIActionScore(AIActionType.HoldRange, 30f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                2,
                10u,
                new AIActionScore(AIActionType.Objective, 20f),
                false,
                MakeScores(
                    new AIActionScore(AIActionType.Objective, 20f),
                    new AIActionScore(AIActionType.Wander, 5f)),
                "A=0");

            Assert.AreEqual(2, AIValidationTelemetry.ActiveBotCount);
            Assert.AreEqual(1, AIValidationTelemetry.TargetedBotCount);
            Assert.AreEqual(1, AIValidationTelemetry.TargetlessBotCount);
            Assert.AreEqual(1, AIValidationTelemetry.GetActionCount(AIActionType.Approach));
            Assert.AreEqual(1, AIValidationTelemetry.GetActionCount(AIActionType.Objective));
            Assert.AreEqual(0, AIValidationTelemetry.InvalidDecisionCount);
        }

        [Test]
        public void RecordDecision_FlagsInvalidContextOnlyForMeaningfulScores()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Approach, 25f),
                false,
                MakeScores(new AIActionScore(AIActionType.Approach, 25f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                2,
                10u,
                new AIActionScore(AIActionType.Objective, 0f),
                true,
                MakeScores(new AIActionScore(AIActionType.Objective, 0f)),
                "A=0");

            Assert.AreEqual(1, AIValidationTelemetry.InvalidDecisionCount);
            Assert.AreEqual(1, AIValidationTelemetry.ZeroScoreDecisionCount);
        }

        [Test]
        public void RecordDecision_TracksSwitchesAcrossTicks()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Approach, 40f),
                true,
                MakeScores(new AIActionScore(AIActionType.Approach, 40f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                1,
                11u,
                new AIActionScore(AIActionType.HoldRange, 38f),
                true,
                MakeScores(new AIActionScore(AIActionType.HoldRange, 38f)),
                "A=0");

            Assert.AreEqual(1, AIValidationTelemetry.ActionSwitchCount);
        }

        [Test]
        public void RecordDecision_TracksLowConfidenceAndRoleAdjustments()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.HoldRange, 42f),
                true,
                MakeScores(
                    new AIActionScore(AIActionType.HoldRange, 42f),
                    new AIActionScore(AIActionType.Reposition, 39f)),
                "A=1 Delta=HoldRange+6.0");

            Assert.AreEqual(1, AIValidationTelemetry.LowConfidenceDecisionCount);
            Assert.AreEqual(1, AIValidationTelemetry.TeamRoleAdjustedDecisionCount);
            Assert.AreEqual(42f, AIValidationTelemetry.AverageTopScore);
            Assert.AreEqual(3f, AIValidationTelemetry.AverageScoreMargin);
        }

        [Test]
        public void RecordDecision_TracksSuperAndRegroupActions()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.UseSuper, 90f),
                true,
                MakeScores(new AIActionScore(AIActionType.UseSuper, 90f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                2,
                10u,
                new AIActionScore(AIActionType.Regroup, 35f),
                false,
                MakeScores(new AIActionScore(AIActionType.Regroup, 35f)),
                "A=0");

            Assert.AreEqual(1, AIValidationTelemetry.GetActionCount(AIActionType.UseSuper));
            Assert.AreEqual(1, AIValidationTelemetry.GetActionCount(AIActionType.Regroup));
            StringAssert.Contains("U/G=1/1", AIValidationTelemetry.GetDebugSummary(10u));
        }

        [Test]
        public void RecordDecision_ResetsFrameCountersWhenTickChanges()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Evade, 80f),
                false,
                MakeScores(new AIActionScore(AIActionType.Evade, 80f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                2,
                11u,
                new AIActionScore(AIActionType.Wander, 5f),
                false,
                MakeScores(new AIActionScore(AIActionType.Wander, 5f)),
                "A=0");

            Assert.AreEqual(1, AIValidationTelemetry.ActiveBotCount);
            Assert.AreEqual(0, AIValidationTelemetry.EmergencyActionCount);
            Assert.AreEqual(1, AIValidationTelemetry.GetActionCount(AIActionType.Wander));
        }

        private static List<AIActionScore> MakeScores(params AIActionScore[] scores)
        {
            return new List<AIActionScore>(scores);
        }
    }
}
