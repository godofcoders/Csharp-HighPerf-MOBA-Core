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
        public const uint RecentlyDamagedTicks = 60;

        public bool IsInBush { get; set; }

        public bool IsProximityRevealed { get; set; }
        public bool IsStatusRevealed { get; set; }

        public bool IsRevealed
        {
            get => IsProximityRevealed || IsStatusRevealed;
            set => IsStatusRevealed = value;
        }

        private bool _hasAttackRevealTick;
        private bool _hasDamageRevealTick;
        private uint _lastAttackTick;
        private uint _lastDamageTakenTick;

        /// <summary>
        /// Last tick this brawler fired a main attack. Used by IsHidden to
        /// apply the recently-attacked visibility window.
        /// </summary>
        public uint LastAttackTick
        {
            get => _lastAttackTick;
            set
            {
                _lastAttackTick = value;
                _hasAttackRevealTick = true;
            }
        }

        public uint LastDamageTakenTick
        {
            get => _lastDamageTakenTick;
            set
            {
                _lastDamageTakenTick = value;
                _hasDamageRevealTick = true;
            }
        }

        public bool IsAttackRevealed(uint currentTick)
        {
            return _hasAttackRevealTick &&
                   (currentTick - _lastAttackTick) < RecentlyAttackedTicks;
        }

        public bool IsDamageRevealed(uint currentTick)
        {
            return _hasDamageRevealTick &&
                   (currentTick - _lastDamageTakenTick) < RecentlyDamagedTicks;
        }

        public void MarkDamageTaken(uint currentTick)
        {
            LastDamageTakenTick = currentTick;
        }

        /// <summary>
        /// True if the brawler is currently hidden from observers — i.e.
        /// standing in a bush, not revealed by an effect/proximity, and not
        /// within recent attack/damage visibility windows. The observer's
        /// team is NOT considered here; allies seeing through stealth is a
        /// coordinator concern handled by BrawlerState.IsHiddenTo.
        /// </summary>
        public bool IsHidden(uint currentTick)
        {
            if (!IsInBush)
                return false;

            if (IsRevealed)
                return false;

            if (IsAttackRevealed(currentTick))
                return false;

            if (IsDamageRevealed(currentTick))
                return false;

            return true;
        }

        /// <summary>Clears stealth flags on respawn. Tick values read as 0 but are not active reveal windows.</summary>
        public void Reset()
        {
            IsInBush = false;
            IsProximityRevealed = false;
            IsStatusRevealed = false;
            _lastAttackTick = 0;
            _lastDamageTakenTick = 0;
            _hasAttackRevealTick = false;
            _hasDamageRevealTick = false;
        }
    }
}
