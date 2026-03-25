using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct AbilityExecutionEvent
    {
        public AbilityEventType EventType;
        public BrawlerController Source;
        public AbilityDefinition AbilityDefinition;
        public AbilitySlotType SlotType;

        public Vector3 Origin;
        public Vector3 Direction;
        public uint Tick;

        public AbilityExecutionResult Result;
    }
}