using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct AreaHazardSpawnRequest
    {
        public BrawlerController Owner;
        public TeamType Team;
        public AreaHazardDefinition Definition;
        public Vector3 Position;

        public AbilityDefinition SourceAbility;
        public AbilitySlotType SlotType;
        public bool IsSuper;
    }
}