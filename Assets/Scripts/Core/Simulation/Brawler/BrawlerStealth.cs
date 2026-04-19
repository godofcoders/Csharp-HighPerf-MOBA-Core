namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the brawler's stealth / visibility state — the data and rules for
    /// "am I currently hidden?" as a pure question about this brawler alone,
    /// ignoring who might be looking.
    ///
    /// The broader "IsHiddenTo(observerTeam)" question stays on BrawlerState
    /// as a coordinator, because it needs brawler-level context (Team) and a
    /// service lookup (ISimulationClock.CurrentTick). This class stays a
    /// POCO — no Unity types, no services.
    /// </summary>
    public class BrawlerStealth
    {
        /// <summary>
        /// Ticks after firing a shot during which the brawler is visible even
        /// while standing in a bush (classic "shooting reveals you" window).
        /// 60 ticks = ~2 seconds at the sim's 30 TPS.
        /// </summary>
        public const uint RecentlyAttackedTicks = 60;

        public bool IsInBush { get; set; }
        public bool IsRevealed { get; set; }

        /// <summary>
        /// Last tick this brawler fired a main attack. Used by IsHidden to
        /// apply the recently-attacked visibility window.
        /// </summary>
        public uint LastAttackTick { get; set; }

        /// <summary>
        /// True if the brawler is currently hidden from observers — i.e.
        /// standing in a bush, not revealed by an effect, and not within the
        /// recently-attacked visibility window. The observer's team is NOT
        /// considered here; allies seeing through stealth is a coordinator
        /// concern handled by BrawlerState.IsHiddenTo.
        /// </summary>
        public bool IsHidden(uint currentTick)
        {
            if (!IsInBush)
                return false;

            if (IsRevealed)
                return false;

            // Recently-attacked window: shooting in a bush reveals you
            // briefly. Uses uint subtraction, which is safe here because
            // currentTick only grows and LastAttackTick is only ever assigned
            // to a current tick value (so it's always <= currentTick in
            // practice).
            bool recentlyAttacked = (currentTick - LastAttackTick) < RecentlyAttackedTicks;
            if (recentlyAttacked)
                return false;

            return true;
        }

        /// <summary>Clears stealth flags on respawn. LastAttackTick back to 0, both flags off.</summary>
        public void Reset()
        {
            IsInBush = false;
            IsRevealed = false;
            LastAttackTick = 0;
        }
    }
}
