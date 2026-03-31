using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct AimAssistResult
    {
        public bool HasResult;
        public BrawlerController Target;
        public Vector3 AimDirection;
        public Vector3 AimPoint;
    }
}