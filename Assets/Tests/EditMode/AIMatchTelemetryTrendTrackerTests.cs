using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIMatchTelemetryTrendTrackerTests
    {
        [SetUp]
        public void SetUp()
        {
            AIMatchTelemetryTrendTracker.ResetForTests();
        }

        [Test]
        public void Record_KeepsTrendHealthy_ForSingleFailSpike()
        {
            for (uint tick = 1u; tick <= 4u; tick++)
            {
                AIMatchTelemetryTrendTracker.Record(
                    Review(tick, AIValidationHealthStatus.Healthy, "Stable"));
            }

            AIMatchTelemetryTrendSnapshot trend =
                AIMatchTelemetryTrendTracker.Record(
                    Review(5u, AIValidationHealthStatus.Fail, "PerfBudget"));

            Assert.AreEqual(AIValidationHealthStatus.Healthy, trend.Status);
            Assert.AreEqual(AIValidationHealthStatus.Fail, trend.LastStatus);
            Assert.AreEqual(1, trend.FailSampleCount);
        }

        [Test]
        public void Record_Watches_WhenNonHealthySamplesPersist()
        {
            AIMatchTelemetryTrendSnapshot trend =
                AIMatchTelemetryTrendTracker.LastSnapshot;
            for (uint tick = 1u; tick <= 5u; tick++)
            {
                trend = AIMatchTelemetryTrendTracker.Record(
                    Review(tick, AIValidationHealthStatus.Watch, "ActionFlicker"));
            }

            Assert.AreEqual(AIValidationHealthStatus.Watch, trend.Status);
            Assert.AreEqual("ActionFlicker", trend.PrimarySignal);
            Assert.AreEqual(5, trend.ConsecutiveNonHealthySamples);
        }

        [Test]
        public void Record_Fails_WhenFailSamplesPersist()
        {
            AIMatchTelemetryTrendSnapshot trend =
                AIMatchTelemetryTrendTracker.LastSnapshot;
            for (uint tick = 1u; tick <= 3u; tick++)
            {
                trend = AIMatchTelemetryTrendTracker.Record(
                    Review(tick, AIValidationHealthStatus.Fail, "InvalidContext"));
            }

            Assert.AreEqual(AIValidationHealthStatus.Fail, trend.Status);
            Assert.AreEqual("InvalidContext", trend.PrimarySignal);
            Assert.AreEqual(3, trend.ConsecutiveFailSamples);
        }

        [Test]
        public void Record_DeduplicatesSameTickSamples()
        {
            AIMatchTelemetryTrendTracker.Record(
                Review(10u, AIValidationHealthStatus.Watch, "ActionFlicker"));
            AIMatchTelemetryTrendSnapshot trend =
                AIMatchTelemetryTrendTracker.Record(
                    Review(10u, AIValidationHealthStatus.Fail, "PerfBudget"));

            Assert.AreEqual(1, trend.SampleCount);
            Assert.AreEqual(AIValidationHealthStatus.Watch, trend.LastStatus);
            Assert.AreEqual(0, trend.FailSampleCount);
        }

        [Test]
        public void Record_ClearsTrend_WhenTickRewinds()
        {
            for (uint tick = 10u; tick <= 12u; tick++)
            {
                AIMatchTelemetryTrendTracker.Record(
                    Review(tick, AIValidationHealthStatus.Fail, "PerfBudget"));
            }

            AIMatchTelemetryTrendSnapshot trend =
                AIMatchTelemetryTrendTracker.Record(
                    Review(5u, AIValidationHealthStatus.Healthy, "Stable"));

            Assert.AreEqual(AIValidationHealthStatus.Healthy, trend.Status);
            Assert.AreEqual(1, trend.SampleCount);
            Assert.AreEqual(0, trend.FailSampleCount);
        }

        private static AIMatchTelemetryReviewSnapshot Review(
            uint tick,
            AIValidationHealthStatus status,
            string signal)
        {
            return new AIMatchTelemetryReviewSnapshot(
                tick,
                status,
                signal,
                status,
                signal,
                6,
                100,
                100,
                10,
                3,
                12,
                1,
                2,
                0,
                0,
                0,
                false,
                0.10f,
                0.08f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0.20f,
                0.20f,
                AIActionType.HoldRange,
                0.40f,
                6);
        }
    }
}
