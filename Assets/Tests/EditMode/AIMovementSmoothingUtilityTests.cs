using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIMovementSmoothingUtilityTests
    {
        [Test]
        public void SmoothDirection_DampensHardDirectionFlip()
        {
            var previous = new AIMovementSmoothingState(true, Vector3.right);

            AIMovementSmoothingResult result =
                AIMovementSmoothingUtility.SmoothDirection(
                    Vector3.left,
                    previous,
                    baseBlend: 0.55f,
                    highPriority: false,
                    avoidanceLocked: false);

            Assert.AreEqual("smooth_flip", result.Reason);
            Assert.Greater(Vector3.Dot(result.Direction.normalized, Vector3.right), -0.75f);
        }

        [Test]
        public void SmoothDirection_HighPriorityRespondsQuickly()
        {
            var previous = new AIMovementSmoothingState(true, Vector3.right);

            AIMovementSmoothingResult result =
                AIMovementSmoothingUtility.SmoothDirection(
                    Vector3.left,
                    previous,
                    baseBlend: 0.35f,
                    highPriority: true,
                    avoidanceLocked: false);

            Assert.Less(Vector3.Dot(result.Direction.normalized, Vector3.right), -0.35f);
        }

        [Test]
        public void SmoothDirection_ZeroInputClearsState()
        {
            var previous = new AIMovementSmoothingState(true, Vector3.forward);

            AIMovementSmoothingResult result =
                AIMovementSmoothingUtility.SmoothDirection(
                    Vector3.zero,
                    previous,
                    baseBlend: 0.55f,
                    highPriority: false,
                    avoidanceLocked: false);

            Assert.AreEqual(Vector3.zero, result.Direction);
            Assert.IsFalse(result.State.HasDirection);
            Assert.AreEqual("smooth_idle", result.Reason);
        }
    }
}
