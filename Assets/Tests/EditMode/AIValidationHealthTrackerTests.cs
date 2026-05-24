using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIValidationHealthTrackerTests
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;

        [SetUp]
        public void SetUp()
        {
            AIValidationTelemetry.ResetForTests();
            AIValidationHealthTracker.ResetForTests();
        }

        [Test]
        public void RecordFrame_Fails_WhenInvalidDecisionExists()
        {
            RecordFrame(
                tick: 10u,
                activeBots: 3,
                invalid: 1,
                action: AIActionType.Approach,
                actionCount: 3);

            Assert.AreEqual(AIValidationHealthStatus.Fail, AIValidationHealthTracker.Status);
            Assert.AreEqual("InvalidContext", AIValidationHealthTracker.PrimarySignal);
        }

        [Test]
        public void RecordFrame_Fails_WhenZeroScoresAreSustained()
        {
            RecordFrame(
                tick: 10u,
                activeBots: 20,
                zero: 3,
                action: AIActionType.Wander,
                actionCount: 4);

            Assert.AreEqual(AIValidationHealthStatus.Fail, AIValidationHealthTracker.Status);
            Assert.AreEqual("ZeroScores", AIValidationHealthTracker.PrimarySignal);
        }

        [Test]
        public void RecordFrame_Watches_WhenSwitchRatioIsTooHigh()
        {
            RecordFrame(
                tick: 10u,
                activeBots: 20,
                switches: 14,
                action: AIActionType.HoldRange,
                actionCount: 8);

            Assert.AreEqual(AIValidationHealthStatus.Watch, AIValidationHealthTracker.Status);
            Assert.AreEqual("ActionFlicker", AIValidationHealthTracker.PrimarySignal);
        }

        [Test]
        public void RecordFrame_Watches_WhenActionDistributionCollapses()
        {
            RecordFrame(
                tick: 10u,
                activeBots: 30,
                action: AIActionType.Approach,
                actionCount: 28);

            Assert.AreEqual(AIValidationHealthStatus.Watch, AIValidationHealthTracker.Status);
            Assert.AreEqual("ActionCollapse", AIValidationHealthTracker.PrimarySignal);
            Assert.AreEqual(AIActionType.Approach, AIValidationHealthTracker.DominantActionType);
        }

        [Test]
        public void RecordFrame_DropsOldFrames_FromRollingWindow()
        {
            RecordFrame(
                tick: 1u,
                activeBots: 3,
                invalid: 1,
                action: AIActionType.Approach,
                actionCount: 1);

            for (int i = 0; i < AIValidationHealthTracker.WindowFrameCapacity; i++)
            {
                AIActionType action = (AIActionType)((i % (ActionSlotCount - 1)) + 1);
                RecordFrame(
                    tick: (uint)(2 + i),
                    activeBots: 1,
                    action: action,
                    actionCount: 1);
            }

            Assert.AreEqual(AIValidationHealthTracker.WindowFrameCapacity, AIValidationHealthTracker.WindowFrameCount);
            Assert.AreEqual(0, AIValidationHealthTracker.InvalidDecisionTotal);
            Assert.AreEqual(AIValidationHealthStatus.Healthy, AIValidationHealthTracker.Status);
        }

        [Test]
        public void RecordDecision_FinalizesCompletedTicksIntoHealthWindow()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Approach, 25f),
                false,
                null,
                "A=0");

            Assert.AreEqual(AIValidationHealthStatus.NoData, AIValidationHealthTracker.Status);

            AIValidationTelemetry.RecordDecision(
                1,
                11u,
                new AIActionScore(AIActionType.Wander, 5f),
                false,
                null,
                "A=0");

            Assert.AreEqual(AIValidationHealthStatus.Fail, AIValidationHealthTracker.Status);
            Assert.AreEqual("InvalidContext", AIValidationHealthTracker.PrimarySignal);
        }

        [Test]
        public void RecordDecision_ClearsHealthWindow_WhenSimulationTickRewinds()
        {
            AIValidationTelemetry.RecordDecision(
                1,
                100u,
                new AIActionScore(AIActionType.Approach, 25f),
                false,
                null,
                "A=0");

            AIValidationTelemetry.RecordDecision(
                1,
                101u,
                new AIActionScore(AIActionType.Wander, 5f),
                false,
                null,
                "A=0");

            Assert.AreEqual(AIValidationHealthStatus.Fail, AIValidationHealthTracker.Status);

            AIValidationTelemetry.RecordDecision(
                1,
                1u,
                new AIActionScore(AIActionType.Wander, 5f),
                false,
                null,
                "A=0");

            Assert.AreEqual(AIValidationHealthStatus.NoData, AIValidationHealthTracker.Status);
            Assert.AreEqual(0, AIValidationHealthTracker.BotDecisionCount);
        }

        private static void RecordFrame(
            uint tick,
            int activeBots,
            int invalid = 0,
            int zero = 0,
            int switches = 0,
            int lowConfidence = 0,
            AIActionType action = AIActionType.Wander,
            int actionCount = 0)
        {
            int[] actionCounts = new int[ActionSlotCount];
            if (action > AIActionType.None && (int)action < actionCounts.Length)
                actionCounts[(int)action] = actionCount;

            AIValidationHealthTracker.RecordFrame(
                new AIValidationFrame
                {
                    Tick = tick,
                    ActiveBotCount = activeBots,
                    TargetedBotCount = activeBots,
                    ActionSwitchCount = switches,
                    InvalidDecisionCount = invalid,
                    LowConfidenceDecisionCount = lowConfidence,
                    ZeroScoreDecisionCount = zero,
                    AverageTopScore = 50f,
                    AverageScoreMargin = 12f
                },
                actionCounts);
        }
    }
}
