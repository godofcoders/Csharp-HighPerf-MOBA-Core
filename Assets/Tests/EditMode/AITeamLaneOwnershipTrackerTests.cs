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
            tracker.ReportLane(9, AITeamLaneAssignment.Left, Vector3.zero, 100u);
            tracker.ReportLane(10, AITeamLaneAssignment.Left, Vector3.zero, 100u);
            tracker.ReportLane(11, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot pending = tracker.GetSnapshot(10, 101u, 30u);
            AITeamLaneOwnershipSnapshot confirmed = tracker.GetSnapshot(10, 115u, 30u);

            Assert.IsFalse(pending.ShouldRotate);
            Assert.IsTrue(pending.RotationPending);
            Assert.AreEqual(AITeamLaneAssignment.Left, pending.RecommendedLane);
            Assert.AreEqual("rebalance_pending", pending.Reason);

            Assert.IsTrue(confirmed.ShouldRotate);
            Assert.IsFalse(confirmed.RotationPending);
            Assert.AreEqual(AITeamLaneAssignment.Right, confirmed.RecommendedLane);
            Assert.AreEqual("rebalance_underowned", confirmed.Reason);
            Assert.GreaterOrEqual((int)confirmed.RotationAgeTicks, 14);
        }

        [Test]
        public void GetSnapshot_SuppressesRepeatRotation_AfterRecentCommit()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(9, AITeamLaneAssignment.Left, Vector3.zero, 100u);
            tracker.ReportLane(10, AITeamLaneAssignment.Left, Vector3.zero, 100u);
            tracker.ReportLane(11, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot confirmed = tracker.GetSnapshot(10, 115u, 30u);
            AITeamLaneAssignment committedLane = confirmed.RecommendedLane;
            Assert.IsTrue(confirmed.ShouldRotate);

            tracker.ReportLane(10, committedLane, Vector3.zero, 116u);
            tracker.ReportLane(11, AITeamLaneAssignment.Mid, Vector3.zero, 116u);
            tracker.ReportLane(7, committedLane, Vector3.zero, 116u);

            AITeamLaneOwnershipSnapshot cooldown = tracker.GetSnapshot(10, 117u, 30u);

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

        [Test]
        public void GetSnapshot_PreservesPairedMidControl_WhenSoftlyOverOwned()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(10, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(13, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot snapshot = tracker.GetSnapshot(13, 101u, 30u);

            Assert.AreEqual(AITeamLaneAssignment.Mid, snapshot.RecommendedLane);
            Assert.IsTrue(snapshot.CurrentLaneOverOwned);
            Assert.IsFalse(snapshot.ShouldRotate);
            Assert.IsFalse(snapshot.RotationPending);
            Assert.AreEqual("mid_control_hold", snapshot.Reason);
        }

        [Test]
        public void GetSnapshot_PreservesAssignedLanePair_WhenSoftlyOverOwned()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(12, AITeamLaneAssignment.Left, Vector3.zero, 100u);
            tracker.ReportLane(15, AITeamLaneAssignment.Left, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot snapshot = tracker.GetSnapshot(15, 101u, 30u);

            Assert.AreEqual(AITeamLaneAssignment.Left, snapshot.RecommendedLane);
            Assert.IsTrue(snapshot.CurrentLaneOverOwned);
            Assert.IsFalse(snapshot.ShouldRotate);
            Assert.IsFalse(snapshot.RotationPending);
            Assert.AreEqual("assigned_lane_hold", snapshot.Reason);
        }

        [Test]
        public void GetSnapshot_RequiresExtraConfirmation_WhenRotatingFromMidControl()
        {
            AITeamLaneOwnershipTracker tracker = new AITeamLaneOwnershipTracker();
            tracker.ReportLane(10, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(13, AITeamLaneAssignment.Mid, Vector3.zero, 100u);
            tracker.ReportLane(16, AITeamLaneAssignment.Mid, Vector3.zero, 100u);

            AITeamLaneOwnershipSnapshot initial = tracker.GetSnapshot(13, 101u, 30u);
            AITeamLaneOwnershipSnapshot pending = tracker.GetSnapshot(13, 115u, 30u);
            AITeamLaneOwnershipSnapshot confirmed = tracker.GetSnapshot(13, 123u, 30u);

            Assert.IsFalse(initial.ShouldRotate);
            Assert.IsTrue(initial.RotationPending);
            Assert.IsFalse(pending.ShouldRotate);
            Assert.IsTrue(pending.RotationPending);
            Assert.AreEqual("rebalance_pending", pending.Reason);

            Assert.IsTrue(confirmed.ShouldRotate);
            Assert.IsFalse(confirmed.RotationPending);
            Assert.AreNotEqual(AITeamLaneAssignment.Mid, confirmed.RecommendedLane);
            Assert.GreaterOrEqual((int)confirmed.RotationAgeTicks, 22);
        }
    }
}
