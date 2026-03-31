using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public struct AimAssistRequest
    {
        public BrawlerController Source;
        public AbilityDefinition AbilityDefinition;

        public AimAssistMode Mode;

        public Vector3 Origin;
        public Vector3 Forward;
        public float Range;

        public bool IncludeSelf;
        public bool RequireAlive;
    }
}