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
        public PassiveDefinition[] GearOptions;

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
    }
}