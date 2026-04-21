namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Base class for a live instance of a super-charge source installed
    /// against a specific brawler at runtime. A concrete subclass lives
    /// inside the <c>MOBA.Core.Simulation</c> namespace and is spawned by
    /// the matching <c>SuperChargeSourceDefinition.CreateRuntime()</c>.
    ///
    /// Runtime objects are POCO — no Unity types, no events, no singletons
    /// — and receive the owning <see cref="BrawlerState"/> through method
    /// parameters (never stored as a field on the base, to avoid stale
    /// references across respawn swaps).
    ///
    /// Hook overview:
    ///   - <see cref="OnInstalled"/> / <see cref="OnUninstalled"/>
    ///       one-shot lifecycle for any runtime wiring (timers, caches)
    ///   - <see cref="Tick"/>
    ///       per-simulation-tick update (auto-over-time, proximity scans)
    ///   - <see cref="OnDamageDealt"/>
    ///       pushed from <c>DamageService</c> once damage is finalised
    ///   - <see cref="OnHealApplied"/>
    ///       pushed from the heal pipeline once a heal lands on a target
    ///
    /// A concrete runtime only overrides the hooks it cares about —
    /// base implementations are all no-ops so the four concrete pairs
    /// stay small.
    /// </summary>
    public abstract class SuperChargeSourceRuntime
    {
        /// <summary>
        /// Called once immediately after the runtime is created and paired
        /// with the owning brawler. Use this to cache per-instance data
        /// (e.g. timers initialised to the owner's current tick).
        /// </summary>
        public virtual void OnInstalled(BrawlerState owner)
        {
        }

        /// <summary>
        /// Called once just before the runtime is discarded (loadout swap,
        /// passive rebuild, etc.). Use to clear anything that might leak
        /// across a respawn — though because these are POCOs with no event
        /// subscriptions, most subclasses can leave this as the no-op base.
        /// </summary>
        public virtual void OnUninstalled(BrawlerState owner)
        {
        }

        /// <summary>
        /// Per-simulation-tick update. <paramref name="deltaTime"/> is the
        /// fixed simulation step (1/30s at 30 TPS). Sources that charge
        /// passively (auto-over-time, ally-proximity) do their work here;
        /// sources that only react to events (damage/heal) leave it as the
        /// no-op base.
        /// </summary>
        public virtual void Tick(BrawlerState owner, float deltaTime, uint currentTick)
        {
        }

        /// <summary>
        /// Pushed from <c>DamageService</c> once a hit is fully resolved
        /// (after outgoing/incoming modifiers and shields). The amount is
        /// the finalised damage actually delivered, so shield-soaked chunks
        /// don't double-count.
        /// </summary>
        public virtual void OnDamageDealt(BrawlerState owner, float damageAmount, BrawlerState victim)
        {
        }

        /// <summary>
        /// Pushed from the heal pipeline once a heal lands on a target.
        /// The amount is the finalised healing applied after clamping to
        /// the recipient's missing health, so over-heal doesn't count.
        /// </summary>
        public virtual void OnHealApplied(BrawlerState owner, float healAmount, BrawlerState recipient)
        {
        }
    }
}
