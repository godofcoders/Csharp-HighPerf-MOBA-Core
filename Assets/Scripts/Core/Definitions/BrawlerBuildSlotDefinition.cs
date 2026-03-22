using System;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [Serializable]
    public struct BrawlerBuildSlotDefinition
    {
        public string SlotId;
        public string DisplayName;
        public BrawlerBuildSlotType SlotType;
        public int UnlockPowerLevel;
        public bool AllowDuplicateSelectionInSameTypeGroup;
    }
}