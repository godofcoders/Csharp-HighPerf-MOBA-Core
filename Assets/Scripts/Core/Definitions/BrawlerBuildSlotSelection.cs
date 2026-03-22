using System;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [Serializable]
    public struct BrawlerBuildSlotSelection
    {
        public string SlotId;

        public GadgetDefinition Gadget;
        public StarPowerDefinition StarPower;
        public HyperchargeDefinition Hypercharge;
        public PassiveDefinition Gear;
    }
}