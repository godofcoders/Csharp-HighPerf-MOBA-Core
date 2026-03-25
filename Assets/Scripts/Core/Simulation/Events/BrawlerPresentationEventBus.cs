using System;

namespace MOBA.Core.Simulation
{
    public static class BrawlerPresentationEventBus
    {
        public static event Action<BrawlerPresentationEvent> OnEvent;

        public static void Raise(BrawlerPresentationEvent evt)
        {
            OnEvent?.Invoke(evt);
        }

        public static void Clear()
        {
            OnEvent = null;
        }
    }
}