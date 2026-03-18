namespace MOBA.Core.Simulation
{
    public sealed class SlowEffect : IStatusEffectInstance
    {
        private readonly object _sourceToken;
        private float _magnitude;

        public StatusEffectType Type => StatusEffectType.Slow;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        public SlowEffect(float magnitude, float durationSeconds, object sourceToken, uint currentTick)
        {
            _magnitude = magnitude;
            _sourceToken = sourceToken ?? this;
            StartTick = currentTick;
            EndTick = currentTick + SecondsToTicks(durationSeconds);
        }

        public void Apply(BrawlerState state, uint currentTick)
        {
            state.AddIncomingMovementModifier(new MovementModifier(MovementModifierType.SpeedMultiplier, 1f - _magnitude, _sourceToken));
        }

        public void Tick(BrawlerState state, uint currentTick) { }

        public void Remove(BrawlerState state, uint currentTick)
        {
            state.RemoveIncomingMovementModifiersFromSource(_sourceToken);
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Slow;
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