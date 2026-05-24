using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using System.Collections.Generic;

namespace MOBA.Tests.EditMode
{
    public class AIValidationScenarioTrackerTests
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;

        [SetUp]
        public void SetUp()
        {
            AIValidationTelemetry.ResetForTests();
            AIValidationHealthTracker.ResetForTests();
            AIValidationScenarioTracker.ResetForTests();
        }

        [Test]
        public void ClassifyFrame_ReturnsCombat_WhenMostBotsHaveTargets()
        {
            int[] actions = MakeActionCounts(
                AIActionType.Approach,
                count: 3);

            AIValidationScenarioType scenario = AIValidationScenarioTracker.ClassifyFrame(
                MakeFrame(activeBots: 4, targetedBots: 3, targetlessBots: 1),
                actions);

            Assert.AreEqual(AIValidationScenarioType.Combat, scenario);
        }

        [Test]
        public void ClassifyFrame_ReturnsObjective_WhenMostBotsAreTargetlessAndMapActionsWin()
        {
            int[] actions = MakeActionCounts(
                AIActionType.Objective,
                count: 3);

            AIValidationScenarioType scenario = AIValidationScenarioTracker.ClassifyFrame(
                MakeFrame(activeBots: 4, targetedBots: 1, targetlessBots: 3),
                actions);

            Assert.AreEqual(AIValidationScenarioType.Objective, scenario);
        }

        [Test]
        public void ClassifyFrame_ReturnsMixed_WhenSignalsAreSplit()
        {
            int[] actions = MakeActionCounts(
                AIActionType.Approach,
                count: 1);
            actions[(int)AIActionType.Objective] = 1;

            AIValidationScenarioType scenario = AIValidationScenarioTracker.ClassifyFrame(
                MakeFrame(activeBots: 4, targetedBots: 2, targetlessBots: 2),
                actions);

            Assert.AreEqual(AIValidationScenarioType.Mixed, scenario);
        }

        [Test]
        public void RecordFrame_AggregatesScenarioStatsAndHealthOutcomes()
        {
            AIValidationScenarioTracker.RecordFrame(
                MakeFrame(activeBots: 4, targetedBots: 3, targetlessBots: 1, switches: 3),
                MakeActionCounts(AIActionType.HoldRange, 4),
                AIValidationHealthStatus.Watch,
                "ActionFlicker");

            AIValidationScenarioTracker.RecordFrame(
                MakeFrame(activeBots: 4, targetedBots: 1, targetlessBots: 3, invalid: 1),
                MakeActionCounts(AIActionType.Objective, 4),
                AIValidationHealthStatus.Fail,
                "ZeroScores");

            Assert.AreEqual(2, AIValidationScenarioTracker.TotalFrameCount);
            Assert.AreEqual(8, AIValidationScenarioTracker.TotalBotDecisionCount);
            Assert.AreEqual(1, AIValidationScenarioTracker.GetWatchFrameCount(AIValidationScenarioType.Combat));
            Assert.AreEqual(1, AIValidationScenarioTracker.GetFailFrameCount(AIValidationScenarioType.Objective));
            Assert.AreEqual(AIValidationHealthStatus.Watch, AIValidationScenarioTracker.GetWorstStatus(AIValidationScenarioType.Combat));
            Assert.AreEqual(AIValidationHealthStatus.Fail, AIValidationScenarioTracker.GetWorstStatus(AIValidationScenarioType.Objective));
        }

        [Test]
        public void RecordFrame_TracksActionTotalsPerScenario()
        {
            AIValidationScenarioTracker.RecordFrame(
                MakeFrame(activeBots: 5, targetedBots: 4, targetlessBots: 1),
                MakeActionCounts(AIActionType.UseSuper, 2),
                AIValidationHealthStatus.Healthy,
                "Stable");

            Assert.AreEqual(2, AIValidationScenarioTracker.GetActionCount(
                AIValidationScenarioType.Combat,
                AIActionType.UseSuper));
        }

        [Test]
        public void RecordDecision_FinalizesCompletedTicksIntoScenarioTracker()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Approach, 50f),
                true,
                MakeScores(
                    new AIActionScore(AIActionType.Approach, 50f),
                    new AIActionScore(AIActionType.HoldRange, 20f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                2,
                10u,
                new AIActionScore(AIActionType.HoldRange, 45f),
                true,
                MakeScores(
                    new AIActionScore(AIActionType.HoldRange, 45f),
                    new AIActionScore(AIActionType.Reposition, 20f)),
                "A=0");

            Assert.AreEqual(0, AIValidationScenarioTracker.TotalFrameCount);

            AIValidationTelemetry.RecordDecision(
                1,
                11u,
                new AIActionScore(AIActionType.Wander, 5f),
                false,
                MakeScores(new AIActionScore(AIActionType.Wander, 5f)),
                "A=0");

            Assert.AreEqual(1, AIValidationScenarioTracker.TotalFrameCount);
            Assert.AreEqual(2, AIValidationScenarioTracker.GetBotDecisionCount(AIValidationScenarioType.Combat));
            Assert.AreEqual(AIValidationScenarioType.Combat, AIValidationScenarioTracker.CurrentScenario);
        }

        [Test]
        public void Clear_ResetsScenarioState()
        {
            AIValidationScenarioTracker.RecordFrame(
                MakeFrame(activeBots: 4, targetedBots: 3, targetlessBots: 1),
                MakeActionCounts(AIActionType.Approach, 4),
                AIValidationHealthStatus.Fail,
                "InvalidContext");

            AIValidationScenarioTracker.Clear();

            Assert.AreEqual(0, AIValidationScenarioTracker.TotalFrameCount);
            Assert.AreEqual(0, AIValidationScenarioTracker.GetFrameCount(AIValidationScenarioType.Combat));
            Assert.AreEqual(AIValidationScenarioType.None, AIValidationScenarioTracker.CurrentScenario);
            Assert.AreEqual(AIValidationHealthStatus.NoData, AIValidationScenarioTracker.CurrentHealthStatus);
        }

        private static AIValidationFrame MakeFrame(
            int activeBots,
            int targetedBots,
            int targetlessBots,
            int invalid = 0,
            int zero = 0,
            int switches = 0,
            int lowConfidence = 0)
        {
            return new AIValidationFrame
            {
                ActiveBotCount = activeBots,
                TargetedBotCount = targetedBots,
                TargetlessBotCount = targetlessBots,
                InvalidDecisionCount = invalid,
                ZeroScoreDecisionCount = zero,
                ActionSwitchCount = switches,
                LowConfidenceDecisionCount = lowConfidence,
                AverageTopScore = 50f,
                AverageScoreMargin = 12f
            };
        }

        private static int[] MakeActionCounts(AIActionType actionType, int count)
        {
            int[] actionCounts = new int[ActionSlotCount];
            actionCounts[(int)actionType] = count;
            return actionCounts;
        }

        private static List<AIActionScore> MakeScores(params AIActionScore[] scores)
        {
            return new List<AIActionScore>(scores);
        }
    }
}
