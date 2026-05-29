using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIReportCardTrackerTests
    {
        [SetUp]
        public void SetUp()
        {
            AIValidationTelemetry.ResetForTests();
            AIReportCardTracker.ResetForTests();
        }

        [Test]
        public void ValidationTelemetry_FeedsBotAndTeamDecisionReportCards()
        {
            AIReportCardTracker.RegisterBot(101, TeamType.Blue, "Colt");
            AIReportCardTracker.RegisterBot(202, TeamType.Blue, "Byron");

            AIValidationTelemetry.RecordDecision(
                101,
                10u,
                new AIActionScore(AIActionType.Objective, 30f),
                false,
                MakeScores(
                    new AIActionScore(AIActionType.Objective, 30f),
                    new AIActionScore(AIActionType.Wander, 8f)),
                "A=1 Delta=Objective+5.0");

            AIValidationTelemetry.RecordDecision(
                202,
                10u,
                new AIActionScore(AIActionType.Peel, 44f),
                true,
                MakeScores(
                    new AIActionScore(AIActionType.Peel, 44f),
                    new AIActionScore(AIActionType.HoldRange, 41f)),
                "A=0");

            AIReportCardSnapshot bot = AIReportCardTracker.GetBotSnapshot(101, 10u);
            Assert.AreEqual(1, bot.DecisionCount);
            Assert.AreEqual(1, bot.TargetlessDecisionCount);
            Assert.AreEqual(1, bot.ObjectiveDecisionCount);
            Assert.AreEqual(1, bot.TeamRoleAdjustedDecisionCount);
            Assert.AreEqual(30f, bot.AverageTopScore);

            AIReportCardSnapshot team = AIReportCardTracker.GetTeamSnapshot(TeamType.Blue, 10u);
            Assert.AreEqual(2, team.DecisionCount);
            Assert.AreEqual(1, team.ObjectiveDecisionCount);
            Assert.AreEqual(1, team.PeelDecisionCount);
            Assert.AreEqual(2, team.RegisteredBotCount);
        }

        [Test]
        public void AbilityResults_TrackBadCastsAndAgedWastedSupers()
        {
            AIReportCardTracker.RegisterBot(101, TeamType.Blue, "Colt");

            AIReportCardTracker.RecordAbilityResult(
                101,
                AbilitySlotType.Super,
                AbilityExecutionResult.Failed(null, AbilitySlotType.Super),
                10u);

            AbilityExecutionResult pendingSuper =
                AbilityExecutionResult.Succeeded(null, AbilitySlotType.Super);

            AIReportCardTracker.RecordAbilityResult(
                101,
                AbilitySlotType.Super,
                pendingSuper,
                20u);

            AIReportCardSnapshot beforeAging =
                AIReportCardTracker.GetBotSnapshot(101, 100u);

            Assert.AreEqual(2, beforeAging.SuperCastCount);
            Assert.AreEqual(1, beforeAging.FailedCastCount);
            Assert.AreEqual(1, beforeAging.BadCastCount);
            Assert.AreEqual(1, beforeAging.WastedSuperCount);

            AIReportCardSnapshot afterAging =
                AIReportCardTracker.GetBotSnapshot(101, 111u);

            Assert.AreEqual(2, afterAging.WastedSuperCount);
            Assert.AreEqual(1f, afterAging.WastedSuperRatio);
        }

        [Test]
        public void CombatResults_TrackUsefulnessKillsDeathsAndSuperImpact()
        {
            AIReportCardTracker.RegisterBot(101, TeamType.Blue, "Colt");
            AIReportCardTracker.RegisterBot(202, TeamType.Red, "Shelly");

            AIReportCardTracker.RecordAbilityResult(
                101,
                AbilitySlotType.Super,
                AbilityExecutionResult.Succeeded(null, AbilitySlotType.Super),
                10u);

            AIReportCardTracker.RecordCombatResult(
                101,
                202,
                900f,
                true,
                true,
                12u);
            AIReportCardTracker.RecordHealingDone(
                101,
                150f,
                false,
                13u);

            AIReportCardSnapshot attacker =
                AIReportCardTracker.GetBotSnapshot(101, 13u);
            AIReportCardSnapshot victim =
                AIReportCardTracker.GetBotSnapshot(202, 13u);

            Assert.AreEqual(900f, attacker.DamageDealt);
            Assert.AreEqual(150f, attacker.HealingDone);
            Assert.AreEqual(1, attacker.Kills);
            Assert.AreEqual(1, attacker.SuperImpactCount);
            Assert.AreEqual(0, attacker.WastedSuperCount);

            Assert.AreEqual(900f, victim.DamageTaken);
            Assert.AreEqual(1, victim.Deaths);
        }

        [Test]
        public void ObjectiveAndRecoverySignals_AggregateIntoReportCards()
        {
            AIReportCardTracker.RegisterBot(101, TeamType.Blue, "Colt");

            AIReportCardTracker.RecordObjectiveValue(101, 3, 20u);
            AIReportCardTracker.RecordFailureRecovery(
                101,
                AIFailureRecoveryReason.NavigationStall,
                21u);
            AIReportCardTracker.RecordFailureRecovery(
                101,
                AIFailureRecoveryReason.FailedCast,
                22u);

            AIReportCardSnapshot bot =
                AIReportCardTracker.GetBotSnapshot(101, 22u);

            Assert.AreEqual(1, bot.ObjectivePickupCount);
            Assert.AreEqual(3, bot.ObjectiveValue);
            Assert.AreEqual(2, bot.FailureRecoveryCount);
            Assert.AreEqual(1, bot.NavigationStallRecoveryCount);
            Assert.AreEqual(1, bot.FailedCastRecoveryCount);
            StringAssert.Contains("obj=0+3", bot.GetDebugSummary());
            StringAssert.Contains("rec=2", bot.GetDebugSummary());
        }

        private static List<AIActionScore> MakeScores(params AIActionScore[] scores)
        {
            return new List<AIActionScore>(scores);
        }
    }
}
