using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIPredictiveCombatUtilityTests
    {
        [Test]
        public void EvaluateProjectileShot_LeadsMovingTarget()
        {
            AIPredictiveShotResult result =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    shooterPosition: Vector3.zero,
                    targetPosition: new Vector3(6f, 0f, 0f),
                    targetVelocity: new Vector3(0f, 0f, 4f),
                    range: 12f,
                    projectileSpeed: 12f,
                    availableAmmo: 3,
                    targetControlled: false,
                    enemyCountInLane: 1,
                    allyCountInLane: 0);

            Assert.IsTrue(result.ShouldFire);
            Assert.Greater(result.AimPoint.z, 0f);
            Assert.AreEqual("predictive_window", result.Reason);
        }

        [Test]
        public void EvaluateProjectileShot_HoldsLowAmmoShot_WhenAngleIsPoor()
        {
            AIPredictiveShotResult result =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    shooterPosition: Vector3.zero,
                    targetPosition: new Vector3(11f, 0f, 0f),
                    targetVelocity: new Vector3(0f, 0f, 8f),
                    range: 12f,
                    projectileSpeed: 8f,
                    availableAmmo: 1,
                    targetControlled: false,
                    enemyCountInLane: 1,
                    allyCountInLane: 0);

            Assert.IsFalse(result.ShouldFire);
            Assert.AreEqual("hold_bad_angle", result.Reason);
        }

        [Test]
        public void EvaluateProjectileShot_FiresThroughStrongLane_WhenMultipleEnemiesLineUp()
        {
            AIPredictiveShotResult result =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    shooterPosition: Vector3.zero,
                    targetPosition: new Vector3(11f, 0f, 0f),
                    targetVelocity: new Vector3(0f, 0f, 8f),
                    range: 12f,
                    projectileSpeed: 8f,
                    availableAmmo: 1,
                    targetControlled: false,
                    enemyCountInLane: 2,
                    allyCountInLane: 0);

            Assert.IsTrue(result.ShouldFire);
            Assert.AreEqual("strong_fire_lane", result.Reason);
        }

        [Test]
        public void EvaluateProjectileShot_FiresAtControlledTarget()
        {
            AIPredictiveShotResult result =
                AIPredictiveCombatUtility.EvaluateProjectileShot(
                    shooterPosition: Vector3.zero,
                    targetPosition: new Vector3(11f, 0f, 0f),
                    targetVelocity: new Vector3(0f, 0f, 8f),
                    range: 12f,
                    projectileSpeed: 8f,
                    availableAmmo: 1,
                    targetControlled: true,
                    enemyCountInLane: 1,
                    allyCountInLane: 0);

            Assert.IsTrue(result.ShouldFire);
            Assert.AreEqual("controlled_target", result.Reason);
        }
    }
}
