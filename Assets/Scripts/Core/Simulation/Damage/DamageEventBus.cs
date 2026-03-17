using System;

namespace MOBA.Core.Simulation
{
    public static class DamageEventBus
    {
        public static event Action<DamageContext> OnDamage;

        public static void RaiseDamage(DamageContext ctx)
        {
            OnDamage?.Invoke(ctx);
        }
    }
}