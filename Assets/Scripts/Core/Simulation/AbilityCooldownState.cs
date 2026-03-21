namespace MOBA.Core.Simulation
{
    public struct AbilityCooldownState
    {
        public uint ReadyAtTick;

        public bool IsReady(uint currentTick)
        {
            return currentTick >= ReadyAtTick;
        }

        public void StartCooldown(uint currentTick, uint cooldownTicks)
        {
            ReadyAtTick = currentTick + cooldownTicks;
        }

        public void Reset()
        {
            ReadyAtTick = 0;
        }
    }
}