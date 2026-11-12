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

        private readonly Dictionary<TickPhase, int> _phaseIndices;
        private readonly List<ITickable>[] _phaseBuckets;

        private List<ITickable> _dispatchingList;
        private int _nextIndex;

        public SimulationRegistry()
        {
            _phaseIndices = new Dictionary<TickPhase, int>(OrderedPhases.Length);
            _phaseBuckets = new List<ITickable>[OrderedPhases.Length];
            for (int i = 0; i < OrderedPhases.Length; i++)
            {
                _phaseIndices.Add(OrderedPhases[i], i);
                _phaseBuckets[i] = new List<ITickable>(32);
            }
        }

        public void Register(ITickable tickable, TickPhase phase)
        {
            var list = _phaseBuckets[_phaseIndices[phase]];
            if (!list.Contains(tickable))
                list.Add(tickable);
        }

        public void Unregister(ITickable tickable, TickPhase phase)
        {
            var list = _phaseBuckets[_phaseIndices[phase]];
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
                for (int p = 0; p < _phaseBuckets.Length; p++)
                {
                    _dispatchingList = _phaseBuckets[p];
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
