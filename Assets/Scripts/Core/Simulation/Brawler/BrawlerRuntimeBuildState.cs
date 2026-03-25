using System.Collections.Generic;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public sealed class BrawlerRuntimeBuildState
    {
        public GadgetDefinition EquippedGadget { get; private set; }
        public StarPowerDefinition EquippedStarPower { get; private set; }
        public HyperchargeDefinition EquippedHypercharge { get; private set; }

        private readonly List<GearDefinition> _equippedGears = new List<GearDefinition>(2);
        public IReadOnlyList<GearDefinition> EquippedGears => _equippedGears;

        public bool IsGearSlot1Unlocked { get; private set; }
        public bool IsGearSlot2Unlocked { get; private set; }
        public bool IsGadgetSlotUnlocked { get; private set; }
        public bool IsStarPowerSlotUnlocked { get; private set; }
        public bool IsHyperchargeSlotUnlocked { get; private set; }

        public void Clear()
        {
            EquippedGadget = null;
            EquippedStarPower = null;
            EquippedHypercharge = null;
            _equippedGears.Clear();

            IsGearSlot1Unlocked = false;
            IsGearSlot2Unlocked = false;
            IsGadgetSlotUnlocked = false;
            IsStarPowerSlotUnlocked = false;
            IsHyperchargeSlotUnlocked = false;
        }

        public void SetUnlockedState(
            bool gear1,
            bool gear2,
            bool gadget,
            bool starPower,
            bool hypercharge)
        {
            IsGearSlot1Unlocked = gear1;
            IsGearSlot2Unlocked = gear2;
            IsGadgetSlotUnlocked = gadget;
            IsStarPowerSlotUnlocked = starPower;
            IsHyperchargeSlotUnlocked = hypercharge;
        }

        public void SetEquippedGadget(GadgetDefinition gadget)
        {
            EquippedGadget = gadget;
        }

        public void SetEquippedStarPower(StarPowerDefinition starPower)
        {
            EquippedStarPower = starPower;
        }

        public void SetEquippedHypercharge(HyperchargeDefinition hypercharge)
        {
            EquippedHypercharge = hypercharge;
        }

        public void SetEquippedGears(IEnumerable<GearDefinition> gears)
        {
            _equippedGears.Clear();

            if (gears == null)
                return;

            foreach (GearDefinition gear in gears)
            {
                if (gear == null)
                    continue;

                if (!_equippedGears.Contains(gear))
                    _equippedGears.Add(gear);
            }
        }
    }
}