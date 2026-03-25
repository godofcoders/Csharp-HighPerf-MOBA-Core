using System;

namespace MOBA.Core.Simulation
{
    public static class AbilityEventBus
    {
        public static event Action<AbilityExecutionEvent> OnAbilityEvent;

        public static void Raise(AbilityExecutionEvent evt)
        {
            OnAbilityEvent?.Invoke(evt);
        }
    }
}