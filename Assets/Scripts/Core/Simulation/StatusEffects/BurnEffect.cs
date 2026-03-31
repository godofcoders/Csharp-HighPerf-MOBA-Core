using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class BurnEffect : IStatusEffectInstance
    {
        public StatusEffectType Type => StatusEffectType.Burn;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        private readonly BrawlerController _source;
        private readonly float _magnitude;
        private readonly float _durationSeconds;
        private uint _nextTickDamageTick;

        public BurnEffect(BrawlerController source, float magnitude, float durationSeconds, uint currentTick)
        {
            _source = source;
            _magnitude = magnitude;
            _durationSeconds = durationSeconds;

            StartTick = currentTick;
            EndTick = currentTick + (uint)(durationSeconds * 30f);
            _nextTickDamageTick = currentTick;
        }

        public void Apply(IStatusTarget target, uint currentTick)
        {
        }

        public void Tick(IStatusTarget target, uint currentTick)
        {
            if (currentTick < _nextTickDamageTick)
                return;

            _nextTickDamageTick = currentTick + 30;

            if (target is BrawlerState brawlerState)
            {
                brawlerState.TakeDamage(_magnitude);
            }
            else if (target is DeployableState deployableState)
            {
                deployableState.TakeDamage(_magnitude);
            }
        }

        public void Remove(IStatusTarget target, uint currentTick)
        {
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Burn;
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