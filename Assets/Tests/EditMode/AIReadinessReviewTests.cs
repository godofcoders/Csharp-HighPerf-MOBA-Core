using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIReadinessReviewTests
    {
        [Test]
        public void Evaluate_ReturnsHealthy_WhenSignalsAreStable()
        {
            AIReadinessReviewSnapshot review =
                AIReadinessReview.Evaluate(
                    120u,
                    Match(),
                    Trend(),
                    Gauntlet(AIValidationGauntletStatus.Passed, "passed"));

            Assert.AreEqual(AIValidationHealthStatus.Healthy, review.Status);
            Assert.AreEqual("Stable", review.PrimarySignal);
            StringAssert.Contains("AIReady=OK", review.GetDebugSummary());
        }

        [Test]
        public void Evaluate_Fails_WhenGauntletFails()
        {
            AIReadinessReviewSnapshot review =
                AIReadinessReview.Evaluate(
                    120u,
                    Match(),
                    Trend(),
                    Gauntlet(AIValidationGauntletStatus.Failed, "objective_neglect_high"));

            Assert.AreEqual(AIValidationHealthStatus.Fail, review.Status);
            Assert.AreEqual("Gauntlet:objective_neglect_high", review.PrimarySignal);
        }

        [Test]
        public void Evaluate_Watches_WhenTrendIsNonHealthy()
        {
            AIReadinessReviewSnapshot review =
                AIReadinessReview.Evaluate(
                    120u,
                    Match(),
                    Trend(AIValidationHealthStatus.Watch, "ActionFlicker"),
                    Gauntlet(AIValidationGauntletStatus.Passed, "passed"));

            Assert.AreEqual(AIValidationHealthStatus.Watch, review.Status);
            Assert.AreEqual("Trend:ActionFlicker", review.PrimarySignal);
        }

        [Test]
        public void Evaluate_Watches_WhenActionDiversityIsTooLow()
        {
            AIReadinessReviewSnapshot review =
                AIReadinessReview.Evaluate(
                    120u,
                    Match(uniqueActions: 2),
                    Trend(),
                    Gauntlet(AIValidationGauntletStatus.Passed, "passed"));

            Assert.AreEqual(AIValidationHealthStatus.Watch, review.Status);
            Assert.AreEqual("LowActionDiversity", review.PrimarySignal);
        }

        [Test]
        public void Evaluate_ReturnsNoData_WhenNothingRecorded()
        {
            AIReadinessReviewSnapshot review =
                AIReadinessReview.Evaluate(
                    120u,
                    Match(AIValidationHealthStatus.NoData, "NoData", decisions: 0),
                    Trend(AIValidationHealthStatus.NoData, "NoData"),
                    Gauntlet(AIValidationGauntletStatus.NotStarted, "not_started", AIValidationGauntletScenarioType.None));

            Assert.AreEqual(AIValidationHealthStatus.NoData, review.Status);
            Assert.AreEqual("NoData", review.PrimarySignal);
        }

        private static AIMatchTelemetryReviewSnapshot Match(
            AIValidationHealthStatus status = AIValidationHealthStatus.Healthy,
            string signal = "Stable",
            int decisions = 80,
            int uniqueActions = 5)
        {
            return new AIMatchTelemetryReviewSnapshot(
                120u,
                status,
                signal,
                status,
                signal,
                registeredBotCount: decisions > 0 ? 6 : 0,
                teamDecisionCount: decisions,
                windowBotDecisionCount: decisions,
                objectiveDecisionCount: decisions > 0 ? 8 : 0,
                objectiveValue: decisions > 0 ? 3 : 0,
                abilityCastCount: decisions > 0 ? 10 : 0,
                badCastCount: 0,
                superCastCount: decisions > 0 ? 2 : 0,
                wastedSuperCount: 0,
                failureRecoveryCount: 0,
                idleHesitationRecoveryCount: 0,
                isPerformanceOverBudget: false,
                objectiveDecisionRatio: decisions > 0 ? 0.10f : 0f,
                badCastRatio: 0f,
                wastedSuperRatio: 0f,
                failureRecoveryRatio: 0f,
                idleHesitationRatio: 0f,
                pathFailureRatio: 0f,
                invalidDecisionRatio: 0f,
                zeroScoreRatio: 0f,
                actionSwitchRatio: 0.15f,
                lowConfidenceRatio: 0.20f,
                dominantActionType: AIActionType.HoldRange,
                dominantActionRatio: 0.35f,
                uniqueActionCount: uniqueActions);
        }

        private static AIMatchTelemetryTrendSnapshot Trend(
            AIValidationHealthStatus status = AIValidationHealthStatus.Healthy,
            string signal = "Stable")
        {
            return new AIMatchTelemetryTrendSnapshot(
                status,
                signal,
                sampleCount: status == AIValidationHealthStatus.NoData ? 0 : 12,
                healthySampleCount: status == AIValidationHealthStatus.Healthy ? 12 : 8,
                watchSampleCount: status == AIValidationHealthStatus.Watch ? 4 : 0,
                failSampleCount: status == AIValidationHealthStatus.Fail ? 4 : 0,
                consecutiveNonHealthySamples: status == AIValidationHealthStatus.Healthy ? 0 : 4,
                consecutiveFailSamples: status == AIValidationHealthStatus.Fail ? 4 : 0,
                lastStatus: status,
                lastSignal: signal,
                dominantSignal: signal,
                nonHealthyRatio: status == AIValidationHealthStatus.Healthy ? 0f : 0.34f,
                failRatio: status == AIValidationHealthStatus.Fail ? 0.34f : 0f);
        }

        private static AIValidationGauntletResult Gauntlet(
            AIValidationGauntletStatus status,
            string reason,
            AIValidationGauntletScenarioType scenario = AIValidationGauntletScenarioType.PlaytestRegression)
        {
            return new AIValidationGauntletResult
            {
                ScenarioType = scenario,
                Status = status,
                Reason = reason,
                FrameCount = status == AIValidationGauntletStatus.NotStarted ? 0 : 24,
                BotDecisionCount = status == AIValidationGauntletStatus.NotStarted ? 0 : 72
            };
        }
    }
}
