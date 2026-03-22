using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class BrawlerBuildOptionDefinition : ScriptableObject
    {
        [Header("Build Option Identity")]
        public string OptionName;
        [TextArea] public string Description;

        [Header("Build Slot Rules")]
        public BrawlerBuildSlotType[] AllowedBuildSlotTypes;

        public virtual bool CanEquipInBuildSlot(BrawlerBuildSlotType slotType)
        {
            if (slotType == BrawlerBuildSlotType.None)
                return false;

            if (AllowedBuildSlotTypes == null || AllowedBuildSlotTypes.Length == 0)
                return false;

            for (int i = 0; i < AllowedBuildSlotTypes.Length; i++)
            {
                if (AllowedBuildSlotTypes[i] == slotType)
                    return true;
            }

            return false;
        }
    }
}