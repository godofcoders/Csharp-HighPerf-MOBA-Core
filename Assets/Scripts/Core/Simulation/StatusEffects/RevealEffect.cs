namespace MOBA.Core.Simulation
{
    public sealed class RevealEffect : IStatusEffectInstance
    {
        public StatusEffectType Type => StatusEffectType.Reveal;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        public RevealEffect(float durationSeconds, uint currentTick)
        {
            StartTick = currentTick;
            EndTick = currentTick + (uint)(durationSeconds * 30f);
        }

        public void Apply(IStatusTarget target, uint currentTick)
        {
            if (target is BrawlerState brawlerState)
                brawlerState.IsRevealed = true;
        }

        public void Tick(IStatusTarget target, uint currentTick)
        {
        }

        public void Remove(IStatusTarget target, uint currentTick)
        {
            if (target is BrawlerState brawlerState)
                brawlerState.IsRevealed = false;
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Reveal;
        }

        public void Merge(StatusEffectContext context, uint currentTick)
        {
            uint durationTicks = (uint)(context.Duration * 30f);
            uint newEndTick = currentTick + durationTicks;

            if (newEndTick > EndTick)
                EndTick = newEndTick;
        }

        public bool IsExpired(uint currentTick)
        {
            return currentTick >= EndTick;
        }
    }
}