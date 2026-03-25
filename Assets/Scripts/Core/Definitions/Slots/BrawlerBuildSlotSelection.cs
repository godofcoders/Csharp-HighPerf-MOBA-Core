using System;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [Serializable]
    public struct BrawlerBuildSlotSelection
    {
        public string SlotId;
        public BrawlerBuildOptionDefinition SelectedOption;
    }
}