using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct AbilityExecutionContext
    {
        public BrawlerController Source;
        public AbilityDefinition AbilityDefinition;
        public AbilitySlotType SlotType;

        public Vector3 Origin;
        public Vector3 Direction;
        public uint StartTick;

        public bool IsSuper;
        public bool IsGadget;

        public Vector3 TargetPoint;
        public bool HasTargetPoint;
    }
}