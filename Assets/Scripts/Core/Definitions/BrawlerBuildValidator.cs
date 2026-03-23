using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    public readonly struct BrawlerBuildValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public BrawlerBuildValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public static BrawlerBuildValidationResult Valid()
        {
            return new BrawlerBuildValidationResult(true, string.Empty);
        }

        public static BrawlerBuildValidationResult Invalid(string message)
        {
            return new BrawlerBuildValidationResult(false, message);
        }
    }

    public static class BrawlerBuildValidator
    {
        public static BrawlerBuildValidationResult Validate(
            BrawlerDefinition brawler,
            BrawlerBuildDefinition build,
            int powerLevel)
        {
            if (brawler == null)
                return BrawlerBuildValidationResult.Invalid("BrawlerDefinition is null.");

            if (build == null)
                return BrawlerBuildValidationResult.Invalid("BrawlerBuildDefinition is null.");

            if (brawler.BuildLayout == null)
                return BrawlerBuildValidationResult.Invalid($"Brawler '{brawler.name}' has no BuildLayout assigned.");

            BrawlerBuildSlotDefinition[] layoutSlots = brawler.BuildLayout.Slots;
            BrawlerBuildSlotSelection[] selections = build.Selections;

            if (layoutSlots == null || layoutSlots.Length == 0)
                return BrawlerBuildValidationResult.Valid();

            Dictionary<string, BrawlerBuildSlotDefinition> slotMap = new Dictionary<string, BrawlerBuildSlotDefinition>();
            for (int i = 0; i < layoutSlots.Length; i++)
            {
                BrawlerBuildSlotDefinition slot = layoutSlots[i];
                if (string.IsNullOrWhiteSpace(slot.SlotId))
                    continue;

                if (!slotMap.ContainsKey(slot.SlotId))
                    slotMap.Add(slot.SlotId, slot);
            }

            HashSet<BrawlerBuildOptionDefinition> selectedOptions = new HashSet<BrawlerBuildOptionDefinition>();
            HashSet<GadgetDefinition> selectedGadgets = new HashSet<GadgetDefinition>();
            HashSet<StarPowerDefinition> selectedStarPowers = new HashSet<StarPowerDefinition>();
            HashSet<HyperchargeDefinition> selectedHypercharges = new HashSet<HyperchargeDefinition>();
            HashSet<PassiveDefinition> selectedGears = new HashSet<PassiveDefinition>();

            if (selections == null)
                return BrawlerBuildValidationResult.Valid();

            for (int i = 0; i < selections.Length; i++)
            {
                BrawlerBuildSlotSelection selection = selections[i];

                if (string.IsNullOrWhiteSpace(selection.SlotId))
                    return BrawlerBuildValidationResult.Invalid("A build selection has an empty SlotId.");

                if (!slotMap.TryGetValue(selection.SlotId, out BrawlerBuildSlotDefinition slot))
                    return BrawlerBuildValidationResult.Invalid(
                        $"Selection references unknown slot id '{selection.SlotId}'.");

                if (powerLevel < slot.UnlockPowerLevel)
                    return BrawlerBuildValidationResult.Invalid(
                        $"Slot '{slot.DisplayName}' is locked until power level {slot.UnlockPowerLevel}.");

                BrawlerBuildOptionDefinition selected = selection.SelectedOption;
                if (selected == null)
                    continue;

                if (!selected.CanEquipInBuildSlot(slot.SlotType))
                    return BrawlerBuildValidationResult.Invalid(
                        $"Option '{selected.name}' cannot be equipped in slot '{slot.DisplayName}'.");

                switch (slot.SlotType)
                {
                    case BrawlerBuildSlotType.Gadget:
                        {
                            if (!(selected is GadgetDefinition gadget))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Option '{selected.name}' is not a GadgetDefinition for slot '{slot.DisplayName}'.");

                            if (!ContainsReference(brawler.GadgetOptions, gadget))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gadget '{gadget.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedGadgets.Contains(gadget))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gadget '{gadget.name}' cannot be equipped more than once.");

                            selectedGadgets.Add(gadget);
                            break;
                        }

                    case BrawlerBuildSlotType.StarPower:
                        {
                            if (!(selected is StarPowerDefinition starPower))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Option '{selected.name}' is not a StarPowerDefinition for slot '{slot.DisplayName}'.");

                            if (!ContainsReference(brawler.StarPowerOptions, starPower))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Star Power '{starPower.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedStarPowers.Contains(starPower))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Star Power '{starPower.name}' cannot be equipped more than once.");

                            selectedStarPowers.Add(starPower);
                            break;
                        }

                    case BrawlerBuildSlotType.Hypercharge:
                        {
                            if (!(selected is HyperchargeDefinition hypercharge))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Option '{selected.name}' is not a HyperchargeDefinition for slot '{slot.DisplayName}'.");

                            if (!ContainsReference(brawler.HyperchargeOptions, hypercharge))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Hypercharge '{hypercharge.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedHypercharges.Contains(hypercharge))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Hypercharge '{hypercharge.name}' cannot be equipped more than once.");

                            selectedHypercharges.Add(hypercharge);
                            break;
                        }

                    case BrawlerBuildSlotType.Gear:
                        {
                            if (!(selected is PassiveDefinition gear))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Option '{selected.name}' is not a PassiveDefinition for slot '{slot.DisplayName}'.");

                            if (!ContainsReference(brawler.GearOptions, gear))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gear '{gear.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedGears.Contains(gear))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gear '{gear.name}' cannot be equipped more than once.");

                            selectedGears.Add(gear);
                            break;
                        }
                }

                if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedOptions.Contains(selected))
                {
                    return BrawlerBuildValidationResult.Invalid(
                        $"Option '{selected.name}' cannot be equipped more than once.");
                }

                selectedOptions.Add(selected);
            }

            return BrawlerBuildValidationResult.Valid();
        }

        private static bool ContainsReference<T>(T[] array, T target) where T : Object
        {
            if (array == null || target == null)
                return false;

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == target)
                    return true;
            }

            return false;
        }
    }
}