using NUnit.Framework;
using MOBA.Core.Simulation.AI;

namespace MOBA.Tests.EditMode
{
    public class AITeamFocusTrackerTests
    {
        [Test]
        public void ReportFocus_CountsBotsPerTarget()
        {
            AITeamFocusTracker tracker = new AITeamFocusTracker();

            tracker.ReportFocus(1, 100);
            tracker.ReportFocus(2, 100);
            tracker.ReportFocus(3, 200);

            Assert.AreEqual(2, tracker.GetFocusCount(100));
            Assert.AreEqual(1, tracker.GetFocusCount(200));
        }

        [Test]
        public void ReportFocus_MovesBotBetweenTargets()
        {
            AITeamFocusTracker tracker = new AITeamFocusTracker();

            tracker.ReportFocus(1, 100);
            tracker.ReportFocus(2, 100);
            tracker.ReportFocus(1, 200);

            Assert.AreEqual(1, tracker.GetFocusCount(100));
            Assert.AreEqual(1, tracker.GetFocusCount(200));
        }

        [Test]
        public void GetFocusCountExcluding_RemovesOnlyExcludedBot()
        {
            AITeamFocusTracker tracker = new AITeamFocusTracker();

            tracker.ReportFocus(1, 100);
            tracker.ReportFocus(2, 100);
            tracker.ReportFocus(3, 200);

            Assert.AreEqual(1, tracker.GetFocusCountExcluding(100, 1));
            Assert.AreEqual(2, tracker.GetFocusCountExcluding(100, 3));
        }

        [Test]
        public void ClearFocus_RemovesBotContribution()
        {
            AITeamFocusTracker tracker = new AITeamFocusTracker();

            tracker.ReportFocus(1, 100);
            tracker.ReportFocus(2, 100);

            tracker.ClearFocus(1);

            Assert.AreEqual(1, tracker.GetFocusCount(100));
            Assert.AreEqual(0, tracker.GetFocusCountExcluding(100, 2));
        }
    }
}
