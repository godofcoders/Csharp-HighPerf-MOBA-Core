using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public class SimulationRegistry
    {
        // ITickable must be public for this to work!
        private readonly List<ITickable> _tickables = new List<ITickable>();

        // Ensure these are PUBLIC
        public void Register(ITickable tickable)
        {
            if (!_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }

        public void Unregister(ITickable tickable)
        {
            if (_tickables.Contains(tickable))
                _tickables.Remove(tickable);
        }

        public void TickAll(uint currentTick)
        {
            for (int i = _tickables.Count - 1; i >= 0; i--)
            {
                _tickables[i].Tick(currentTick);
            }
        }
    }
}