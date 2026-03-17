using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIReactiveListener
    {
        private readonly BrawlerController _self;
        private readonly AITargetInfo _targetInfo;

        public AIReactiveListener(BrawlerController self, AITargetInfo targetInfo)
        {
            _self = self;
            _targetInfo = targetInfo;
            DamageEventBus.OnDamageApplied += OnDamageApplied;
        }

        private void OnDamageApplied(DamageResultContext result)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
                return;

            var damage = result.Damage;

            if (!ReferenceEquals(damage.Target, _self))
                return;

            if (damage.Attacker == null)
                return;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            _targetInfo.Remember(damage.Attacker, currentTick);
        }

        public void Dispose()
        {
            DamageEventBus.OnDamageApplied -= OnDamageApplied;
        }
    }
}