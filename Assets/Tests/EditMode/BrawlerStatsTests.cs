using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerStats — the POCO that owns health, shield, and
    // modifier pipelines. No Unity dependencies, so every test runs in
    // EditMode in milliseconds.
    //
    // These tests lock down the damage/heal invariants that every other
    // combat system relies on. If ApplyDamage's contract (clamp to zero,
    // return true only on the transition) changes, half the game breaks —
    // this suite catches it.
    public class BrawlerStatsTests
    {
        private static BrawlerStats NewStatsWithMaxHp(float maxHp)
        {
            BrawlerStats stats = new BrawlerStats();
            stats.MaxHealth.SetBaseValue(maxHp);
            stats.ResetHealthToMax();
            return stats;
        }

        // ---------- ApplyDamage ----------

        [Test]
        public void ApplyDamage_ReducesHealth_WhenAlive()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);

            bool wasFatal = stats.ApplyDamage(300f);

            Assert.AreEqual(700f, stats.CurrentHealth);
            Assert.IsFalse(wasFatal);
            Assert.IsFalse(stats.IsDead);
        }

        [Test]
        public void ApplyDamage_ReturnsTrue_OnKillingBlow()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);

            bool wasFatal = stats.ApplyDamage(1000f);

            Assert.AreEqual(0f, stats.CurrentHealth);
            Assert.IsTrue(wasFatal);
            Assert.IsTrue(stats.IsDead);
        }

        [Test]
        public void ApplyDamage_ClampsHealthToZero_OnOverkill()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);

            stats.ApplyDamage(5000f);

            Assert.AreEqual(0f, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_ReturnsFalse_WhenAlreadyDead()
        {
            // "No double-kill" invariant: once dead, subsequent damage does
            // not re-fire the fatal-transition signal. Protects death-event
            // subscribers from multi-fire if several damage events resolve
            // in the same tick.
            BrawlerStats stats = NewStatsWithMaxHp(1000f);
            stats.ApplyDamage(1000f);

            bool secondWasFatal = stats.ApplyDamage(100f);

            Assert.IsFalse(secondWasFatal);
            Assert.AreEqual(0f, stats.CurrentHealth);
        }

        // ---------- ApplyHeal ----------

        [Test]
        public void ApplyHeal_IncreasesHealth_WhenAlive()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);
            stats.ApplyDamage(400f);

            stats.ApplyHeal(200f);

            Assert.AreEqual(800f, stats.CurrentHealth);
        }

        [Test]
        public void ApplyHeal_ClampsAtMaxHealth()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);
            stats.ApplyDamage(100f);

            stats.ApplyHeal(500f);

            Assert.AreEqual(1000f, stats.CurrentHealth);
        }

        [Test]
        public void ApplyHeal_IsNoOp_WhenDead()
        {
            // Heals don't revive. Lifecycle concern that belongs elsewhere
            // (respawn flow, not incidental healing).
            BrawlerStats stats = NewStatsWithMaxHp(1000f);
            stats.ApplyDamage(1000f);

            stats.ApplyHeal(500f);

            Assert.AreEqual(0f, stats.CurrentHealth);
            Assert.IsTrue(stats.IsDead);
        }

        // ---------- SetCurrentHealth ----------

        [Test]
        public void SetCurrentHealth_ClampsToMaxHealth()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);

            stats.SetCurrentHealth(9999f);

            Assert.AreEqual(1000f, stats.CurrentHealth);
        }

        [Test]
        public void SetCurrentHealth_ClampsToZero_WhenNegative()
        {
            BrawlerStats stats = NewStatsWithMaxHp(1000f);

            stats.SetCurrentHealth(-500f);

            Assert.AreEqual(0f, stats.CurrentHealth);
        }

        // ---------- Shield ----------

        [Test]
        public void AddShield_Accumulates()
        {
            BrawlerStats stats = new BrawlerStats();

            stats.AddShield(100f);
            stats.AddShield(50f);

            Assert.AreEqual(150f, stats.ShieldHealth);
        }

        [Test]
        public void AddShield_IgnoresNonPositive()
        {
            BrawlerStats stats = new BrawlerStats();
            stats.AddShield(100f);

            stats.AddShield(-50f);
            stats.AddShield(0f);

            Assert.AreEqual(100f, stats.ShieldHealth);
        }

        [Test]
        public void ClearShield_ZeroesShieldHealth()
        {
            BrawlerStats stats = new BrawlerStats();
            stats.AddShield(100f);

            stats.ClearShield();

            Assert.AreEqual(0f, stats.ShieldHealth);
        }

        // ---------- Clear all modifiers ----------

        [Test]
        public void ClearAllModifiers_ClearsShield()
        {
            BrawlerStats stats = new BrawlerStats();
            stats.AddShield(200f);

            stats.ClearAllModifiers();

            Assert.AreEqual(0f, stats.ShieldHealth);
        }
    }
}
