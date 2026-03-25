using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewPassiveLoadout", menuName = "MOBA/Loadouts/Passive Loadout")]
    public class PassiveLoadoutDefinition : ScriptableObject
    {
        public PassiveLoadoutRules Rules;
        public PassiveLoadoutSlotEntry[] Slots;

        public List<PassiveDefinition> BuildList()
        {
            List<PassiveDefinition> result = new List<PassiveDefinition>(4);

            if (Slots == null)
                return result;

            for (int i = 0; i < Slots.Length; i++)
            {
                PassiveDefinition entry = Slots[i].Passive;

                if (entry == null)
                    continue;

                if (!result.Contains(entry))
                    result.Add(entry);
            }

            return result;
        }

        public PassiveLoadoutValidationResult Validate()
        {
            if (Slots == null)
                return PassiveLoadoutValidationResult.Valid();

            Dictionary<PassiveCategory, int> counts = new Dictionary<PassiveCategory, int>();
            Dictionary<PassiveFamilyDefinition, PassiveDefinition> usedFamilies =
                new Dictionary<PassiveFamilyDefinition, PassiveDefinition>();

            List<PassiveDefinition> accepted = new List<PassiveDefinition>(Slots.Length);

            for (int i = 0; i < Slots.Length; i++)
            {
                PassiveLoadoutSlotEntry slotEntry = Slots[i];
                PassiveDefinition passive = slotEntry.Passive;

                if (passive == null)
                    continue;

                if (!passive.CanEquipInSlot(slotEntry.SlotType))
                {
                    string message =
                        $"Passive '{passive.name}' cannot be equipped in slot '{slotEntry.SlotType}'.";
                    return PassiveLoadoutValidationResult.Invalid(message, passive);
                }

                if (!counts.ContainsKey(passive.Category))
                    counts[passive.Category] = 0;

                counts[passive.Category]++;

                int limit = Rules != null ? Rules.GetLimit(passive.Category) : 99;
                if (counts[passive.Category] > limit)
                {
                    string message = $"Passive loadout exceeds limit for category {passive.Category}. Limit: {limit}.";
                    return PassiveLoadoutValidationResult.Invalid(message, passive);
                }

                if (passive.IsUniqueInFamily && passive.Family != null)
                {
                    if (usedFamilies.TryGetValue(passive.Family, out PassiveDefinition existingFamilyPassive))
                    {
                        string familyName = string.IsNullOrWhiteSpace(passive.Family.FamilyName)
                            ? passive.Family.name
                            : passive.Family.FamilyName;

                        string message =
                            $"Passive loadout contains multiple unique passives from family '{familyName}'.";

                        return PassiveLoadoutValidationResult.Invalid(message, passive, existingFamilyPassive);
                    }

                    usedFamilies[passive.Family] = passive;
                }

                for (int j = 0; j < accepted.Count; j++)
                {
                    PassiveDefinition existing = accepted[j];

                    bool incompatible =
                        passive.IsExplicitlyIncompatibleWith(existing) ||
                        existing.IsExplicitlyIncompatibleWith(passive);

                    if (incompatible)
                    {
                        string message =
                            $"Passive '{passive.name}' is incompatible with '{existing.name}'.";

                        return PassiveLoadoutValidationResult.Invalid(message, passive, existing);
                    }
                }

                if (!accepted.Contains(passive))
                    accepted.Add(passive);
            }

            return PassiveLoadoutValidationResult.Valid();
        }

        public List<PassiveDefinition> BuildValidatedList(bool logWarnings = true)
        {
            List<PassiveDefinition> result = new List<PassiveDefinition>(4);

            if (Slots == null)
                return result;

            Dictionary<PassiveCategory, int> counts = new Dictionary<PassiveCategory, int>();
            Dictionary<PassiveFamilyDefinition, PassiveDefinition> usedFamilies =
                new Dictionary<PassiveFamilyDefinition, PassiveDefinition>();

            for (int i = 0; i < Slots.Length; i++)
            {
                PassiveLoadoutSlotEntry slotEntry = Slots[i];
                PassiveDefinition passive = slotEntry.Passive;

                if (passive == null)
                    continue;

                if (result.Contains(passive))
                    continue;

                if (!passive.CanEquipInSlot(slotEntry.SlotType))
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning(
                            $"[PassiveLoadout] Skipping passive '{passive.name}' in loadout '{name}' because it cannot be equipped in slot '{slotEntry.SlotType}'.");
                    }

                    continue;
                }

                if (!counts.ContainsKey(passive.Category))
                    counts[passive.Category] = 0;

                int limit = Rules != null ? Rules.GetLimit(passive.Category) : 99;

                if (counts[passive.Category] >= limit)
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning(
                            $"[PassiveLoadout] Skipping passive '{passive.name}' in loadout '{name}' because category '{passive.Category}' exceeded limit {limit}.");
                    }

                    continue;
                }

                if (passive.IsUniqueInFamily && passive.Family != null)
                {
                    if (usedFamilies.TryGetValue(passive.Family, out PassiveDefinition existingFamilyPassive))
                    {
                        if (logWarnings)
                        {
                            string familyName = string.IsNullOrWhiteSpace(passive.Family.FamilyName)
                                ? passive.Family.name
                                : passive.Family.FamilyName;

                            Debug.LogWarning(
                                $"[PassiveLoadout] Skipping passive '{passive.name}' in loadout '{name}' because family '{familyName}' already contains '{existingFamilyPassive.name}'.");
                        }

                        continue;
                    }
                }

                PassiveDefinition incompatibleExisting = null;

                for (int j = 0; j < result.Count; j++)
                {
                    PassiveDefinition existing = result[j];

                    bool incompatible =
                        passive.IsExplicitlyIncompatibleWith(existing) ||
                        existing.IsExplicitlyIncompatibleWith(passive);

                    if (incompatible)
                    {
                        incompatibleExisting = existing;
                        break;
                    }
                }

                if (incompatibleExisting != null)
                {
                    if (logWarnings)
                    {
                        Debug.LogWarning(
                            $"[PassiveLoadout] Skipping passive '{passive.name}' in loadout '{name}' because it is incompatible with '{incompatibleExisting.name}'.");
                    }

                    continue;
                }

                counts[passive.Category]++;

                if (passive.IsUniqueInFamily && passive.Family != null)
                    usedFamilies[passive.Family] = passive;

                result.Add(passive);
            }

            return result;
        }
    }
}