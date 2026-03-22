using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class GadgetDefinition : AbilityDefinition
    {
        [Header("Gadget")]
        public int MaxCharges = 3;

        protected virtual void OnValidate()
        {
            SlotType = AbilitySlotType.Gadget;
            AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.Gadget };
        }
    }
}