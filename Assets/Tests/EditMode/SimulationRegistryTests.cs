using System.Collections.Generic;
using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for SimulationRegistry — the phase-bucketed tick scheduler.
    //
    // The single most important invariant tested here is phase ordering. The
    // whole tick model is built on the contract that, within one tick:
    //   PreTick → InputApply → AbilityCast → Movement → Collision →
    //   DamageResolution → StatusEffectTick → Cleanup → PostTick
    // runs in that order, every time, deterministically. Replays, future
    // networking rollback, headless server simulation — all of it depends on
    // this order being immutable. If anyone refactors and silently flips two
    // phases, this suite is the trip-wire.
    //
    // The reverse-iteration safety test pins the other subtle invariant: a
    // tickable can unregister ITSELF from inside its Tick() callback (very
    // common — projectiles do this when they hit). Forward iteration would
    // skip the next neighbor; reverse iteration doesn't. Test
    // TickAll_TickableUnregistersItselfMidTick_DoesNotSkipNeighbor locks that
    // behaviour in.
    public class SimulationRegistryTests
    {
        // ---------- Test fakes ----------

        // Records every tick into a shared list so the test can assert on
        // both whether-it-ticked and what-order-things-ticked-in.
        private sealed class RecordingTickable : ITickable
        {
            private readonly TickPhase _identity;
            private readonly List<TickPhase> _orderObserved;
            public int TickCount { get; private set; }
            public uint LastCurrentTick { get; private set; }

            public RecordingTickable(TickPhase identity, List<TickPhase> orderObserved)
            {
                _identity = identity;
                _orderObserved = orderObserved;
            }

            public void Tick(uint currentTick)
            {
                TickCount++;
                LastCurrentTick = currentTick;
                _orderObserved?.Add(_identity);
            }
        }

        // Removes itself from the registry inside its own Tick() callback.
        // Used to verify reverse-iteration safety.
        private sealed class SelfUnregisteringTickable : ITickable
        {
            private readonly SimulationRegistry _registry;
            private readonly TickPhase _phase;
            public int TickCount { get; private set; }

            public SelfUnregisteringTickable(SimulationRegistry registry, TickPhase phase)
            {
                _registry = registry;
                _phase = phase;
            }

            public void Tick(uint currentTick)
            {
                TickCount++;
                _registry.Unregister(this, _phase);
            }
        }

        // ---------- Tests ----------

        [Test]
        public void Register_AddsTickable_AndItGetsTickedOnTickAll()
        {
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable tickable = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(tickable, TickPhase.Movement);
            registry.TickAll(currentTick: 0);

            Assert.AreEqual(1, tickable.TickCount);
        }

        [Test]
        public void Register_DuplicateRegistration_OnlyTicksOnce()
        {
            // Register() is idempotent on a per-(tickable, phase) basis. This
            // matters because subsystems sometimes try to "make sure" they're
            // registered after a state change. We don't want them to start
            // ticking twice.
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable tickable = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(tickable, TickPhase.Movement);
            registry.Register(tickable, TickPhase.Movement);
            registry.TickAll(0);

            Assert.AreEqual(1, tickable.TickCount);
        }

        [Test]
        public void Unregister_RemovesTickable_AndItStopsTicking()
        {
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable tickable = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(tickable, TickPhase.Movement);
            registry.TickAll(0);
            registry.Unregister(tickable, TickPhase.Movement);
            registry.TickAll(1);

            Assert.AreEqual(1, tickable.TickCount);
        }

        [Test]
        public void TickAll_RunsPhasesInDeclaredOrder()
        {
            // THE critical determinism invariant. Every replay, every future
            // networked rollback, every headless server tick depends on this
            // exact ordering. Register in REVERSE order so we don't trivially
            // satisfy the assertion through insertion order.
            SimulationRegistry registry = new SimulationRegistry();
            List<TickPhase> orderObserved = new List<TickPhase>();

            registry.Register(new RecordingTickable(TickPhase.PostTick, orderObserved), TickPhase.PostTick);
            registry.Register(new RecordingTickable(TickPhase.Cleanup, orderObserved), TickPhase.Cleanup);
            registry.Register(new RecordingTickable(TickPhase.StatusEffectTick, orderObserved), TickPhase.StatusEffectTick);
            registry.Register(new RecordingTickable(TickPhase.DamageResolution, orderObserved), TickPhase.DamageResolution);
            registry.Register(new RecordingTickable(TickPhase.Collision, orderObserved), TickPhase.Collision);
            registry.Register(new RecordingTickable(TickPhase.Movement, orderObserved), TickPhase.Movement);
            registry.Register(new RecordingTickable(TickPhase.AbilityCast, orderObserved), TickPhase.AbilityCast);
            registry.Register(new RecordingTickable(TickPhase.InputApply, orderObserved), TickPhase.InputApply);
            registry.Register(new RecordingTickable(TickPhase.PreTick, orderObserved), TickPhase.PreTick);

            registry.TickAll(currentTick: 0);

            CollectionAssert.AreEqual(
                new[]
                {
                    TickPhase.PreTick,
                    TickPhase.InputApply,
                    TickPhase.AbilityCast,
                    TickPhase.Movement,
                    TickPhase.Collision,
                    TickPhase.DamageResolution,
                    TickPhase.StatusEffectTick,
                    TickPhase.Cleanup,
                    TickPhase.PostTick,
                },
                orderObserved);
        }

        [Test]
        public void TickAll_PassesCurrentTick_ToTickable()
        {
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable tickable = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(tickable, TickPhase.Movement);
            registry.TickAll(currentTick: 42);

            Assert.AreEqual(42u, tickable.LastCurrentTick);
        }

        [Test]
        public void TickAll_TickableUnregistersItselfMidTick_DoesNotSkipNeighbor()
        {
            // Reverse iteration safety. A projectile that hits and despawns
            // unregisters itself from its own Tick(). The neighbour just
            // ahead of it in the list must STILL be ticked on the same call.
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable a = new RecordingTickable(TickPhase.Movement, null);
            SelfUnregisteringTickable b = new SelfUnregisteringTickable(registry, TickPhase.Movement);
            RecordingTickable c = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(a, TickPhase.Movement);
            registry.Register(b, TickPhase.Movement);
            registry.Register(c, TickPhase.Movement);

            registry.TickAll(0);

            Assert.AreEqual(1, a.TickCount, "neighbour A must still tick");
            Assert.AreEqual(1, b.TickCount, "self-unregistering B ticks once");
            Assert.AreEqual(1, c.TickCount, "neighbour C must still tick");

            registry.TickAll(1);

            Assert.AreEqual(2, a.TickCount, "A still alive next tick");
            Assert.AreEqual(1, b.TickCount, "B is gone, must not tick again");
            Assert.AreEqual(2, c.TickCount, "C still alive next tick");
        }

        [Test]
        public void TickAll_EntityRegisteredToMultiplePhases_TicksOncePerPhase()
        {
            // ProjectileManager registers to both Movement and Collision in
            // production code. It must tick once per phase, not once total.
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable multiPhase = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(multiPhase, TickPhase.Movement);
            registry.Register(multiPhase, TickPhase.Collision);

            registry.TickAll(0);

            Assert.AreEqual(2, multiPhase.TickCount);
        }

        [Test]
        public void Register_DifferentPhases_TickIndependently()
        {
            // Sanity: removing a tickable from one phase does not affect its
            // registration in another phase.
            SimulationRegistry registry = new SimulationRegistry();
            RecordingTickable t = new RecordingTickable(TickPhase.Movement, null);

            registry.Register(t, TickPhase.Movement);
            registry.Register(t, TickPhase.Cleanup);
            registry.Unregister(t, TickPhase.Movement);
            registry.TickAll(0);

            Assert.AreEqual(1, t.TickCount, "ticked from Cleanup phase only");
        }
    }
}
