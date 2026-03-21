using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public struct AbilityExecutionResult
    {
        public bool Success;
        public AbilityDefinition AbilityDefinition;
        public AbilitySlotType SlotType;

        public bool SpawnedProjectile;
        public int ProjectileCount;

        public bool AppliedAreaEffect;
        public int TargetsAffected;

        public bool ConsumedResource;
        public bool TriggeredStatus;

        public static AbilityExecutionResult Failed(AbilityDefinition definition, AbilitySlotType slotType)
        {
            return new AbilityExecutionResult
            {
                Success = false,
                AbilityDefinition = definition,
                SlotType = slotType,
                SpawnedProjectile = false,
                ProjectileCount = 0,
                AppliedAreaEffect = false,
                TargetsAffected = 0,
                ConsumedResource = false,
                TriggeredStatus = false
            };
        }

        public static AbilityExecutionResult Succeeded(AbilityDefinition definition, AbilitySlotType slotType)
        {
            return new AbilityExecutionResult
            {
                Success = true,
                AbilityDefinition = definition,
                SlotType = slotType,
                SpawnedProjectile = false,
                ProjectileCount = 0,
                AppliedAreaEffect = false,
                TargetsAffected = 0,
                ConsumedResource = false,
                TriggeredStatus = false
            };
        }
    }
}