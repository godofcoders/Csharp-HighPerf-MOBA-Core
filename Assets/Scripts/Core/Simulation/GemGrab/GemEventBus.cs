using System;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Static event bus for Gem-related signals that are too narrow to
    /// deserve a full subscriber list per brawler. Currently just one
    /// event: gem pickup. MatchStatsTracker subscribes to attribute
    /// per-brawler GemsCollected counts.
    ///
    /// Subscribers MUST unsubscribe on disable / scene unload (event-bus
    /// gotcha). Save the handler delegate to a field so the same instance
    /// is removed at TearDown — see TESTING.md note on
    /// same-delegate-instance discipline.
    /// </summary>
    public static class GemEventBus
    {
        /// <summary>Fired immediately after a Gem.TryPickupBy succeeds.
        /// Args: (carrier, amount). amount is the gem's Value (default 1
        /// for spawner-spawned gems; up-to-N for the death-drop scatter
        /// before it was switched to single-value gems).</summary>
        public static Action<BrawlerState, int> OnGemPickedUp;
    }
}
