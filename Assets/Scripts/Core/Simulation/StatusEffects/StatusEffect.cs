namespace MOBA.Core.Simulation
{
    public abstract class StatusEffect
    {
        public uint EndTick { get; private set; }
        public bool IsExpired(uint currentTick) => currentTick >= EndTick;

        protected BrawlerState Target;

        public void Initialize(BrawlerState target, float durationSeconds, uint startTick)
        {
            Target = target;
            // Convert seconds to ticks (30Hz)
            uint durationTicks = (uint)(durationSeconds * 30);
            EndTick = startTick + durationTicks;
            OnApply();
        }

        public abstract void OnApply();
        public abstract void OnTick(uint currentTick);
        public abstract void OnRemove();
    }
}