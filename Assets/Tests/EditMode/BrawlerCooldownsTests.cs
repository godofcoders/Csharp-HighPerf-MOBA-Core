using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerCooldowns — the per-ability ready/locked timer
    // store for MainAttack, Super, and Gadget.
    //
    // The crown-jewel test in this suite is StartCooldown_OverwritesPreviousCooldown.
    // It looks like a duplicate-call edge case but it actually pins the
    // most subtle design choice in this class: AbilityCooldownState is a
    // mutable struct exposed as a public *field*, not a property. If anyone
    // "cleans that up" by switching to `{ get; private set; }`, then
    // StartCooldown(...) would mutate a temporary copy and the original field
    // would silently never change. That regression is invisible in code
    // review — but the second StartCooldown call below would fail to update
    // ReadyAtTick, and this test would go red. That's the trip-wire.
    //
    // The second-most-important pin is StartCooldown_ConvertsFractionalSeconds_ToTicks.
    // We use SimulationClock.TickDeltaTime (a const = 1f/30f) so 1 second ==
    // 30 ticks. If anyone reintroduces the old hardcoded `* 30f` magic
    // number in another place, or changes the const, this suite spots the
    // mismatch immediately.
    public class BrawlerCooldownsTests
    {
        // ---------- Construction ----------

        [Test]
        public void Construction_AllSlotsReady_AtTickZero()
        {
            // Arrange
            var cooldowns = new BrawlerCooldowns();

            // Act + Assert — fresh instance: all timers default to ReadyAtTick=0.
            // 0 >= 0 is true, so every slot reports ready immediately.
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 0));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.Super, currentTick: 0));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.Gadget, currentTick: 0));
        }

        // ---------- IsReady ----------

        [Test]
        public void IsReady_ReturnsFalse_WhenCurrentTickIsBeforeReadyAtTick()
        {
            // Arrange — start a 1s cooldown at tick 100. Ready at tick 130.
            var cooldowns = new BrawlerCooldowns();
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 100, cooldownSeconds: 1f);

            // Act
            bool ready = cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 129);

            // Assert
            Assert.IsFalse(ready);
        }

        [Test]
        public void IsReady_ReturnsTrue_WhenCurrentTickReachesReadyAtTick()
        {
            // Arrange
            var cooldowns = new BrawlerCooldowns();
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 100, cooldownSeconds: 1f);

            // Act — exact-boundary tick: ReadyAtTick = 130, currentTick = 130.
            bool ready = cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 130);

            // Assert — comparison is `currentTick >= ReadyAtTick`, so the
            // boundary tick itself counts as ready. If anyone turns this into
            // a strict `>` later, the off-by-one would let abilities fire one
            // tick late forever — this test catches it.
            Assert.IsTrue(ready);
        }

        [Test]
        public void IsReady_RoutesToCorrectSlot_AndDoesNotLeakToOthers()
        {
            // Arrange — only Super is on cooldown.
            var cooldowns = new BrawlerCooldowns();
            cooldowns.StartCooldown(AbilityRuntimeSlot.Super, currentTick: 0, cooldownSeconds: 5f);

            // Act + Assert — MainAttack and Gadget should still be ready.
            Assert.IsFalse(cooldowns.IsReady(AbilityRuntimeSlot.Super, currentTick: 10));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 10));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.Gadget, currentTick: 10));
        }

        [Test]
        public void IsReady_ReturnsFalse_ForUnknownSlot()
        {
            // Arrange — cast an out-of-range integer to AbilityRuntimeSlot.
            // This guards the `default:` branch of the switch in IsReady,
            // which returns false (a fail-closed default — safer than
            // returning true and accidentally letting an undefined ability
            // fire).
            var cooldowns = new BrawlerCooldowns();
            var unknownSlot = (AbilityRuntimeSlot)999;

            // Act
            bool ready = cooldowns.IsReady(unknownSlot, currentTick: 0);

            // Assert
            Assert.IsFalse(ready);
        }

        // ---------- StartCooldown ----------

        [Test]
        public void StartCooldown_MainAttack_SetsReadyAtTickBasedOnTPS()
        {
            // Arrange — 1 second cooldown at the default 30 TPS == 30 ticks.
            var cooldowns = new BrawlerCooldowns();

            // Act
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 0, cooldownSeconds: 1f);

            // Assert — read the underlying struct directly. Tick 29 is one
            // tick before ready; tick 30 is exactly ready.
            Assert.AreEqual(30u, cooldowns.MainAttack.ReadyAtTick);
            Assert.IsFalse(cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 29));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 30));
        }

        [Test]
        public void StartCooldown_Super_OnlyAffectsSuperSlot()
        {
            // Arrange
            var cooldowns = new BrawlerCooldowns();

            // Act
            cooldowns.StartCooldown(AbilityRuntimeSlot.Super, currentTick: 0, cooldownSeconds: 2f);

            // Assert — Super advanced, others untouched at their default 0.
            Assert.AreEqual(60u, cooldowns.Super.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.MainAttack.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.Gadget.ReadyAtTick);
        }

        [Test]
        public void StartCooldown_Gadget_OnlyAffectsGadgetSlot()
        {
            // Arrange
            var cooldowns = new BrawlerCooldowns();

            // Act
            cooldowns.StartCooldown(AbilityRuntimeSlot.Gadget, currentTick: 0, cooldownSeconds: 3f);

            // Assert
            Assert.AreEqual(90u, cooldowns.Gadget.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.MainAttack.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.Super.ReadyAtTick);
        }

        [Test]
        public void StartCooldown_ConvertsFractionalSeconds_ToTicks()
        {
            // Arrange — half a second @ 30 TPS == 15 ticks.
            // (Truncates toward zero via the (uint) cast — pinned behaviour.)
            var cooldowns = new BrawlerCooldowns();

            // Act
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 0, cooldownSeconds: 0.5f);

            // Assert
            Assert.AreEqual(15u, cooldowns.MainAttack.ReadyAtTick);
        }

        [Test]
        public void StartCooldown_ZeroSeconds_LeavesSlotReadyImmediately()
        {
            // Arrange — at currentTick=100, a 0s cooldown should leave the
            // ability immediately ready (ReadyAtTick = 100, IsReady(100) = true).
            var cooldowns = new BrawlerCooldowns();

            // Act
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 100, cooldownSeconds: 0f);

            // Assert
            Assert.AreEqual(100u, cooldowns.MainAttack.ReadyAtTick);
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 100));
        }

        [Test]
        public void StartCooldown_OverwritesPreviousCooldown_ProvingFieldIsMutatedNotCopied()
        {
            // This is the regression-canary test for the struct-field-vs-property
            // design noted in BrawlerCooldowns.cs. If the cooldown fields are
            // ever switched to auto-properties, this test fails because
            // StartCooldown would be mutating a copy returned by the getter
            // instead of the real backing field — the second call below
            // would not update ReadyAtTick.

            // Arrange — first cooldown writes ReadyAtTick = 90 (3s @ 30 TPS).
            var cooldowns = new BrawlerCooldowns();
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 0, cooldownSeconds: 3f);
            Assert.AreEqual(90u, cooldowns.MainAttack.ReadyAtTick, "Sanity: first call should have set ReadyAtTick to 90.");

            // Act — second call at the same tick with a SHORTER cooldown.
            // If StartCooldown mutates a copy, ReadyAtTick stays at 90.
            // If it mutates the real field (correct behaviour), it becomes 30.
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 0, cooldownSeconds: 1f);

            // Assert
            Assert.AreEqual(30u, cooldowns.MainAttack.ReadyAtTick);
        }

        // ---------- ResetAll ----------

        [Test]
        public void ResetAll_ClearsEveryCooldownTimer()
        {
            // Arrange — put all three slots on cooldown.
            var cooldowns = new BrawlerCooldowns();
            cooldowns.StartCooldown(AbilityRuntimeSlot.MainAttack, currentTick: 0, cooldownSeconds: 1f);
            cooldowns.StartCooldown(AbilityRuntimeSlot.Super, currentTick: 0, cooldownSeconds: 5f);
            cooldowns.StartCooldown(AbilityRuntimeSlot.Gadget, currentTick: 0, cooldownSeconds: 8f);

            // Act
            cooldowns.ResetAll();

            // Assert — all back to ReadyAtTick = 0, all immediately ready.
            Assert.AreEqual(0u, cooldowns.MainAttack.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.Super.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.Gadget.ReadyAtTick);
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.MainAttack, currentTick: 0));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.Super, currentTick: 0));
            Assert.IsTrue(cooldowns.IsReady(AbilityRuntimeSlot.Gadget, currentTick: 0));
        }

        [Test]
        public void ResetAll_OnFreshInstance_IsNoOp()
        {
            // Arrange — never started any cooldown, everything already at zero.
            var cooldowns = new BrawlerCooldowns();

            // Act — should not throw and should leave state ready.
            cooldowns.ResetAll();

            // Assert
            Assert.AreEqual(0u, cooldowns.MainAttack.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.Super.ReadyAtTick);
            Assert.AreEqual(0u, cooldowns.Gadget.ReadyAtTick);
        }
    }
}
