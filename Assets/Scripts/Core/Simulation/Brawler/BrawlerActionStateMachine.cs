namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the brawler's current "action state" — the lock-out window during
    /// which the brawler is mid-cast, mid-recovery, dead, stunned-by-state,
    /// etc. Each state carries flags for whether movement and action input are
    /// allowed, and whether it can be cancelled (interrupted).
    ///
    /// Design notes:
    ///
    /// 1) BrawlerActionStateData is a struct. To avoid the "mutation lost in a
    ///    copy" foot-gun that bit us in BrawlerCooldowns, every state
    ///    transition here REPLACES the whole `Current` struct (via the
    ///    private setter) rather than calling a mutating method on it. That
    ///    means the struct-in-property pattern is safe — we only write to it
    ///    by assignment, never by `Current.X = ...`.
    ///
    /// 2) POCO: no Unity types, no events, no Debug.Log. The broader
    ///    BrawlerState coordinator still owns the "can I act?" / "can I move?"
    ///    composite checks because those blend action state with other
    ///    concerns (IsDead, IsStunned, status effects).
    /// </summary>
    public class BrawlerActionStateMachine
    {
        public BrawlerActionStateData Current { get; private set; }

        public BrawlerActionStateMachine()
        {
            Clear();
        }

        /// <summary>
        /// Enters a new action state with the given duration and movement/
        /// input/interrupt flags. Overwrites any existing state.
        /// </summary>
        public void Enter(
            BrawlerActionStateType stateType,
            uint currentTick,
            uint durationTicks,
            bool allowMovement,
            bool allowActionInput,
            bool isInterruptible)
        {
            Current = new BrawlerActionStateData
            {
                StateType = stateType,
                StartTick = currentTick,
                LockUntilTick = currentTick + durationTicks,
                AllowMovement = allowMovement,
                AllowActionInput = allowActionInput,
                IsInterruptible = isInterruptible
            };
        }

        /// <summary>
        /// Resets to the default "no active state" — movement and input
        /// allowed, interruptible (a no-op if called again), lock expired.
        /// </summary>
        public void Clear()
        {
            Current = new BrawlerActionStateData
            {
                StateType = BrawlerActionStateType.None,
                StartTick = 0,
                LockUntilTick = 0,
                AllowMovement = true,
                AllowActionInput = true,
                IsInterruptible = true
            };
        }

        /// <summary>
        /// If the current state has expired (lock tick reached), clears it.
        /// Called each simulation tick by the brawler's update path.
        /// </summary>
        public void UpdateExpiry(uint currentTick)
        {
            if (Current.StateType != BrawlerActionStateType.None &&
                !Current.IsActive(currentTick))
            {
                Clear();
            }
        }

        /// <summary>True if some non-None state is currently within its lock window.</summary>
        public bool IsActive(uint currentTick)
        {
            return Current.StateType != BrawlerActionStateType.None &&
                   Current.IsActive(currentTick);
        }

        /// <summary>True if the brawler is in the given specific state right now.</summary>
        public bool IsInState(BrawlerActionStateType type, uint currentTick)
        {
            return Current.StateType == type && Current.IsActive(currentTick);
        }

        /// <summary>
        /// Attempts to cancel the current state. Returns true if there was
        /// nothing to interrupt (trivially succeeded) or if the state allowed
        /// interruption and has now been cleared. Returns false if the current
        /// state is non-interruptible (e.g. dead, hard-locked cast).
        /// </summary>
        public bool TryInterrupt()
        {
            if (Current.StateType == BrawlerActionStateType.None)
                return true;

            if (!Current.IsInterruptible)
                return false;

            Clear();
            return true;
        }
    }
}
