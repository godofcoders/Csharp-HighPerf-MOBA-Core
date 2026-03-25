using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct CombatPresentationEvent
    {
        public CombatPresentationEventType EventType;

        public BrawlerController Source;
        public BrawlerController Target;

        public AbilityDefinition AbilityDefinition;
        public AbilitySlotType SlotType;

        public Vector3 Position;
        public Vector3 Direction;

        public float Value;
        public bool IsSuper;
    }
}