namespace MOBA.Core.Simulation
{
    public abstract class TimedControlStatusEffect : IStatusEffectInstance
    {
        public abstract StatusEffectType Type { get; }

        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        protected object SourceToken { get; private set; }

        protected TimedControlStatusEffect(uint startTick, uint endTick, object sourceToken)
        {
            StartTick = startTick;
            EndTick = endTick;
            SourceToken = sourceToken;
        }

        public virtual void Apply(IStatusTarget target, uint currentTick)
        {
        }

        public virtual void Tick(IStatusTarget target, uint currentTick)
        {
        }

        public virtual void Remove(IStatusTarget target, uint currentTick)
        {
        }

        public virtual bool IsExpired(uint currentTick)
        {
            return currentTick >= EndTick;
        }

        public virtual bool CanMerge(StatusEffectContext context)
        {
            return context.Type == Type;
        }

        public virtual void Merge(StatusEffectContext context, uint currentTick)
        {
            uint durationTicks = (uint)(context.Duration * 30f);
            uint newEndTick = currentTick + durationTicks;

            if (newEndTick > EndTick)
                EndTick = newEndTick;

            SourceToken = context.SourceToken;
        }
    }
}