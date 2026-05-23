using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AITeamActionTrackerTests
    {
        [Test]
        public void ReportAction_CountsBotsPerAction()
        {
            AITeamActionTracker tracker = new AITeamActionTracker();

            tracker.ReportAction(1, AIActionType.Approach, 10u);
            tracker.ReportAction(2, AIActionType.Approach, 10u);
            tracker.ReportAction(3, AIActionType.Peel, 10u);

            Assert.AreEqual(2, tracker.GetActionCount(AIActionType.Approach, 10u, 12u));
            Assert.AreEqual(1, tracker.GetActionCount(AIActionType.Peel, 10u, 12u));
        }

        [Test]
        public void ReportAction_MovesBotBetweenActions()
        {
            AITeamActionTracker tracker = new AITeamActionTracker();

            tracker.ReportAction(1, AIActionType.Approach, 10u);
            tracker.ReportAction(2, AIActionType.Approach, 10u);
            tracker.ReportAction(1, AIActionType.HoldRange, 11u);

            Assert.AreEqual(1, tracker.GetActionCount(AIActionType.Approach, 11u, 12u));
            Assert.AreEqual(1, tracker.GetActionCount(AIActionType.HoldRange, 11u, 12u));
        }

        [Test]
        public void GetActionCountExcluding_RemovesOnlyExcludedBot()
        {
            AITeamActionTracker tracker = new AITeamActionTracker();

            tracker.ReportAction(1, AIActionType.Objective, 10u);
            tracker.ReportAction(2, AIActionType.Objective, 10u);
            tracker.ReportAction(3, AIActionType.Search, 10u);

            Assert.AreEqual(1, tracker.GetActionCountExcluding(AIActionType.Objective, 1, 10u, 12u));
            Assert.AreEqual(2, tracker.GetActionCountExcluding(AIActionType.Objective, 3, 10u, 12u));
        }

        [Test]
        public void ClearAction_RemovesBotContribution()
        {
            AITeamActionTracker tracker = new AITeamActionTracker();

            tracker.ReportAction(1, AIActionType.Regroup, 10u);
            tracker.ReportAction(2, AIActionType.Regroup, 10u);

            tracker.ClearAction(1);

            Assert.AreEqual(1, tracker.GetActionCount(AIActionType.Regroup, 10u, 12u));
            Assert.AreEqual(0, tracker.GetActionCountExcluding(AIActionType.Regroup, 2, 10u, 12u));
        }

        [Test]
        public void GetActionCount_PurgesStaleReservations()
        {
            AITeamActionTracker tracker = new AITeamActionTracker();

            tracker.ReportAction(1, AIActionType.Approach, 10u);
            tracker.ReportAction(2, AIActionType.Approach, 25u);

            Assert.AreEqual(1, tracker.GetActionCount(AIActionType.Approach, 25u, 12u));
        }
    }
}
