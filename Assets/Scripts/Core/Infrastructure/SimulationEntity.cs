using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    // This is the parent of all 'Living' objects in our simulation
    public abstract class SimulationEntity : MonoBehaviour, ITickable
    {
        // Declares which tick phase this entity runs in. Default is Movement
        // because most entities (brawlers, deployables) primarily integrate
        // position and run their main gameplay logic during Movement.
        //
        // Subclasses override this to opt into a different phase. Example:
        // BrawlerAIController returns InputApply so AI produces commands
        // BEFORE the brawler consumes them in Movement — this makes ordering
        // guaranteed by the phase contract, not by Unity component order.
        //
        // Single phase per entity for now. If we later need an entity to run
        // in multiple phases (e.g. ProjectileManager split into Movement +
        // Collision passes), we can promote this to `TickPhase[] Phases` and
        // register for each — the registry already supports that.
        protected virtual TickPhase Phase => TickPhase.Movement;

        // Must be 'protected virtual' so BrawlerController can override it
        protected virtual void Awake()
        {
            // Base initialization logic if needed
        }

        protected virtual void OnEnable()
        {
            // Accessing the static Registry we built in SimulationClock
            SimulationClock.Registry?.Register(this, Phase);
        }

        protected virtual void OnDisable()
        {
            SimulationClock.Registry?.Unregister(this, Phase);
        }

        // Every child must implement their own Tick logic
        public abstract void Tick(uint currentTick);
    }
}