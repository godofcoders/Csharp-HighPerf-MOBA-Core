using System;

namespace MOBA.Core.Simulation
{
    public static class StatusEffectEventBus
    {
        public static event Action<StatusEffectResult> OnStatusEffectApplied;
        public static event Action<StatusEffectResult> OnStatusEffectRemoved;

        public static void RaiseApplied(StatusEffectResult result)
        {
            OnStatusEffectApplied?.Invoke(result);
        }

        public static void RaiseRemoved(StatusEffectResult result)
        {
            OnStatusEffectRemoved?.Invoke(result);
        }
    }
}