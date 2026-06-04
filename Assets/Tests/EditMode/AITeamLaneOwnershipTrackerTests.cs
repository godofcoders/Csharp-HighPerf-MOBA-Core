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
        public void GetSnapshot_DelaysNonAnchorRotation_UntilPressurePersists()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(10, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(13, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot pending = tracker.GetSnapshot(13, 101u, 30u);
            AITeamLaneOwnershipSnapshot confirmed = tracker.GetSnapshot(13, 111u, 30u);

            Assert.IsFalse(pending.ShouldRotate);
            Assert.IsTrue(pending.RotationPending);
            Assert.AreEqual(AITeamLaneAssignment.Mid, pending.RecommendedLane);
            Assert.AreEqual("rebalance_pending", pending.Reason);

            Assert.IsTrue(confirmed.ShouldRotate);
            Assert.IsFalse(confirmed.RotationPending);
            Assert.AreNotEqual(AITeamLaneAssignment.Mid, confirmed.RecommendedLane);
            Assert.AreEqual("rebalance_underowned", confirmed.Reason);
            Assert.GreaterOrEqual((int)confirmed.RotationAgeTicks, 10);
        }

        [Test]
        public void GetSnapshot_SuppressesRepeatRotation_AfterRecentCommit()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(10, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(13, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot confirmed = tracker.GetSnapshot(13, 111u, 30u);
            AITeamLaneAssignment committedLane = confirmed.RecommendedLane;
            Assert.IsTrue(confirmed.ShouldRotate);

            tracker.ReportLane(13, committedLane, Vector3.zero, 112u);
            tracker.ReportLane(7, committedLane, Vector3.zero, 112u);

            AITeamLaneOwnershipSnapshot cooldown = tracker.GetSnapshot(13, 113u, 30u);

            Assert.IsFalse(cooldown.ShouldRotate);
            Assert.Greater((int)cooldown.RotationCooldownRemainingTicks, 0);
            Assert.AreEqual("rotation_cooldown", cooldown.Reason);
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
