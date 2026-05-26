using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIValidationGauntletTests
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;

        [SetUp]
        public void SetUp()
        {
            AIValidationTelemetry.ResetForTests();
        }

        [Test]
        public void RetreatSafety_Passes_WhenRetreatAndEvadeDominate()
        {
            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.RetreatSafety,
                10u);

            for (int i = 0; i < 12; i++)
            {
                int[] actions = MakeActionCounts(
                    AIActionType.Retreat,
                    1,
                    AIActionType.Evade,
                    1,
                    AIActionType.HoldRange,
                    1);

                AIValidationGauntlet.RecordFrame(
                    MakeFrame(activeBots: 3, targetedBots: 3, targetlessBots: 0, emergency: 2),
                    actions);
            }

            AIValidationGauntletResult result = AIValidationGauntlet.EndScenario(30u);

            Assert.AreEqual(AIValidationGauntletStatus.Passed, result.Status);
            Assert.GreaterOrEqual(result.ExpectedActionRatio, 0.30f);
            Assert.GreaterOrEqual(result.EmergencyActionRatio, 0.20f);
        }

        [Test]
        public void ObjectivePlay_Fails_WhenBotsStayTargetLocked()
        {
            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.ObjectivePlay,
                10u);

            for (int i = 0; i < 12; i++)
            {
                AIValidationGauntlet.RecordFrame(
                    MakeFrame(activeBots: 3, targetedBots: 3, targetlessBots: 0),
                    MakeActionCounts(AIActionType.Objective, 3));
            }

            AIValidationGauntletResult result = AIValidationGauntlet.EndScenario(30u);

            Assert.AreEqual(AIValidationGauntletStatus.Failed, result.Status);
            Assert.AreEqual("target_ratio_high", result.Reason);
        }

        [Test]
        public void AbilityUsage_RequiresActualAbilitySignal()
        {
            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.AbilityUsage,
                10u);

            RecordAbilityUsageFrames();

            AIValidationGauntletResult missingSignalResult =
                AIValidationGauntlet.EndScenario(30u);

            Assert.AreEqual(AIValidationGauntletStatus.Failed, missingSignalResult.Status);
            Assert.AreEqual("ability_signal_missing", missingSignalResult.Reason);

            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.AbilityUsage,
                40u);

            RecordAbilityUsageFrames();
            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.SuperCast,
                42u);

            AIValidationGauntletResult passResult =
                AIValidationGauntlet.EndScenario(60u);

            Assert.AreEqual(AIValidationGauntletStatus.Passed, passResult.Status);
            Assert.AreEqual(1, passResult.AbilitySignalCount);
        }

        [Test]
        public void StuckRecovery_Passes_WhenFailureRecoverySignalAppears()
        {
            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.StuckRecovery,
                10u);

            for (int i = 0; i < 12; i++)
            {
                AIValidationGauntlet.RecordFrame(
                    MakeFrame(activeBots: 2, targetedBots: 0, targetlessBots: 2),
                    MakeActionCounts(AIActionType.Search, 2));
            }

            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.FailureRecovery,
                15u);

            AIValidationGauntletResult result = AIValidationGauntlet.EndScenario(30u);

            Assert.AreEqual(AIValidationGauntletStatus.Passed, result.Status);
            Assert.AreEqual(1, result.FailureRecoverySignalCount);
        }

        [Test]
        public void TeamCoordination_Passes_WhenActionsAreDiverseAndRoleAdjusted()
        {
            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.TeamCoordination,
                10u);

            for (int i = 0; i < 12; i++)
            {
                int[] actions = MakeActionCounts(
                    AIActionType.Approach,
                    1,
                    AIActionType.HoldRange,
                    1,
                    AIActionType.Peel,
                    1);

                AIValidationGauntlet.RecordFrame(
                    MakeFrame(
                        activeBots: 3,
                        targetedBots: 2,
                        targetlessBots: 1,
                        teamRoleAdjusted: 1),
                    actions);
            }

            AIValidationGauntletResult result = AIValidationGauntlet.EndScenario(30u);

            Assert.AreEqual(AIValidationGauntletStatus.Passed, result.Status);
            Assert.GreaterOrEqual(result.UniqueActionCount, 3);
            Assert.Greater(result.TeamRoleAdjustedRatio, 0f);
        }

        [Test]
        public void RecordSignal_AggregatesSpecificAbilitySignals()
        {
            AIValidationGauntlet.BeginScenario(
                AIValidationGauntletScenarioType.AbilityUsage,
                10u);

            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.MainAttackCast,
                10u);
            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.GadgetCast,
                11u);

            Assert.AreEqual(2, AIValidationGauntlet.GetSignalCount(
                AIValidationGauntletSignal.AbilityCast));
            Assert.AreEqual(1, AIValidationGauntlet.GetSignalCount(
                AIValidationGauntletSignal.MainAttackCast));
            Assert.AreEqual(1, AIValidationGauntlet.GetSignalCount(
                AIValidationGauntletSignal.GadgetCast));
        }

        [Test]
        public void TelemetryFlush_RecordsCompletedFramesIntoActiveGauntlet()
        {
            AIValidationGauntletSpec spec =
                AIValidationGauntlet.CreateDefaultSpec(
                    AIValidationGauntletScenarioType.ObjectivePlay);
            spec.MinimumFrameCount = 1;
            spec.MinimumBotDecisionCount = 1;

            AIValidationGauntlet.BeginScenario(spec, 10u);

            AIValidationTelemetry.RecordDecision(
                1,
                10u,
                new AIActionScore(AIActionType.Objective, 40f),
                false,
                MakeScores(new AIActionScore(AIActionType.Objective, 40f)),
                "A=0");

            AIValidationTelemetry.RecordDecision(
                1,
                11u,
                new AIActionScore(AIActionType.Wander, 5f),
                false,
                MakeScores(new AIActionScore(AIActionType.Wander, 5f)),
                "A=0");

            AIValidationGauntletResult result =
                AIValidationGauntlet.EndScenario(12u);

            Assert.AreEqual(AIValidationGauntletStatus.Passed, result.Status);
            Assert.AreEqual(1, result.FrameCount);
            Assert.AreEqual(1, result.BotDecisionCount);
            Assert.AreEqual(1, result.ExpectedActionCount);
        }

        private static void RecordAbilityUsageFrames()
        {
            for (int i = 0; i < 12; i++)
            {
                AIValidationGauntlet.RecordFrame(
                    MakeFrame(activeBots: 3, targetedBots: 3, targetlessBots: 0),
                    MakeActionCounts(
                        AIActionType.UseSuper,
                        1,
                        AIActionType.Approach,
                        2));
            }
        }

        private static AIValidationFrame MakeFrame(
            int activeBots,
            int targetedBots,
            int targetlessBots,
            int invalid = 0,
            int zero = 0,
            int switches = 0,
            int lowConfidence = 0,
            int emergency = 0,
            int teamRoleAdjusted = 0)
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
                EmergencyActionCount = emergency,
                TeamRoleAdjustedDecisionCount = teamRoleAdjusted,
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

        private static int[] MakeActionCounts(
            AIActionType firstAction,
            int firstCount,
            AIActionType secondAction,
            int secondCount)
        {
            int[] actionCounts = MakeActionCounts(firstAction, firstCount);
            actionCounts[(int)secondAction] = secondCount;
            return actionCounts;
        }

        private static int[] MakeActionCounts(
            AIActionType firstAction,
            int firstCount,
            AIActionType secondAction,
            int secondCount,
            AIActionType thirdAction,
            int thirdCount)
        {
            int[] actionCounts = MakeActionCounts(
                firstAction,
                firstCount,
                secondAction,
                secondCount);
            actionCounts[(int)thirdAction] = thirdCount;
            return actionCounts;
        }

        private static System.Collections.Generic.List<AIActionScore> MakeScores(
            params AIActionScore[] scores)
        {
            return new System.Collections.Generic.List<AIActionScore>(scores);
        }
    }
}
