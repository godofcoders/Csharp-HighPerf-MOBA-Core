using System;

namespace MOBA.Core.Simulation
{
    public static class DamageEventBus
    {
        public static event Action<DamageResultContext> OnDamageApplied;

        public static void RaiseDamageApplied(DamageResultContext ctx)
        {
            OnDamageApplied?.Invoke(ctx);
        }
    }
}