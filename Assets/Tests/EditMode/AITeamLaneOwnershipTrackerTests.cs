using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AITeamLaneOwnershipTrackerTests
    {
        [Test]
        public void GetSnapshot_KeepsStableAnchor_WhenLaneIsOverOwned()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(10, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(11, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(12, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot anchor = tracker.GetSnapshot(10, 101u, 30u);

            Assert.AreEqual(AITeamLaneAssignment.Mid, anchor.RecommendedLane);
            Assert.IsTrue(anchor.CurrentLaneOverOwned);
            Assert.IsFalse(anchor.ShouldRotate);
        }

        [Test]
        public void GetSnapshot_RotatesNonAnchor_ToUnderOwnedLane()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(10, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(11, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot rotating = tracker.GetSnapshot(11, 101u, 30u);

            Assert.IsTrue(rotating.ShouldRotate);
            Assert.AreNotEqual(AITeamLaneAssignment.Mid, rotating.RecommendedLane);
            Assert.AreEqual("rebalance_underowned", rotating.Reason);
        }

        [Test]
        public void GetSnapshot_RecoversAbandonedAssignedLane()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(31, AITeamLaneAssignment.Left, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot snapshot = tracker.GetSnapshot(31, 101u, 30u);

            Assert.AreEqual(AITeamLaneAssignment.Mid, snapshot.AssignedLane);
            Assert.IsTrue(snapshot.AssignedLaneAbandoned);
            Assert.AreEqual(AITeamLaneAssignment.Mid, snapshot.RecommendedLane);
            Assert.AreEqual("recover_assigned", snapshot.Reason);
        }
    }
}
