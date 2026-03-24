using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BrawlerBuildLayout", menuName = "MOBA/Builds/Brawler Build Layout")]
    public class BrawlerBuildLayoutDefinition : ScriptableObject
    {
        public BrawlerBuildSlotDefinition[] Slots;

        public int CountSlots(BrawlerBuildSlotType slotType)
        {
            if (Slots == null)
                return 0;

            int count = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].SlotType == slotType)
                    count++;
            }

            return count;
        }

        public bool HasSlotId(string slotId)
        {
            if (Slots == null || string.IsNullOrWhiteSpace(slotId))
                return false;

            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].SlotId == slotId)
                    return true;
            }

            return false;
        }
    }
}