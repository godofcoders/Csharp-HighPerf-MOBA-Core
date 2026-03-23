using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class GearDefinition : PassiveDefinition
    {
        protected virtual void OnValidate()
        {
            Category = PassiveCategory.Gear;
            AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.Gear };
        }
    }
}