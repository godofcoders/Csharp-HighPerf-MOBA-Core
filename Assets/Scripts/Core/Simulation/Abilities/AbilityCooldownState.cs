namespace MOBA.Core.Simulation
{
    public struct AbilityCooldownState
    {
        public uint ReadyAtTick;

        // Original cooldown length in ticks at the moment StartCooldown was
        // called. Stored so callers (HUD overlays, AI scoring) can compute
        // "how far through the cooldown am I?" without needing to remember
        // the original duration themselves. Reset() clears it back to 0.
        public uint DurationTicks;

        public bool IsReady(uint currentTick)
        {
            return currentTick >= ReadyAtTick;
        }

        public void StartCooldown(uint currentTick, uint cooldownTicks)
        {
            ReadyAtTick = currentTick + cooldownTicks;
            DurationTicks = cooldownTicks;
        }

        /// <summary>
        /// Returns 1 when cooldown was just started, decaying linearly to 0
        /// at the moment the ability becomes ready. Returns 0 when no
        /// cooldown is active. Drives radial-sweep cooldown overlays in the
        /// HUD.
        /// </summary>
        public float GetProgress(uint currentTick)
        {
            if (DurationTicks == 0) return 0f;
            if (currentTick >= ReadyAtTick) return 0f;
            uint remaining = ReadyAtTick - currentTick;
            float t = (float)remaining / (float)DurationTicks;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        public void Reset()
        {
            ReadyAtTick = 0;
            DurationTicks = 0;
        }
    }
}