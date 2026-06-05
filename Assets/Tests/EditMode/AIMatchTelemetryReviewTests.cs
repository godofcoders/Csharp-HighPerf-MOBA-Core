using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIMatchTelemetryReviewTests
    {
        [Test]
        public void Evaluate_ReturnsHealthy_WhenMatchSignalsAreStable()
        {
            AIMatchTelemetryReviewSnapshot review =
                AIMatchTelemetryReview.Evaluate(Context());

            Assert.AreEqual(AIValidationHealthStatus.Healthy, review.Status);
            Assert.AreEqual("Stable", review.PrimarySignal);
            StringAssert.Contains("MatchReview=OK", review.GetDebugSummary());
        }

        [Test]
        public void Evaluate_Watches_WhenObjectiveInterestIsMissing()
        {
            AIMatchTelemetryReviewSnapshot review =
                AIMatchTelemetryReview.Evaluate(
                    Context(
                        decisions: 80,
                        objectiveDecisions: 0,
                        objectiveValue: 0));

            Assert.AreEqual(AIValidationHealthStatus.Watch, review.Status);
            Assert.AreEqual("ObjectiveNeglect", review.PrimarySignal);
        }

        [Test]
        public void Evaluate_Watches_WhenBadCastsAreHigh()
        {
            AIMatchTelemetryReviewSnapshot review =
                AIMatchTelemetryReview.Evaluate(
                    Context(
                        abilityCasts: 8,
                        badCasts: 4));

            Assert.AreEqual(AIValidationHealthStatus.Watch, review.Status);
            Assert.AreEqual("BadCasts", review.PrimarySignal);
            Assert.AreEqual(0.5f, review.BadCastRatio);
        }

        [Test]
        public void Evaluate_Fails_WhenPerformanceExceedsBudget()
        {
            AIMatchTelemetryReviewSnapshot review =
                AIMatchTelemetryReview.Evaluate(
                    Context(
                        mapResolves: 12,
                        maxMapResolves: 8));

            Assert.AreEqual(AIValidationHealthStatus.Fail, review.Status);
            Assert.AreEqual("PerfBudget", review.PrimarySignal);
            Assert.IsTrue(review.IsPerformanceOverBudget);
        }

        [Test]
        public void Evaluate_Watches_WhenIdleRecoveryPressureIsHigh()
        {
            AIMatchTelemetryReviewSnapshot review =
                AIMatchTelemetryReview.Evaluate(
                    Context(
                        decisions: 100,
                        idleRecoveries: 9));

            Assert.AreEqual(AIValidationHealthStatus.Watch, review.Status);
            Assert.AreEqual("IdleHesitation", review.PrimarySignal);
            Assert.AreEqual(0.09f, review.IdleHesitationRatio, 0.0001f);
        }

        private static AIMatchTelemetryReviewContext Context(
            int decisions = 80,
            int objectiveDecisions = 8,
            int objectiveValue = 3,
            int abilityCasts = 10,
            int badCasts = 1,
            int superCasts = 2,
            int wastedSupers = 0,
            int recoveries = 0,
            int idleRecoveries = 0,
            int mapResolves = 2,
            int pathQueries = 8,
            int pathFailures = 0,
            int touchedNodes = 120,
            int maxMapResolves = 16,
            int maxPathQueries = 16,
            int maxTouchedNodes = 2000)
        {
            int blueDecisions = decisions / 2;
            int redDecisions = decisions - blueDecisions;
            int safePathFailures = pathFailures < 0 ? 0 : pathFailures;
            int safePathSuccesses = pathQueries - safePathFailures;
            if (safePathSuccesses < 0)
                safePathSuccesses = 0;

            return new AIMatchTelemetryReviewContext
            {
                Tick = 120u,
                BlueTeam = TeamSnapshot(
                    blueDecisions,
                    objectiveDecisions / 2,
                    objectiveValue / 2,
                    abilityCasts / 2,
                    badCasts / 2,
                    superCasts / 2,
                    wastedSupers / 2,
                    recoveries / 2,
                    idleRecoveries / 2),
                RedTeam = TeamSnapshot(
                    redDecisions,
                    objectiveDecisions - objectiveDecisions / 2,
                    objectiveValue - objectiveValue / 2,
                    abilityCasts - abilityCasts / 2,
                    badCasts - badCasts / 2,
                    superCasts - superCasts / 2,
                    wastedSupers - wastedSupers / 2,
                    recoveries - recoveries / 2,
                    idleRecoveries - idleRecoveries / 2),
                HealthStatus = AIValidationHealthStatus.Healthy,
                HealthSignal = "Stable",
                WindowBotDecisionCount = decisions,
                InvalidDecisionRatio = 0f,
                ZeroScoreRatio = 0f,
                ActionSwitchRatio = 0.12f,
                LowConfidenceRatio = 0.20f,
                DominantActionType = AIActionType.HoldRange,
                DominantActionRatio = 0.40f,
                UniqueActionCount = 6,
                Performance = new AIPerformanceSnapshot(
                    mapResolves,
                    1,
                    4,
                    1,
                    pathQueries,
                    safePathSuccesses,
                    safePathFailures,
                    touchedNodes,
                    touchedNodes),
                Limits = new AIMatchTelemetryReviewLimits(
                    maxMapResolves,
                    maxPathQueries,
                    maxTouchedNodes)
            };
        }

        private static AIReportCardSnapshot TeamSnapshot(
            int decisions,
            int objectiveDecisions,
            int objectiveValue,
            int abilityCasts,
            int badCasts,
            int superCasts,
            int wastedSupers,
            int recoveries,
            int idleRecoveries)
        {
            return new AIReportCardSnapshot
            {
                RegisteredBotCount = 3,
                IsTeamSnapshot = true,
                DecisionCount = decisions,
                ObjectiveDecisionCount = objectiveDecisions,
                ObjectiveValue = objectiveValue,
                AbilityCastCount = abilityCasts,
                BadCastCount = badCasts,
                SuperCastCount = superCasts,
                WastedSuperCount = wastedSupers,
                FailureRecoveryCount = recoveries,
                IdleHesitationRecoveryCount = idleRecoveries
            };
        }
    }
}
