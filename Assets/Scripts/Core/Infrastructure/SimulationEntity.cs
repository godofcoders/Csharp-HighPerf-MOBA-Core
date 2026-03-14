using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    // This is the parent of all 'Living' objects in our simulation
    public abstract class SimulationEntity : MonoBehaviour, ITickable
    {
        // Must be 'protected virtual' so BrawlerController can override it
        protected virtual void Awake()
        {
            // Base initialization logic if needed
        }

        protected virtual void OnEnable()
        {
            // Accessing the static Registry we built in SimulationClock
            SimulationClock.Registry?.Register(this);
        }

        protected virtual void OnDisable()
        {
            SimulationClock.Registry?.Unregister(this);
        }

        // Every child must implement their own Tick logic
        public abstract void Tick(uint currentTick);
    }
}