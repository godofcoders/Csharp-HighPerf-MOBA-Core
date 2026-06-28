using System;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Narrow event surface for Brawl Ball ball-state changes. Presentation,
    /// HUD, and AI telemetry can subscribe without the mode knowing about
    /// those systems directly.
    /// </summary>
    public static class BrawlBallEventBus
    {
        public static Action<BrawlerController, Vector3> OnBallPickedUp;
        public static Action<BrawlerController> OnBallCarrierChanged;
        public static Action<BrawlerController, Vector3, Vector3, bool> OnBallKicked;
        public static Action<Vector3> OnBallDropped;
        public static Action<Vector3> OnBallReset;

        public static void RaiseBallPickedUp(BrawlerController carrier, Vector3 position)
        {
            OnBallPickedUp?.Invoke(carrier, position);
        }

        public static void RaiseCarrierChanged(BrawlerController carrier)
        {
            OnBallCarrierChanged?.Invoke(carrier);
        }

        public static void RaiseBallKicked(
            BrawlerController kicker,
            Vector3 position,
            Vector3 direction,
            bool isSuperKick)
        {
            OnBallKicked?.Invoke(kicker, position, direction, isSuperKick);
        }

        public static void RaiseBallDropped(Vector3 position)
        {
            OnBallDropped?.Invoke(position);
        }

        public static void RaiseBallReset(Vector3 position)
        {
            OnBallReset?.Invoke(position);
        }
    }
}
