namespace MOBA.Core.Simulation
{
    public sealed class SlowEffect : IStatusEffectInstance
    {
        public StatusEffectType Type => StatusEffectType.Slow;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        private readonly float _magnitude;
        private readonly float _durationSeconds;
        private readonly object _sourceToken;

        public SlowEffect(float magnitude, float durationSeconds, object sourceToken, uint currentTick)
        {
            _magnitude = magnitude;
            _durationSeconds = durationSeconds;
            _sourceToken = sourceToken;
            StartTick = currentTick;
            EndTick = currentTick + SimulationClock.SecondsToTicks(durationSeconds);
        }

        public void Apply(IStatusTarget target, uint currentTick)
        {
            MovementModifier modifier = new MovementModifier(
                MovementModifierType.SpeedMultiplier,
                _magnitude,
                _sourceToken);

            target.AddIncomingMovementModifier(modifier);
        }

        public void Tick(IStatusTarget target, uint currentTick)
        {
        }

        public void Remove(IStatusTarget target, uint currentTick)
        {
            target.RemoveIncomingMovementModifiersFromSource(_sourceToken);
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Slow;
        }

        public void Merge(StatusEffectContext context, uint currentTick)
        {
            uint durationTicks = SimulationClock.SecondsToTicks(context.Duration);
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