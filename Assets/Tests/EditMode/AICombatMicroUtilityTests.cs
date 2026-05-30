using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AICombatMicroUtilityTests
    {
        [Test]
        public void EvaluateAmmoDiscipline_HoldsLowAmmoLowQualityShot()
        {
            AIAmmoDisciplineDecision decision =
                AICombatMicroUtility.EvaluateAmmoDiscipline(
                    availableAmmo: 1,
                    maxAmmo: 3,
                    currentAmmo: 1.10f,
                    shotQuality: 0.45f,
                    targetHealthRatio: 0.9f,
                    targetControlled: false,
                    enemyCountInLane: 1,
                    allyCountInLane: 0,
                    isAreaDenial: false);

            Assert.IsTrue(decision.ShouldHoldFire);
            Assert.AreEqual("low_ammo_hold", decision.Reason);
        }

        [Test]
        public void EvaluateAmmoDiscipline_CommitsFinisherDespiteLowAmmo()
        {
            AIAmmoDisciplineDecision decision =
                AICombatMicroUtility.EvaluateAmmoDiscipline(
                    availableAmmo: 1,
                    maxAmmo: 3,
                    currentAmmo: 1.10f,
                    shotQuality: 0.35f,
                    targetHealthRatio: 0.20f,
                    targetControlled: false,
                    enemyCountInLane: 1,
                    allyCountInLane: 0,
                    isAreaDenial: false);

            Assert.IsFalse(decision.ShouldHoldFire);
            Assert.AreEqual("finisher_window", decision.Reason);
        }

        [Test]
        public void EvaluateAmmoDiscipline_HoldsNearReloadReserveForRhythm()
        {
            AIAmmoDisciplineDecision decision =
                AICombatMicroUtility.EvaluateAmmoDiscipline(
                    availableAmmo: 1,
                    maxAmmo: 3,
                    currentAmmo: 1.82f,
                    shotQuality: 0.70f,
                    targetHealthRatio: 0.75f,
                    targetControlled: false,
                    enemyCountInLane: 1,
                    allyCountInLane: 0,
                    isAreaDenial: false);

            Assert.IsTrue(decision.ShouldHoldFire);
            Assert.AreEqual("reload_rhythm", decision.Reason);
        }

        [Test]
        public void ResolveMovementStyle_UsesReloadBaitWhenAmmoDry()
        {
            AICombatMicroMovementDecision decision =
                AICombatMicroUtility.ResolveMovementStyle(
                    availableAmmo: 0,
                    maxAmmo: 3,
                    currentAmmo: 0.20f,
                    targetDistance: 6f,
                    preferredRange: 6f,
                    tooCloseDistance: 3f,
                    isArtillery: false,
                    hasDanger: false,
                    currentTick: 1u,
                    entityId: 7);

            Assert.AreEqual(AICombatMicroMoveStyle.ReloadBait, decision.Style);
            Assert.AreEqual("reload_bait", decision.Reason);
        }

        [Test]
        public void ResolveMovementStyle_PrefersThrowerSpacingForArtilleryCloseRange()
        {
            AICombatMicroMovementDecision decision =
                AICombatMicroUtility.ResolveMovementStyle(
                    availableAmmo: 2,
                    maxAmmo: 3,
                    currentAmmo: 2.20f,
                    targetDistance: 6f,
                    preferredRange: 6f,
                    tooCloseDistance: 3f,
                    isArtillery: true,
                    hasDanger: false,
                    currentTick: 20u,
                    entityId: 3);

            Assert.AreEqual(AICombatMicroMoveStyle.ThrowerSpacing, decision.Style);
            Assert.AreEqual("thrower_spacing", decision.Reason);
        }

        [Test]
        public void ResolveMovementStyle_UsesDeterministicDodgeFeintWindow()
        {
            AICombatMicroMovementDecision decision =
                AICombatMicroUtility.ResolveMovementStyle(
                    availableAmmo: 2,
                    maxAmmo: 3,
                    currentAmmo: 2.20f,
                    targetDistance: 5.5f,
                    preferredRange: 6f,
                    tooCloseDistance: 3f,
                    isArtillery: false,
                    hasDanger: false,
                    currentTick: 55u,
                    entityId: 1);

            Assert.AreEqual(AICombatMicroMoveStyle.DodgeFeint, decision.Style);
            Assert.AreEqual("dodge_feint", decision.Reason);
        }

        [Test]
        public void ResolveMovementStyle_UsesPeekTimingAtGoodRangeWithAmmo()
        {
            AICombatMicroMovementDecision decision =
                AICombatMicroUtility.ResolveMovementStyle(
                    availableAmmo: 2,
                    maxAmmo: 3,
                    currentAmmo: 2.20f,
                    targetDistance: 5.5f,
                    preferredRange: 6f,
                    tooCloseDistance: 3f,
                    isArtillery: false,
                    hasDanger: false,
                    currentTick: 20u,
                    entityId: 1);

            Assert.AreEqual(AICombatMicroMoveStyle.PeekTiming, decision.Style);
            Assert.AreEqual("peek_timing", decision.Reason);
        }
    }
}
