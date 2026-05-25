using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIAbilitySynergyUtilityTests
    {
        [Test]
        public void EvaluateComboWindow_CommitsForFreshControlledFinisher()
        {
            AIComboWindowResult result =
                AIAbilitySynergyUtility.EvaluateComboWindow(
                    hasSetup: true,
                    currentTick: 110u,
                    setupTick: 100u,
                    windowTicks: 36u,
                    targetHealthRatio: 0.30f,
                    targetControlled: true,
                    enemyPressureCount: 1,
                    allyRiskCount: 0);

            Assert.IsTrue(result.IsActive);
            Assert.IsTrue(result.ShouldCommit);
            Assert.Greater(result.Score, 58f);
        }

        [Test]
        public void EvaluateComboWindow_ExpiresOutsideWindow()
        {
            AIComboWindowResult result =
                AIAbilitySynergyUtility.EvaluateComboWindow(
                    hasSetup: true,
                    currentTick: 160u,
                    setupTick: 100u,
                    windowTicks: 36u,
                    targetHealthRatio: 0.30f,
                    targetControlled: true,
                    enemyPressureCount: 2,
                    allyRiskCount: 0);

            Assert.IsFalse(result.IsActive);
            Assert.IsFalse(result.ShouldCommit);
            Assert.AreEqual("expired", result.Reason);
        }

        [Test]
        public void ResolveLayeredAreaDenialPoint_LeadsMovingTargetsAndStaysInRange()
        {
            Vector3 point = AIAbilitySynergyUtility.ResolveLayeredAreaDenialPoint(
                selfPosition: Vector3.zero,
                targetPosition: new Vector3(5f, 0f, 0f),
                targetVelocity: new Vector3(0f, 0f, 4f),
                impactRadius: 2f,
                maxRange: 7f,
                enemyClusterCount: 2,
                layerIndex: 0);

            Assert.Greater(point.z, 0f);
            Assert.LessOrEqual(point.magnitude, 7.01f);
        }

        [Test]
        public void ScoreDeployableProtection_RisesForWoundedThreatenedDeployable()
        {
            float safeScore = AIAbilitySynergyUtility.ScoreDeployableProtection(
                deployableHealthRatio: 0.9f,
                enemyDistanceToDeployable: 8f,
                protectionRadius: 5f,
                nearbyEnemyCount: 0);

            float urgentScore = AIAbilitySynergyUtility.ScoreDeployableProtection(
                deployableHealthRatio: 0.25f,
                enemyDistanceToDeployable: 1f,
                protectionRadius: 5f,
                nearbyEnemyCount: 2);

            Assert.Greater(urgentScore, safeScore);
            Assert.Greater(urgentScore, 0.6f);
        }
    }
}
