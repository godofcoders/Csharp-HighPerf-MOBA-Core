using NUnit.Framework;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    // Unit tests for ResourceStorage — the ammo/charge bucket.
    //
    // Contract: Consume() is atomic (all-or-nothing), Tick() refills
    // continuously but AvailableBars reports only whole integer bars.
    // This is the "chunky reload" feel from Brawl Stars: you can see
    // the progress bar filling but you can't fire until a full bar
    // is ready.
    public class ResourceStorageTests
    {
        [Test]
        public void Construction_StartsAtMaxAmmo()
        {
            ResourceStorage ammo = new ResourceStorage(max: 3, reloadSpeed: 1f);

            Assert.AreEqual(3, ammo.MaxAmmo);
            Assert.AreEqual(3f, ammo.CurrentAmmo);
            Assert.AreEqual(1f, ammo.ReloadSpeed);
        }

        [Test]
        public void Consume_Succeeds_WhenEnoughAvailable()
        {
            ResourceStorage ammo = new ResourceStorage(3, 1f);

            bool consumed = ammo.Consume(1);

            Assert.IsTrue(consumed);
            Assert.AreEqual(2f, ammo.CurrentAmmo);
        }

        [Test]
        public void Consume_FailsAtomically_WhenInsufficient()
        {
            // "Atomic" means: if we don't have enough, nothing is deducted.
            // Prevents half-spent-shot bugs.
            ResourceStorage ammo = new ResourceStorage(3, 1f);
            ammo.Consume(3);

            bool consumed = ammo.Consume(1);

            Assert.IsFalse(consumed);
            Assert.AreEqual(0f, ammo.CurrentAmmo);
        }

        [Test]
        public void Tick_RefillsOverTime()
        {
            ResourceStorage ammo = new ResourceStorage(3, reloadSpeed: 1f);
            ammo.Consume(2);

            ammo.Tick(0.5f);

            Assert.AreEqual(1.5f, ammo.CurrentAmmo, 0.0001f);
        }

        [Test]
        public void SetReloadSpeed_ChangesSubsequentRefillRate()
        {
            ResourceStorage ammo = new ResourceStorage(3, reloadSpeed: 1f);
            ammo.Consume(3);

            ammo.SetReloadSpeed(0.25f);
            ammo.Tick(2f);

            Assert.AreEqual(0.5f, ammo.CurrentAmmo, 0.0001f);
        }

        [Test]
        public void CombatBalance_LongRangeAttacksReloadMoreSlowly()
        {
            float shortDuration = BrawlerCombatBalance.ResolveAmmoReloadDuration(4f);
            float mediumDuration = BrawlerCombatBalance.ResolveAmmoReloadDuration(12f);
            float longDuration = BrawlerCombatBalance.ResolveAmmoReloadDuration(15f);

            Assert.AreEqual(2f, shortDuration, 0.0001f);
            Assert.AreEqual(2.625f, mediumDuration, 0.0001f);
            Assert.AreEqual(3.25f, longDuration, 0.0001f);
            Assert.That(
                BrawlerCombatBalance.ResolveAmmoReloadSpeed(15f),
                Is.LessThan(BrawlerCombatBalance.ResolveAmmoReloadSpeed(4f)));
        }

        [Test]
        public void BrawlerState_UsesCurrentMainAttackRangeForAmmoReload()
        {
            BrawlerDefinition brawler = ScriptableObject.CreateInstance<BrawlerDefinition>();
            ProjectileAbilityDefinition attack =
                ScriptableObject.CreateInstance<ProjectileAbilityDefinition>();

            try
            {
                brawler.BaseHealth = 1000f;
                brawler.BaseDamage = 100f;
                brawler.BaseMoveSpeed = 5f;
                brawler.MainAttack = attack;
                brawler.ProgressionBonuses = null;
                brawler.SuperChargeSources = null;
                attack.Range = 15f;

                BrawlerState state = new BrawlerState(brawler, TeamType.Blue);
                state.Ammo.Consume(3);
                state.UpdateResources(3.25f);

                Assert.AreEqual(1f, state.Ammo.CurrentAmmo, 0.0001f);
                Assert.AreEqual(
                    BrawlerCombatBalance.ResolveAmmoReloadSpeed(15f),
                    state.Ammo.ReloadSpeed,
                    0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(attack);
                Object.DestroyImmediate(brawler);
            }
        }

        [Test]
        public void Tick_ClampsAtMax()
        {
            ResourceStorage ammo = new ResourceStorage(3, 10f);

            ammo.Tick(1f);

            Assert.AreEqual(3f, ammo.CurrentAmmo);
        }

        [Test]
        public void Refill_JumpsToMax_EvenMidReload()
        {
            // Gadget effect "instantly refill ammo" should skip the slow
            // reload regardless of current progress.
            ResourceStorage ammo = new ResourceStorage(3, 1f);
            ammo.Consume(3);
            ammo.Tick(0.3f);

            ammo.Refill();

            Assert.AreEqual(3f, ammo.CurrentAmmo);
        }

        [Test]
        public void AvailableBars_IsFloorOfCurrentAmmo()
        {
            // 1.7 ammo means ONE bar is usable; the 0.7 of the second
            // bar is progress, not ammo.
            ResourceStorage ammo = new ResourceStorage(3, 1f);
            ammo.Consume(3);
            ammo.Tick(1.7f);

            Assert.AreEqual(1, ammo.AvailableBars);
        }
    }
}
