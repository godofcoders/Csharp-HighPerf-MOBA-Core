using UnityEngine;
using System.Collections.Generic;

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

        public bool TryGetSlot(string slotId, out BrawlerBuildSlotDefinition slot)
        {
            if (Slots != null)
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    if (Slots[i].SlotId == slotId)
                    {
                        slot = Slots[i];
                        return true;
                    }
                }
            }

            slot = default;
            return false;
        }

        public bool IsSlotUnlocked(string slotId, int powerLevel)
        {
            if (!TryGetSlot(slotId, out BrawlerBuildSlotDefinition slot))
                return false;

            return powerLevel >= slot.UnlockPowerLevel;
        }

        public List<BrawlerBuildSlotSelection> BuildUnlockedSelections(BrawlerBuildDefinition build, int powerLevel)
        {
            List<BrawlerBuildSlotSelection> result = new List<BrawlerBuildSlotSelection>(8);

            if (build == null || build.Selections == null || BuildLayout == null)
                return result;

            for (int i = 0; i < build.Selections.Length; i++)
            {
                BrawlerBuildSlotSelection selection = build.Selections[i];

                if (string.IsNullOrWhiteSpace(selection.SlotId))
                    continue;

                if (!BuildLayout.IsSlotUnlocked(selection.SlotId, powerLevel))
                    continue;

                result.Add(selection);
            }

            return result;
        }
    }
}