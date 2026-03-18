using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct StatusEffectContext
    {
        public BrawlerController Source;
        public BrawlerController Target;

        public StatusEffectType Type;
        public float Duration;
        public float Magnitude;

        public Vector3 Origin;
        public object SourceToken;
    }
}