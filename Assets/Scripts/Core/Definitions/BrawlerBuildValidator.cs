using System.Collections.Generic;

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

                switch (slot.SlotType)
                {
                    case BrawlerBuildSlotType.Gadget:
                        {
                            if (selection.Gadget == null)
                                break;

                            if (!ContainsReference(brawler.GadgetOptions, selection.Gadget))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gadget '{selection.Gadget.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedGadgets.Contains(selection.Gadget))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gadget '{selection.Gadget.name}' cannot be equipped more than once.");

                            selectedGadgets.Add(selection.Gadget);
                            break;
                        }

                    case BrawlerBuildSlotType.StarPower:
                        {
                            if (selection.StarPower == null)
                                break;

                            if (!ContainsReference(brawler.StarPowerOptions, selection.StarPower))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Star Power '{selection.StarPower.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedStarPowers.Contains(selection.StarPower))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Star Power '{selection.StarPower.name}' cannot be equipped more than once.");

                            selectedStarPowers.Add(selection.StarPower);
                            break;
                        }

                    case BrawlerBuildSlotType.Hypercharge:
                        {
                            if (selection.Hypercharge == null)
                                break;

                            if (!ContainsReference(brawler.HyperchargeOptions, selection.Hypercharge))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Hypercharge '{selection.Hypercharge.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedHypercharges.Contains(selection.Hypercharge))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Hypercharge '{selection.Hypercharge.name}' cannot be equipped more than once.");

                            selectedHypercharges.Add(selection.Hypercharge);
                            break;
                        }

                    case BrawlerBuildSlotType.Gear:
                        {
                            if (selection.Gear == null)
                                break;

                            if (!ContainsReference(brawler.GearOptions, selection.Gear))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gear '{selection.Gear.name}' is not available for brawler '{brawler.name}'.");

                            if (!slot.AllowDuplicateSelectionInSameTypeGroup && selectedGears.Contains(selection.Gear))
                                return BrawlerBuildValidationResult.Invalid(
                                    $"Gear '{selection.Gear.name}' cannot be equipped more than once.");

                            selectedGears.Add(selection.Gear);
                            break;
                        }
                }
            }

            return BrawlerBuildValidationResult.Valid();
        }

        private static bool ContainsReference<T>(T[] array, T target) where T : UnityEngine.Object
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