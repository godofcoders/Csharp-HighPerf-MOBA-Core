namespace MOBA.Core.Simulation
{
    public sealed class StunEffect : IStatusEffectInstance
    {
        public StatusEffectType Type => StatusEffectType.Stun;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        public StunEffect(float durationSeconds, uint currentTick)
        {
            StartTick = currentTick;
            EndTick = currentTick + SecondsToTicks(durationSeconds);
        }

        public void Apply(BrawlerState state, uint currentTick)
        {
            state.IsStunned = true;
        }

        public void Tick(BrawlerState state, uint currentTick) { }

        public void Remove(BrawlerState state, uint currentTick)
        {
            state.IsStunned = false;
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Stun;
        }

        public void Merge(StatusEffectContext context, uint currentTick)
        {
            uint newEnd = currentTick + SecondsToTicks(context.Duration);
            if (newEnd > EndTick)
                EndTick = newEnd;
        }

        public bool IsExpired(uint currentTick)
        {
            return currentTick >= EndTick;
        }

        private static uint SecondsToTicks(float seconds)
        {
            return (uint)(seconds * 30f);
        }
    }
}