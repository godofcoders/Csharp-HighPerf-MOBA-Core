using System;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [Serializable]
    public struct PassiveLoadoutSlotEntry
    {
        public PassiveSlotType SlotType;
        public PassiveDefinition Passive;
    }
}