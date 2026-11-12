using System;
using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Groups tickables by phase, and within a single tick, runs each phase
    /// to completion in phase-order before the next phase starts.
    ///
    /// Within a phase, tickables run in reverse registration order.
    /// New entries in the current phase wait until the next tick.
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

        private List<ITickable> _dispatchingList;
        private int _nextIndex;

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
            var list = _byPhase[phase];
            int index = list.IndexOf(tickable);
            if (index < 0)
                return;

            list.RemoveAt(index);
            if (list == _dispatchingList && index <= _nextIndex)
                _nextIndex--;
        }

        public void TickAll(uint currentTick)
        {
            if (_dispatchingList != null)
                throw new InvalidOperationException("Simulation tick dispatch cannot be nested.");

            try
            {
                for (int p = 0; p < OrderedPhases.Length; p++)
                {
                    _dispatchingList = _byPhase[OrderedPhases[p]];
                    _nextIndex = _dispatchingList.Count - 1;
                    while (_nextIndex >= 0)
                        _dispatchingList[_nextIndex--].Tick(currentTick);
                }
            }
            finally
            {
                _dispatchingList = null;
            }
        }
    }
}
