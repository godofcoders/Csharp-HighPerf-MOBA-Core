using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIPerformanceTrackerTests
    {
        [SetUp]
        public void SetUp()
        {
            AIPerformanceTracker.ResetForTests();
        }

        [Test]
        public void RecordMapResolve_TracksCacheHitsCandidatesAndValidations()
        {
            AIPerformanceTracker.RecordMapResolve(
                10u,
                AIMapRouteIntent.Search,
                false,
                18,
                2);

            AIPerformanceTracker.RecordMapResolve(
                10u,
                AIMapRouteIntent.Search,
                true,
                0,
                0);

            Assert.AreEqual(2, AIPerformanceTracker.MapResolveCount);
            Assert.AreEqual(1, AIPerformanceTracker.MapResolveCacheHits);
            Assert.AreEqual(18, AIPerformanceTracker.MapCandidateCount);
            Assert.AreEqual(2, AIPerformanceTracker.MapPathValidationCount);
        }

        [Test]
        public void RecordPathQuery_TracksSuccessFailureAndTouchedNodes()
        {
            AIPerformanceTracker.RecordPathQuery(12u, true, 24);
            AIPerformanceTracker.RecordPathQuery(12u, false, 7);

            Assert.AreEqual(2, AIPerformanceTracker.PathQueryCount);
            Assert.AreEqual(1, AIPerformanceTracker.PathQuerySuccessCount);
            Assert.AreEqual(1, AIPerformanceTracker.PathQueryFailureCount);
            Assert.AreEqual(31, AIPerformanceTracker.PathTouchedNodeCount);
            Assert.AreEqual(24, AIPerformanceTracker.PathMaxTouchedNodes);
        }

        [Test]
        public void Recorders_ResetWhenTickChanges()
        {
            AIPerformanceTracker.RecordPathQuery(20u, true, 10);
            AIPerformanceTracker.RecordMapResolve(
                21u,
                AIMapRouteIntent.Objective,
                false,
                8,
                1);

            Assert.AreEqual(0, AIPerformanceTracker.PathQueryCount);
            Assert.AreEqual(1, AIPerformanceTracker.MapResolveCount);
        }

        [Test]
        public void BudgetSummary_FlagsOverBudgetFrames()
        {
            AIPerformanceTracker.RecordMapResolve(
                30u,
                AIMapRouteIntent.CombatAdvance,
                false,
                12,
                2);
            AIPerformanceTracker.RecordMapResolve(
                30u,
                AIMapRouteIntent.CombatAdvance,
                false,
                12,
                2);
            AIPerformanceTracker.RecordPathQuery(30u, true, 80);

            Assert.IsTrue(AIPerformanceTracker.IsOverBudget(
                maxMapResolves: 1,
                maxPathQueries: 4,
                maxTouchedNodes: 200));
            StringAssert.Contains(
                "Budget=OVER",
                AIPerformanceTracker.GetBudgetSummary(
                    30u,
                    maxMapResolves: 1,
                    maxPathQueries: 4,
                    maxTouchedNodes: 200));
        }

        [Test]
        public void BudgetSummary_ClampsInvalidBudgetsToOne()
        {
            AIPerformanceTracker.RecordPathQuery(40u, true, 1);

            string summary = AIPerformanceTracker.GetBudgetSummary(
                40u,
                maxMapResolves: 0,
                maxPathQueries: 0,
                maxTouchedNodes: 0);

            StringAssert.Contains("map=0/1", summary);
            StringAssert.Contains("paths=1/1", summary);
            StringAssert.Contains("nodes=1/1", summary);
        }

        [Test]
        public void Recorders_IgnoreNegativeTelemetryCounts()
        {
            AIPerformanceTracker.RecordMapResolve(
                50u,
                AIMapRouteIntent.Search,
                false,
                -10,
                -2);
            AIPerformanceTracker.RecordPathQuery(50u, true, -100);

            Assert.AreEqual(0, AIPerformanceTracker.MapCandidateCount);
            Assert.AreEqual(0, AIPerformanceTracker.MapPathValidationCount);
            Assert.AreEqual(0, AIPerformanceTracker.PathTouchedNodeCount);
            Assert.AreEqual(0, AIPerformanceTracker.PathMaxTouchedNodes);
        }
    }
}
