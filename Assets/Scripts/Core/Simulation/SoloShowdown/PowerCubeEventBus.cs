using System;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public static class PowerCubeEventBus
    {
        public static Action<BrawlerState, int> OnPowerCubePickedUp;
        public static Action<Vector3, int> OnPowerCubePickedUpAt;
    }
}
