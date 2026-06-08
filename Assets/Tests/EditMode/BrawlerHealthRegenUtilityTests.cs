using MOBA.Core.Simulation;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class BrawlerHealthRegenUtilityTests
    {
        [Test]
        public void CanRegenerate_WaitsForDamageDelay()
        {
            Assert.IsFalse(BrawlerHealthRegenUtility.CanRegenerate(
                currentTick: 119,
                lastDamageTick: 30,
                lastAttackTick: 0,
                delayTicks: 90,
                currentHealth: 500f,
                maxHealth: 1000f,
                isDead: false));

            Assert.IsTrue(BrawlerHealthRegenUtility.CanRegenerate(
                currentTick: 120,
                lastDamageTick: 30,
                lastAttackTick: 0,
                delayTicks: 90,
                currentHealth: 500f,
                maxHealth: 1000f,
                isDead: false));
        }

        [Test]
        public void CanRegenerate_AlsoWaitsForRecentAttack()
        {
            Assert.IsFalse(BrawlerHealthRegenUtility.CanRegenerate(
                currentTick: 100,
                lastDamageTick: 0,
                lastAttackTick: 80,
                delayTicks: 90,
                currentHealth: 500f,
                maxHealth: 1000f,
                isDead: false));
        }

        [Test]
        public void CanRegenerate_RejectsDeadOrFullHealthBrawlers()
        {
            Assert.IsFalse(BrawlerHealthRegenUtility.CanRegenerate(
                currentTick: 200,
                lastDamageTick: 0,
                lastAttackTick: 0,
                delayTicks: 90,
                currentHealth: 0f,
                maxHealth: 1000f,
                isDead: true));

            Assert.IsFalse(BrawlerHealthRegenUtility.CanRegenerate(
                currentTick: 200,
                lastDamageTick: 0,
                lastAttackTick: 0,
                delayTicks: 90,
                currentHealth: 1000f,
                maxHealth: 1000f,
                isDead: false));
        }

        [Test]
        public void CalculateHealAmount_UsesMaxHealthPerSecond()
        {
            float heal = BrawlerHealthRegenUtility.CalculateHealAmount(
                maxHealth: 3000f,
                deltaTime: 0.5f,
                maxHealthPerSecond: 0.10f);

            Assert.AreEqual(150f, heal);
        }
    }
}
