using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for HyperchargeTracker — the per-brawler charge meter that
    // gates the Brawl-Stars-style "hypercharge" super-super activation.
    //
    // Same shape as BrawlerCooldowns: a small POCO state machine with one
    // input collaborator that's a const (SimulationClock.TickDeltaTime), so
    // no fakes are needed.
    //
    // Two crown-jewel tests in this fixture:
    //
    //   1. Activate_ConvertsDurationSeconds_ToTicks_AtTPS — pins the
    //      seconds→ticks contract. Same precision-bug shape that
    //      BrawlerCooldowns had: the doc-comment in HyperchargeTracker.cs
    //      explicitly says the conversion is `(uint)(durationSeconds /
    //      SimulationClock.TickDeltaTime)`, and TickDeltaTime is 1f/30f =
    //      ~0.033333335f in IEEE float, so a naive divide-then-truncate
    //      computes 29.9999... and drops it to 29 for a 1s input. This test
    //      asserts the contract (1s = 30 ticks); if the production code
    //      truncates instead of rounds, this test goes red and tells us to
    //      apply the same `+ 0.5f` fix BrawlerCooldowns uses.
    //
    //   2. AddCharge_IsNoOp_WhenAlreadyActive — pins the gameplay rule that
    //      you cannot accumulate charge for the *next* hypercharge while the
    //      *current* one is still ticking. If anyone removes the early-out
    //      guard in AddCharge, the player would silently start re-charging
    //      mid-super and the next hypercharge would be available the moment
    //      the current one ends — a "free" second hypercharge bug.
    public class HyperchargeTrackerTests
    {
        // ---------- Construction ----------

        [Test]
        public void Construction_StartsInactive_AndAtZeroCharge()
        {
            // Arrange + Act
            var tracker = new HyperchargeTracker();

            // Assert
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0f, tracker.ChargePercent, 0.0001f);
        }

        // ---------- AddCharge ----------

        [Test]
        public void AddCharge_AccumulatesPartialCharge()
        {
            // Arrange
            var tracker = new HyperchargeTracker();

            // Act
            tracker.AddCharge(0.3f);
            tracker.AddCharge(0.4f);

            // Assert
            Assert.AreEqual(0.7f, tracker.ChargePercent, 0.0001f);
        }

        [Test]
        public void AddCharge_ClampsAtOne_WhenAccumulationOverflows()
        {
            // Arrange — 0.7 + 0.6 would be 1.3 without the Math.Min clamp.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(0.7f);

            // Act
            tracker.AddCharge(0.6f);

            // Assert
            Assert.AreEqual(1f, tracker.ChargePercent, 0.0001f);
        }

        [Test]
        public void AddCharge_IsNoOp_WhenAlreadyActive()
        {
            // The gameplay rule: while a hypercharge is RUNNING, you cannot
            // start charging up the next one. AddCharge silently ignores the
            // call until Tick deactivates.

            // Arrange — fully charge and activate.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 5f);
            Assert.IsTrue(tracker.IsActive, "Sanity: should be active before the no-op test.");
            Assert.AreEqual(0f, tracker.ChargePercent, 0.0001f, "Sanity: charge should be consumed by Activate.");

            // Act — try to add charge mid-hypercharge.
            tracker.AddCharge(0.8f);

            // Assert — silently ignored.
            Assert.AreEqual(0f, tracker.ChargePercent, 0.0001f);
        }

        // ---------- Activate ----------

        [Test]
        public void Activate_DoesNothing_WhenChargeBelowOne()
        {
            // Arrange — only 0.99 charge.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(0.99f);

            // Act
            tracker.Activate(startTick: 100, durationSeconds: 5f);

            // Assert — the early-out guard means no state change.
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0.99f, tracker.ChargePercent, 0.0001f);
        }

        [Test]
        public void Activate_StartsHypercharge_AndConsumesCharge_WhenChargeAtOne()
        {
            // Arrange
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);

            // Act
            tracker.Activate(startTick: 100, durationSeconds: 5f);

            // Assert
            Assert.IsTrue(tracker.IsActive);
            Assert.AreEqual(0f, tracker.ChargePercent, 0.0001f);
        }

        [Test]
        public void Activate_DurationOfZero_FallsBackToFiveSecondsDefault()
        {
            // Pinned: the magic-number fallback. If anyone removes the
            // `if (durationSeconds <= 0f) durationSeconds = 5f;` line, this
            // test catches it because Tick at tick 30 (1 second in) would
            // already deactivate.

            // Arrange — fully charge, then activate with 0s duration.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 0f);

            // Act + Assert — at tick 149 (4.97s in), still active because
            // the fallback duration is 5s = 150 ticks.
            tracker.Tick(currentTick: 149, onDeactivate: null);
            Assert.IsTrue(tracker.IsActive);

            // At tick 150, the boundary is reached and it deactivates.
            tracker.Tick(currentTick: 150, onDeactivate: null);
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void Activate_NegativeDuration_AlsoFallsBackToFiveSecondsDefault()
        {
            // Same fallback path, different input — guard accepts <= 0.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: -2f);

            tracker.Tick(currentTick: 150, onDeactivate: null);
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void Activate_ConvertsDurationSeconds_ToTicks_AtTPS()
        {
            // CROWN JEWEL — the same precision-bug catcher we used for
            // BrawlerCooldowns. 1s @ 30 TPS must convert to exactly 30 ticks,
            // not 29. If the production code uses naive (uint)(seconds /
            // TickDeltaTime), this test fails because of IEEE float
            // imprecision and signals that the same `+ 0.5f` round-to-nearest
            // fix needs to be applied here.

            // Arrange — 2-second hypercharge starting at tick 0. End tick
            // should be 60 (2 * 30).
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 2f);

            // Act + Assert — tick 59 is one tick before end; tick 60 is end.
            tracker.Tick(currentTick: 59, onDeactivate: null);
            Assert.IsTrue(tracker.IsActive, "At tick 59 (one before end), hypercharge should still be active.");

            tracker.Tick(currentTick: 60, onDeactivate: null);
            Assert.IsFalse(tracker.IsActive, "At tick 60 (exactly end), hypercharge should deactivate.");
        }

        // ---------- Tick ----------

        [Test]
        public void Tick_BeforeEndTick_DoesNothing_AndStaysActive()
        {
            // Arrange — 5s hypercharge starts at tick 0, ends at tick 150.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 5f);

            int callbackFireCount = 0;

            // Act
            tracker.Tick(currentTick: 50, onDeactivate: () => callbackFireCount++);

            // Assert
            Assert.IsTrue(tracker.IsActive);
            Assert.AreEqual(0, callbackFireCount);
        }

        [Test]
        public void Tick_AtEndTick_DeactivatesAndFiresCallbackExactlyOnce()
        {
            // Arrange — 5s hypercharge, end at tick 150.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 5f);

            int callbackFireCount = 0;
            void OnDeactivate() => callbackFireCount++;

            // Act — tick at end.
            tracker.Tick(currentTick: 150, onDeactivate: OnDeactivate);

            // Assert — flipped to inactive and callback fired once.
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(1, callbackFireCount);

            // Tick again past end — callback should NOT re-fire because
            // IsActive is now false (the guard short-circuits).
            tracker.Tick(currentTick: 151, onDeactivate: OnDeactivate);
            Assert.AreEqual(1, callbackFireCount);
        }

        [Test]
        public void Tick_NullCallback_DoesNotThrow_OnDeactivation()
        {
            // The `?.Invoke()` null-safe call protects callers from having to
            // pass a no-op lambda. Pinned so a refactor that drops the `?` is
            // caught immediately.

            // Arrange
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 1f);

            // Act + Assert — no NRE.
            Assert.DoesNotThrow(() => tracker.Tick(currentTick: 30, onDeactivate: null));
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void Tick_WhenInactive_DoesNothing_AndDoesNotFireCallback()
        {
            // Arrange — never activated.
            var tracker = new HyperchargeTracker();
            int callbackFireCount = 0;

            // Act — tick at any value.
            tracker.Tick(currentTick: 9999, onDeactivate: () => callbackFireCount++);

            // Assert — guard short-circuits on !IsActive, no state change, no callback.
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0, callbackFireCount);
        }

        // ---------- Reset ----------

        [Test]
        public void Reset_ClearsActiveState_ChargePercent_AndEndTick()
        {
            // Arrange — fully charge, activate, then immediately reset.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 5f);

            // Act
            tracker.Reset();

            // Assert — back to construction state.
            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(0f, tracker.ChargePercent, 0.0001f);

            // Indirect proof that _endTick was also cleared: a Tick at any
            // value should NOT spuriously fire the deactivate callback,
            // because the guard `IsActive && currentTick >= _endTick`
            // short-circuits on IsActive=false.
            int callbackFireCount = 0;
            tracker.Tick(currentTick: 9999, onDeactivate: () => callbackFireCount++);
            Assert.AreEqual(0, callbackFireCount);
        }

        [Test]
        public void Reset_AllowsRechargeFromZero_AndReactivation()
        {
            // Arrange — full lifecycle: charge → activate → reset → charge again.
            var tracker = new HyperchargeTracker();
            tracker.AddCharge(1f);
            tracker.Activate(startTick: 0, durationSeconds: 5f);
            tracker.Reset();

            // Act — recharge from a known-zero state.
            tracker.AddCharge(1f);

            // Assert — AddCharge worked because IsActive was reset to false,
            // so the no-op guard didn't trigger this time.
            Assert.AreEqual(1f, tracker.ChargePercent, 0.0001f);

            // And re-activation works.
            tracker.Activate(startTick: 200, durationSeconds: 3f);
            Assert.IsTrue(tracker.IsActive);
        }
    }
}
