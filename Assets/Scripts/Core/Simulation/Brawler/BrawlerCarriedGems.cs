namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the brawler's Gem Grab carrier state — how many gems they're
    /// currently holding. Stays a POCO: no Unity types, no events, no
    /// singletons. Pure count + mutators.
    ///
    /// Gameplay rules pinned here (Brawl Stars Gem Grab):
    ///   - On pickup, count goes up by the gem's value (default 1 per gem).
    ///   - On death, all carried gems are dropped — i.e. count goes to 0.
    ///     The actual gem-drop spawning is the coordinator's job (it needs
    ///     position + spawning), but the count clear lives here.
    ///   - On cashout (future Day 3 work), the team's match score increases
    ///     by Count and Count goes to 0.
    /// </summary>
    public class BrawlerCarriedGems
    {
        public int Count { get; private set; }

        /// <summary>Adds the supplied number of gems to the carrier. Negative
        /// values are clamped to zero — use <see cref="Clear"/> to wipe.</summary>
        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            Count += amount;
        }

        /// <summary>Sets <see cref="Count"/> to 0. Used on death-drop and on
        /// cashout. Returns the dropped count so callers can spawn that many
        /// world-gems at the brawler's last position.</summary>
        public int Clear()
        {
            int dropped = Count;
            Count = 0;
            return dropped;
        }
    }
}
