using System;
using MOBA.Core.Simulation;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    // Unit tests for SimulationClock.SecondsToTicks — the single source of truth
    // for converting designer-typed durations (seconds) into the integer tick
    // counts the deterministic simulation actually runs on.
    //
    // Why this fixture matters even though the helper is one line:
    // we discovered TWO different bug shapes for this conversion in the wild
    // — `(uint)(seconds / TickDeltaTime)` truncating 1.0s to 29 ticks, and
    // `(uint)(seconds * 30f)` silently coupling every site to 30 TPS. The
    // helper exists specifically to kill both. These tests pin the contract
    // so any future "simplification" that reintroduces either bug shape goes
    // red immediately.
    public class SimulationClockSecondsToTicksTests
    {
        [Test]
        public void SecondsToTicks_OneSecond_Returns30Ticks_NotTruncatedTo29()
        {
            // The bug we just killed in BrawlerCooldowns and HyperchargeTracker.
            // 1f / (1f/30f) is 29.9999998f in IEEE float — a naive (uint) cast
            // would drop it to 29. The +0.5f round-to-nearest fixes it.
            Assert.AreEqual(30u, SimulationClock.SecondsToTicks(1f));
        }

        [Test]
        public void SecondsToTicks_FractionalSeconds_Roundtrip()
        {
            // 0.5s @ 30 TPS == 15 ticks exactly (when rounded-to-nearest).
            // Naive truncation would give 14.
            Assert.AreEqual(15u, SimulationClock.SecondsToTicks(0.5f));
        }

        [Test]
        public void SecondsToTicks_Zero_ReturnsZero()
        {
            // Edge case: 0 seconds == 0 ticks. The +0.5f musn't accidentally
            // round 0 up to 1 (0 + 0.5 = 0.5, which truncates back to 0).
            Assert.AreEqual(0u, SimulationClock.SecondsToTicks(0f));
        }

        [Test]
        public void SecondsToTicks_RoundsToNearestTick_NotDownAndNotUp()
        {
            // Tick boundary is 1f/30f ≈ 33.33ms. The half-tick boundary is
            // ~16.67ms. Inputs above that round up; inputs below round down.

            // 0.0167s ≈ 0.501 ticks → rounds to 1.
            Assert.AreEqual(1u, SimulationClock.SecondsToTicks(0.0167f));

            // 0.0166s ≈ 0.498 ticks → rounds to 0.
            Assert.AreEqual(0u, SimulationClock.SecondsToTicks(0.0166f));
        }
    }

    public class SimulationTimingTests
    {
        [Test]
        public void Contract_UsesThirtyDeterministicTicksPerSecond()
        {
            Assert.AreEqual(30, SimulationTiming.TicksPerSecond);
            Assert.AreEqual(1f / 30f, SimulationTiming.TickDeltaSeconds);
            Assert.AreEqual(30u, SimulationTiming.SecondsToTicks(1f));
            Assert.AreEqual(1f, SimulationTiming.TicksToSeconds(30u));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        public void SecondsToTicks_NonPositiveDurationsReturnZero(float seconds)
        {
            Assert.AreEqual(0u, SimulationTiming.SecondsToTicks(seconds));
        }

        [Test]
        public void SecondsToTicks_InvalidAndExcessiveDurationsAreSafe()
        {
            Assert.AreEqual(0u, SimulationTiming.SecondsToTicks(float.NaN));
            Assert.AreEqual(uint.MaxValue, SimulationTiming.SecondsToTicks(float.PositiveInfinity));
        }

        [Test]
        public void TickProcessor_UsesConfiguredRateWithoutFloatDrift()
        {
            TickProcessor processor = new TickProcessor(60);

            int processed = 0;
            for (int i = 0; i < 60; i++)
                processed += processor.Update(1f / 60f);

            Assert.AreEqual(60, processed);
            Assert.AreEqual(60u, processor.CurrentTick);
            Assert.AreEqual(1f / 60f, processor.TickDelta);
        }

        [Test]
        public void TickProcessor_RejectsInvalidRatesAndDeltas()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TickProcessor(0));

            TickProcessor processor = new TickProcessor(SimulationTiming.TicksPerSecond);
            Assert.AreEqual(0, processor.Update(float.NaN));
            Assert.AreEqual(0, processor.Update(float.PositiveInfinity));
            Assert.AreEqual(0, processor.Update(-1f));
            Assert.AreEqual(0u, processor.CurrentTick);
        }
    }
}
