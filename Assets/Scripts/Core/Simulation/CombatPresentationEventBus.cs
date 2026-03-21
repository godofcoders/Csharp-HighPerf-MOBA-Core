using System;

namespace MOBA.Core.Simulation
{
    public static class CombatPresentationEventBus
    {
        public static event Action<CombatPresentationEvent> OnEvent;

        public static void Raise(CombatPresentationEvent evt)
        {
            OnEvent?.Invoke(evt);
        }
    }
}