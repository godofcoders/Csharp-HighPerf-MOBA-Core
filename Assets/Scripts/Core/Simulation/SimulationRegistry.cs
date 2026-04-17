using System;
using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Groups tickables by phase, and within a single tick, runs each phase
    /// to completion in phase-order before the next phase starts.
    ///
    /// Within a phase, execution order is insertion order (fine — the whole
    /// point of phases is that order BETWEEN phases matters; order WITHIN a
    /// phase is a don't-care for correctness).
    ///
    /// One entity may register to multiple phases (e.g. ProjectileManager
    /// registers to both Movement and Collision). It calls Register() twice.
    /// </summary>
    public class SimulationRegistry
    {
        // Cached once. Enum.GetValues returns ascending-value order, which is
        // our intended phase order (PreTick=0 ... PostTick=80).
        private static readonly TickPhase[] OrderedPhases =
            (TickPhase[])Enum.GetValues(typeof(TickPhase));

        private readonly Dictionary<TickPhase, List<ITickable>> _byPhase;

        public SimulationRegistry()
        {
            // Pre-populate every phase bucket so Register() never allocates
            // a new list during gameplay. Tiny detail, big frame-time win.
            _byPhase = new Dictionary<TickPhase, List<ITickable>>(OrderedPhases.Length);
            foreach (var phase in OrderedPhases)
                _byPhase[phase] = new List<ITickable>(32);
        }

        public void Register(ITickable tickable, TickPhase phase)
        {
            var list = _byPhase[phase];
            if (!list.Contains(tickable))
                list.Add(tickable);
        }

        public void Unregister(ITickable tickable, TickPhase phase)
        {
            _byPhase[phase].Remove(tickable);
        }

        public void TickAll(uint currentTick)
        {
            for (int p = 0; p < OrderedPhases.Length; p++)
            {
                var list = _byPhase[OrderedPhases[p]];

                // Reverse iteration is deliberate. If a Tick() callback causes
                // the entity to Unregister itself (common — e.g. a projectile
                // hits, despawns, and removes itself from the registry), reverse
                // iteration won't skip elements. Forward iteration would.
                for (int i = list.Count - 1; i >= 0; i--)
                    list[i].Tick(currentTick);
            }
        }
    }
}