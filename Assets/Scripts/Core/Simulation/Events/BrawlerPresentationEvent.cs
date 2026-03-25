using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct BrawlerPresentationEvent
    {
        public BrawlerPresentationEventType EventType;
        public BrawlerController Source;
        public AbilityDefinition AbilityDefinition;
        public Vector3 Position;
        public Vector3 Direction;
        public float Value;
        public uint Tick;
    }
}