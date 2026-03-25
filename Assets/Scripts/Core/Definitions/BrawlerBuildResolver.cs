using System.Collections.Generic;

namespace MOBA.Core.Definitions
{
    public static class BrawlerBuildResolver
    {
        public static bool TryResolve(
            BrawlerDefinition brawler,
            BrawlerBuildDefinition build,
            int powerLevel,
            out ResolvedBrawlerBuild resolved,
            out string error)
        {
            resolved = null;
            error = string.Empty;

            BrawlerBuildValidationResult validation = BrawlerBuildValidator.Validate(brawler, build, powerLevel);
            if (!validation.IsValid)
            {
                error = validation.Message;
                return false;
            }

            resolved = new ResolvedBrawlerBuild();

            if (build == null || build.Selections == null)
                return true;

            Dictionary<string, BrawlerBuildSlotDefinition> slotMap = BuildSlotMap(brawler);

            for (int i = 0; i < build.Selections.Length; i++)
            {
                BrawlerBuildSlotSelection selection = build.Selections[i];
                if (selection.SelectedOption == null)
                    continue;

                if (!slotMap.TryGetValue(selection.SlotId, out BrawlerBuildSlotDefinition slot))
                    continue;

                switch (slot.SlotType)
                {
                    case BrawlerBuildSlotType.Gadget:
                        {
                            if (selection.SelectedOption is GadgetDefinition gadget &&
                                !resolved.Gadgets.Contains(gadget))
                            {
                                resolved.Gadgets.Add(gadget);
                            }
                            break;
                        }

                    case BrawlerBuildSlotType.StarPower:
                    case BrawlerBuildSlotType.Gear:
                        {
                            if (selection.SelectedOption is PassiveDefinition passive &&
                                !resolved.PassiveOptions.Contains(passive))
                            {
                                resolved.PassiveOptions.Add(passive);
                            }
                            break;
                        }

                    case BrawlerBuildSlotType.Hypercharge:
                        {
                            if (selection.SelectedOption is HyperchargeDefinition hypercharge)
                            {
                                resolved.Hypercharge = hypercharge;
                            }
                            break;
                        }
                }
            }

            return true;
        }

        public static bool TryResolveUnlockedOnly(
            BrawlerDefinition brawler,
            BrawlerBuildDefinition build,
            int powerLevel,
            out ResolvedBrawlerBuild resolved,
            out string error)
        {
            resolved = null;
            error = string.Empty;

            if (brawler == null)
            {
                error = "BrawlerDefinition is null.";
                return false;
            }

            if (build == null)
            {
                resolved = new ResolvedBrawlerBuild();
                return true;
            }

            BrawlerBuildValidationResult fullValidation = BrawlerBuildValidator.Validate(brawler, build, 999);
            if (!fullValidation.IsValid)
            {
                error = fullValidation.Message;
                return false;
            }

            resolved = new ResolvedBrawlerBuild();

            if (build.Selections == null || brawler.BuildLayout == null)
                return true;

            Dictionary<string, BrawlerBuildSlotDefinition> slotMap = BuildSlotMap(brawler);
            List<BrawlerBuildSlotSelection> unlockedSelections = brawler.BuildUnlockedSelections(build, powerLevel);

            for (int i = 0; i < unlockedSelections.Count; i++)
            {
                BrawlerBuildSlotSelection selection = unlockedSelections[i];
                if (selection.SelectedOption == null)
                    continue;

                if (!slotMap.TryGetValue(selection.SlotId, out BrawlerBuildSlotDefinition slot))
                    continue;

                switch (slot.SlotType)
                {
                    case BrawlerBuildSlotType.Gadget:
                        {
                            if (selection.SelectedOption is GadgetDefinition gadget &&
                                !resolved.Gadgets.Contains(gadget))
                            {
                                resolved.Gadgets.Add(gadget);
                            }
                            break;
                        }

                    case BrawlerBuildSlotType.StarPower:
                    case BrawlerBuildSlotType.Gear:
                        {
                            if (selection.SelectedOption is PassiveDefinition passive &&
                                !resolved.PassiveOptions.Contains(passive))
                            {
                                resolved.PassiveOptions.Add(passive);
                            }
                            break;
                        }

                    case BrawlerBuildSlotType.Hypercharge:
                        {
                            if (selection.SelectedOption is HyperchargeDefinition hypercharge)
                            {
                                resolved.Hypercharge = hypercharge;
                            }
                            break;
                        }
                }
            }

            return true;
        }

        private static Dictionary<string, BrawlerBuildSlotDefinition> BuildSlotMap(BrawlerDefinition brawler)
        {
            Dictionary<string, BrawlerBuildSlotDefinition> map = new Dictionary<string, BrawlerBuildSlotDefinition>();

            if (brawler == null || brawler.BuildLayout == null || brawler.BuildLayout.Slots == null)
                return map;

            BrawlerBuildSlotDefinition[] slots = brawler.BuildLayout.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                BrawlerBuildSlotDefinition slot = slots[i];
                if (string.IsNullOrWhiteSpace(slot.SlotId))
                    continue;

                if (!map.ContainsKey(slot.SlotId))
                    map.Add(slot.SlotId, slot);
            }

            return map;
        }
    }
}