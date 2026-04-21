using MOBA.Core.Infrastructure;
using UnityEngine;

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

            // Route through DamageService so every pipeline step runs: incoming
            // modifiers, shields, lifesteal, DamageEventBus, combat log, and
            // — critically — the super-charge push on the burn's ignitor. Before
            // this, burn DoT bypassed DamageService entirely (a direct
            // state.TakeDamage call), so ticks never fed the attacker's
            // super-charge and deployables wouldn't despawn on fatal ticks.
            ISpatialEntity spatialTarget = null;
            if (target is BrawlerState brawlerState)
                spatialTarget = brawlerState.Owner;
            else if (target is DeployableState deployableState)
                spatialTarget = deployableState.Controller;

            if (spatialTarget == null)
                return;

            var damageService = ServiceProvider.Get<IDamageService>();
            if (damageService == null)
                return;

            damageService.ApplyDamage(new DamageContext
            {
                Attacker = _source,
                Target = spatialTarget,
                Damage = _magnitude,
                Type = DamageType.Ability,
                HitPosition = spatialTarget.Position,
                Direction = Vector3.forward,
                IsCritical = false,
                SourceAbility = null,
                IsSuper = false
            });
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