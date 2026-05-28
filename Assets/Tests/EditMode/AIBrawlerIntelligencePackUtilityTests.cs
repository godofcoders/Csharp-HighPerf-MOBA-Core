using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIBrawlerIntelligencePackUtilityTests
    {
        [Test]
        public void EvaluateDashGadget_EscapesWhenLowHealthAndThreatClose()
        {
            AIBrawlerPackDecision decision =
                AIBrawlerIntelligencePackUtility.EvaluateDashGadget(
                    selfHealthRatio: 0.28f,
                    enemyDistance: 1.5f,
                    dangerDistance: 5f,
                    targetHealthRatio: 0.9f,
                    targetControlled: false,
                    hasEscapeIntent: true);

            Assert.IsTrue(decision.ShouldUse);
            Assert.IsTrue(decision.ForceUse);
            Assert.AreEqual("dash_escape", decision.Reason);
        }

        [Test]
        public void EvaluateDashGadget_ChasesControlledFinisherWhenSafeEnough()
        {
            AIBrawlerPackDecision decision =
                AIBrawlerIntelligencePackUtility.EvaluateDashGadget(
                    selfHealthRatio: 0.85f,
                    enemyDistance: 5.5f,
                    dangerDistance: 4f,
                    targetHealthRatio: 0.18f,
                    targetControlled: true,
                    hasEscapeIntent: false);

            Assert.IsTrue(decision.ShouldUse);
            Assert.IsTrue(decision.ForceUse);
            Assert.AreEqual("dash_finisher", decision.Reason);
        }

        [Test]
        public void EvaluateAmmoRefillGadget_UsesOnlyForLowAmmoPressure()
        {
            AIBrawlerPackDecision fullAmmo =
                AIBrawlerIntelligencePackUtility.EvaluateAmmoRefillGadget(
                    availableAmmo: 3,
                    maxAmmo: 3,
                    targetDistance: 5f,
                    attackRange: 10f,
                    targetHealthRatio: 0.2f,
                    targetControlled: true,
                    enemiesInLane: 2);

            AIBrawlerPackDecision emptyAmmo =
                AIBrawlerIntelligencePackUtility.EvaluateAmmoRefillGadget(
                    availableAmmo: 0,
                    maxAmmo: 3,
                    targetDistance: 5f,
                    attackRange: 10f,
                    targetHealthRatio: 0.2f,
                    targetControlled: true,
                    enemiesInLane: 2);

            Assert.IsFalse(fullAmmo.ShouldUse);
            Assert.IsTrue(emptyAmmo.ShouldUse);
            Assert.Greater(emptyAmmo.Score, fullAmmo.Score);
        }

        [Test]
        public void EvaluateSuperChargeGadget_UsesWhenItCompletesSuperUnderPressure()
        {
            AIBrawlerPackDecision decision =
                AIBrawlerIntelligencePackUtility.EvaluateSuperChargeGadget(
                    chargePercent: 0.78f,
                    chargeFraction: 0.25f,
                    superReady: false,
                    targetDistance: 4f,
                    superRange: 8f,
                    nearbyEnemyCount: 1,
                    targetControlled: false);

            Assert.IsTrue(decision.ShouldUse);
            Assert.IsTrue(decision.ForceUse);
            Assert.AreEqual("super_charge_combo", decision.Reason);
        }

        [Test]
        public void EvaluateAreaDenialCommit_CommitsForControlledLingeringCluster()
        {
            AIBrawlerPackDecision decision =
                AIBrawlerIntelligencePackUtility.EvaluateAreaDenialCommit(
                    enemyPressureCount: 3,
                    allyRiskCount: 0,
                    targetHealthRatio: 0.55f,
                    targetControlled: true,
                    hasLingeringHazard: true,
                    isSuper: true);

            Assert.IsTrue(decision.ShouldUse);
            Assert.IsTrue(decision.ForceUse);
            Assert.AreEqual("area_denial", decision.Reason);
        }

        [Test]
        public void ScoreChainBounceAnchor_PrefersControlledLowHealthClusters()
        {
            float weakSingle =
                AIBrawlerIntelligencePackUtility.ScoreChainBounceAnchor(
                    bounceTargets: 1,
                    requestedTargetBonus: true,
                    targetHealthRatio: 0.9f,
                    targetControlled: false);

            float strongCluster =
                AIBrawlerIntelligencePackUtility.ScoreChainBounceAnchor(
                    bounceTargets: 3,
                    requestedTargetBonus: false,
                    targetHealthRatio: 0.2f,
                    targetControlled: true);

            Assert.Greater(strongCluster, weakSingle);
        }
    }
}
