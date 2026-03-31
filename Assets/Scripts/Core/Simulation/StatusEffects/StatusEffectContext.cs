using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct StatusEffectContext
    {
        public BrawlerController Source;
        public IStatusTarget Target;
        public StatusEffectType Type;
        public float Duration;
        public float Magnitude;
        public Vector3 Origin;
        public object SourceToken;
    }
}