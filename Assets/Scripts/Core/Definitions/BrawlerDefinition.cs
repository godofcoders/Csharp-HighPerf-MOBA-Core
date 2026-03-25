using System.Collections.Generic;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewBrawler", menuName = "MOBA/Brawler Definition")]
    public class BrawlerDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string BrawlerName;
        public GameObject ModelPrefab;

        [Header("Base Stats")]
        public float BaseHealth = 3000f;
        public float BaseMoveSpeed = 5.0f;
        public float BaseDamage = 500f;

        [Header("Progression")]
        public BrawlerProgressionBonus[] ProgressionBonuses;

        [Header("Abilities")]
        public AbilityDefinition MainAttack;
        public AbilityDefinition SuperAbility;

        [Header("Supplemental Systems")]
        public GadgetDefinition Gadget;

        [Tooltip("Legacy single star power slot. Kept for compatibility.")]
        public StarPowerDefinition StarPower;

        [Tooltip("Default passive loadout asset for this brawler.")]
        public PassiveLoadoutDefinition DefaultPassiveLoadout;

        [Tooltip("Optional inline passive entries. Useful during transition.")]
        public PassiveDefinition[] DefaultPassiveEntries;

        public HyperchargeDefinition Hypercharge;

        [Header("AI")]
        public BrawlerAIProfile AIProfile;

        [Header("AI Role")]
        public BrawlerArchetype Archetype;

        [Header("Combat Role Tuning")]
        public float Aggression = 1f;
        public float SurvivalInstinct = 1f;
        public float TeamplayWeight = 1f;

        [Header("Build Layout")]
        public BrawlerBuildLayoutDefinition BuildLayout;

        [Header("Build Options")]
        public GadgetDefinition[] GadgetOptions;
        public StarPowerDefinition[] StarPowerOptions;
        public HyperchargeDefinition[] HyperchargeOptions;

        [Header("Default Build")]
        public BrawlerBuildDefinition DefaultBuild;

        [Header("Shared Build Catalogs")]
        public GearCatalogDefinition SharedGearCatalog;

        [Header("Gear Options")]

        public GearDefinition[] GearOptions;
        public GearDefinition[] ExtraGearOptions;

        public List<GearDefinition> BuildAvailableGearOptions()
        {
            List<GearDefinition> result = new List<GearDefinition>(8);

            if (SharedGearCatalog != null)
            {
                List<GearDefinition> shared = SharedGearCatalog.BuildList();
                for (int i = 0; i < shared.Count; i++)
                {
                    GearDefinition gear = shared[i];
                    if (gear == null)
                        continue;

                    if (!result.Contains(gear))
                        result.Add(gear);
                }
            }

            if (GearOptions != null)
            {
                for (int i = 0; i < GearOptions.Length; i++)
                {
                    GearDefinition gear = GearOptions[i];
                    if (gear == null)
                        continue;

                    if (!result.Contains(gear))
                        result.Add(gear);
                }
            }

            if (ExtraGearOptions != null)
            {
                for (int i = 0; i < ExtraGearOptions.Length; i++)
                {
                    GearDefinition gear = ExtraGearOptions[i];
                    if (gear == null)
                        continue;

                    if (!result.Contains(gear))
                        result.Add(gear);
                }
            }

            return result;
        }

        public BrawlerProgressionBonus GetProgressionBonus(int powerLevel)
        {
            BrawlerProgressionBonus best = default;
            best.PowerLevel = 1;

            if (ProgressionBonuses == null || ProgressionBonuses.Length == 0)
                return best;

            bool foundAny = false;

            for (int i = 0; i < ProgressionBonuses.Length; i++)
            {
                var entry = ProgressionBonuses[i];

                if (entry.PowerLevel <= powerLevel)
                {
                    if (!foundAny || entry.PowerLevel > best.PowerLevel)
                    {
                        best = entry;
                        foundAny = true;
                    }
                }
            }

            return best;
        }

        public List<PassiveDefinition> BuildDefaultPassiveLoadout()
        {
            List<PassiveDefinition> result = new List<PassiveDefinition>(4);

            if (StarPower != null)
                result.Add(StarPower);

            if (DefaultPassiveLoadout != null)
            {
                PassiveLoadoutValidationResult validation = DefaultPassiveLoadout.Validate();

                if (!validation.IsValid)
                {
                    Debug.LogWarning(
                        $"[BrawlerDefinition] Loadout '{DefaultPassiveLoadout.name}' on brawler '{name}' is invalid. {validation.Message}");
                }

                List<PassiveDefinition> loadoutPassives = DefaultPassiveLoadout.BuildValidatedList(true);

                for (int i = 0; i < loadoutPassives.Count; i++)
                {
                    PassiveDefinition entry = loadoutPassives[i];

                    if (entry == null)
                        continue;

                    if (!result.Contains(entry))
                        result.Add(entry);
                }
            }

            if (DefaultPassiveEntries != null)
            {
                for (int i = 0; i < DefaultPassiveEntries.Length; i++)
                {
                    PassiveDefinition entry = DefaultPassiveEntries[i];

                    if (entry == null)
                        continue;

                    if (!result.Contains(entry))
                        result.Add(entry);
                }
            }

            return result;
        }

        private void OnValidate()
        {
            if (BuildLayout == null)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' has no BuildLayout assigned.");
                return;
            }

            int gearSlots = BuildLayout.CountSlots(BrawlerBuildSlotType.Gear);
            int gadgetSlots = BuildLayout.CountSlots(BrawlerBuildSlotType.Gadget);
            int starPowerSlots = BuildLayout.CountSlots(BrawlerBuildSlotType.StarPower);
            int hyperchargeSlots = BuildLayout.CountSlots(BrawlerBuildSlotType.Hypercharge);

            if (gearSlots != 2)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' expected 2 Gear slots but found {gearSlots}.");
            }

            if (gadgetSlots != 1)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' expected 1 Gadget slot but found {gadgetSlots}.");
            }

            if (starPowerSlots != 1)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' expected 1 Star Power slot but found {starPowerSlots}.");
            }

            if (hyperchargeSlots != 1)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' expected 1 Hypercharge slot but found {hyperchargeSlots}.");
            }

            if (GadgetOptions == null || GadgetOptions.Length == 0)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' has no GadgetOptions configured.");
            }

            if (StarPowerOptions == null || StarPowerOptions.Length == 0)
            {
                Debug.LogWarning($"[BrawlerDefinition] '{name}' has no StarPowerOptions configured.");
            }

            if (DefaultBuild != null)
            {
                BrawlerBuildValidationResult result = BrawlerBuildValidator.Validate(this, DefaultBuild, 999);
                if (!result.IsValid)
                {
                    Debug.LogWarning($"[BrawlerDefinition] DefaultBuild '{DefaultBuild.name}' is invalid for '{name}'. {result.Message}");
                }
            }
        }

        public BrawlerBuildDefinition GetUsableDefaultBuild(int powerLevel)
        {
            if (DefaultBuild == null)
                return null;

            BrawlerBuildValidationResult result = BrawlerBuildValidator.Validate(this, DefaultBuild, 999);
            if (!result.IsValid)
                return null;

            return DefaultBuild;
        }
    }
}