using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct SupportEffectRequest
    {
        public BrawlerController Source;
        public BrawlerController Target;
        public SupportEffectType EffectType;

        public float Magnitude;
        public float DurationSeconds;

        public object SourceToken;
        public Vector3 Origin;
    }
}