using System.Collections.Generic;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIBotTelemetryOutlierReviewTests
    {
        [Test]
        public void Evaluate_ReturnsHealthy_WhenBotsAreStable()
        {
            AIBotTelemetryOutlierSnapshot review =
                AIBotTelemetryOutlierReview.Evaluate(
                    MakeBots(
                        Bot(101, "Colt", decisions: 60, abilityCasts: 10),
                        Bot(202, "Byron", decisions: 60, abilityCasts: 8)));

            Assert.AreEqual(AIValidationHealthStatus.Healthy, review.Status);
            Assert.AreEqual(2, review.CandidateCount);
            StringAssert.Contains("BotOutlier=None", review.GetDebugSummary());
        }

        [Test]
        public void Evaluate_FailsWorstBot_WhenInvalidContextAppears()
        {
            AIBotTelemetryOutlierSnapshot review =
                AIBotTelemetryOutlierReview.Evaluate(
                    MakeBots(
                        Bot(101, "Colt", decisions: 60, abilityCasts: 10),
                        Bot(202, "Shelly", decisions: 30, invalid: 1)));

            Assert.AreEqual(AIValidationHealthStatus.Fail, review.Status);
            Assert.AreEqual(202, review.EntityId);
            Assert.AreEqual("InvalidContext", review.Reason);
        }

        [Test]
        public void Evaluate_WatchesWorstBot_WhenIdleRecoveryPressureIsHigh()
        {
            AIBotTelemetryOutlierSnapshot review =
                AIBotTelemetryOutlierReview.Evaluate(
                    MakeBots(
                        Bot(101, "Colt", decisions: 100),
                        Bot(202, "Jessie", decisions: 100, recoveries: 9, idle: 9)));

            Assert.AreEqual(AIValidationHealthStatus.Watch, review.Status);
            Assert.AreEqual(202, review.EntityId);
            Assert.AreEqual("IdleHesitation", review.Reason);
            Assert.AreEqual(0.09f, review.IdleHesitationRatio, 0.0001f);
        }

        [Test]
        public void Evaluate_IgnoresTeamSnapshots()
        {
            AIBotTelemetryOutlierSnapshot review =
                AIBotTelemetryOutlierReview.Evaluate(
                    MakeBots(
                        new AIReportCardSnapshot
                        {
                            IsTeamSnapshot = true,
                            DecisionCount = 100,
                            InvalidDecisionCount = 3
                        }));

            Assert.AreEqual(AIValidationHealthStatus.NoData, review.Status);
        }

        private static List<AIReportCardSnapshot> MakeBots(
            params AIReportCardSnapshot[] snapshots)
        {
            return new List<AIReportCardSnapshot>(snapshots);
        }

        private static AIReportCardSnapshot Bot(
            int entityId,
            string name,
            int decisions,
            int invalid = 0,
            int abilityCasts = 0,
            int badCasts = 0,
            int superCasts = 0,
            int wastedSupers = 0,
            int objectiveDecisions = 5,
            int objectiveValue = 1,
            int recoveries = 0,
            int idle = 0)
        {
            return new AIReportCardSnapshot
            {
                EntityId = entityId,
                Name = name,
                Team = TeamType.Blue,
                DecisionCount = decisions,
                InvalidDecisionCount = invalid,
                AbilityCastCount = abilityCasts,
                BadCastCount = badCasts,
                SuperCastCount = superCasts,
                WastedSuperCount = wastedSupers,
                ObjectiveDecisionCount = objectiveDecisions,
                ObjectiveValue = objectiveValue,
                FailureRecoveryCount = recoveries,
                IdleHesitationRecoveryCount = idle
            };
        }
    }
}
