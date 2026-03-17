using MOBA.Core.Simulation;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class AIReactiveListener
    {
        private readonly BrawlerController _self;
        private readonly AITargetInfo _targetInfo;

        public AIReactiveListener(BrawlerController self, AITargetInfo targetInfo)
        {
            _self = self;
            _targetInfo = targetInfo;

            DamageEventBus.OnDamage += OnDamage;
        }

        private void OnDamage(DamageContext ctx)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
                return;

            // If I got hit → react immediately
            if (ctx.Target == _self)
            {
                if (ctx.Attacker != null)
                {
                    _targetInfo.Remember(ctx.Attacker, ServiceProvider.Get<ISimulationClock>().CurrentTick);
                }
            }
        }

        public void Dispose()
        {
            DamageEventBus.OnDamage -= OnDamage;
        }
    }
}