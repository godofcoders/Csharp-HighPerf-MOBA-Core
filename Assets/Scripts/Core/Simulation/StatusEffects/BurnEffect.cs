using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class BurnEffect : IStatusEffectInstance
    {
        private readonly float _damagePerTick;
        private readonly BrawlerController _source;
        private uint _nextTick;

        public StatusEffectType Type => StatusEffectType.Burn;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        public BurnEffect(BrawlerController source, float dps, float durationSeconds, uint currentTick)
        {
            _source = source;
            _damagePerTick = dps / 2f; // tick every 0.5 sec
            StartTick = currentTick;
            EndTick = currentTick + SecondsToTicks(durationSeconds);
            _nextTick = currentTick + 15;
        }

        public void Apply(BrawlerState state, uint currentTick) { }

        public void Tick(BrawlerState state, uint currentTick)
        {
            if (currentTick < _nextTick)
                return;

            _nextTick = currentTick + 15;

            var damageService = ServiceProvider.Get<IDamageService>();
            if (damageService == null || state.Owner == null)
                return;

            damageService.ApplyDamage(new DamageContext
            {
                Attacker = _source,
                Target = state.Owner,
                Damage = _damagePerTick,
                Type = DamageType.Ability,
                HitPosition = state.Owner.Position,
                Direction = default,
                SourceAbility = null,
                IsSuper = false
            });
        }

        public void Remove(BrawlerState state, uint currentTick) { }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Burn;
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