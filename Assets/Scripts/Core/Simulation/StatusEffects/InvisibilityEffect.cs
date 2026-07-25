namespace MOBA.Core.Simulation
{
    public sealed class InvisibilityEffect : IStatusEffectInstance
    {
        public StatusEffectType Type => StatusEffectType.Invisibility;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        private object _sourceToken;

        public InvisibilityEffect(float durationSeconds, object sourceToken, uint currentTick)
        {
            StartTick = currentTick;
            EndTick = currentTick + SimulationClock.SecondsToTicks(durationSeconds);
            _sourceToken = sourceToken;
        }

        public void Apply(IStatusTarget target, uint currentTick)
        {
            if (target is BrawlerState brawlerState)
                brawlerState.Stealth.GrantInvisibility(EndTick);
        }

        public void Tick(IStatusTarget target, uint currentTick)
        {
            if (target is not BrawlerState brawlerState)
                return;

            if (brawlerState.IsDead ||
                brawlerState.Stealth.IsAttackRevealed(currentTick) ||
                brawlerState.Stealth.IsDamageRevealed(currentTick) ||
                brawlerState.IsProximityRevealed)
            {
                EndTick = currentTick;
            }
        }

        public void Remove(IStatusTarget target, uint currentTick)
        {
            if (target is BrawlerState brawlerState)
                brawlerState.Stealth.ClearInvisibility();
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Invisibility;
        }

        public void Merge(StatusEffectContext context, uint currentTick)
        {
            uint durationTicks = SimulationClock.SecondsToTicks(context.Duration);
            uint newEndTick = currentTick + durationTicks;

            if (newEndTick > EndTick)
                EndTick = newEndTick;

            _sourceToken = context.SourceToken;
        }

        public bool IsExpired(uint currentTick)
        {
            return currentTick >= EndTick;
        }
    }
}
