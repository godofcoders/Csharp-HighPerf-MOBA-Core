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

        [Tooltip("Legacy single passive slot. Kept for compatibility.")]
        public StarPowerDefinition StarPower;

        [Tooltip("Default persistent passive loadout for this brawler.")]
        public StarPowerDefinition[] DefaultStarPowerLoadout;

        public HyperchargeDefinition Hypercharge;

        [Header("AI")]
        public BrawlerAIProfile AIProfile;

        [Header("AI Role")]
        public BrawlerArchetype Archetype;

        [Header("Combat Role Tuning")]
        public float Aggression = 1f;
        public float SurvivalInstinct = 1f;
        public float TeamplayWeight = 1f;

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

        public List<StarPowerDefinition> BuildDefaultStarPowerLoadout()
        {
            List<StarPowerDefinition> result = new List<StarPowerDefinition>(4);

            if (StarPower != null)
                result.Add(StarPower);

            if (DefaultStarPowerLoadout == null)
                return result;

            for (int i = 0; i < DefaultStarPowerLoadout.Length; i++)
            {
                StarPowerDefinition entry = DefaultStarPowerLoadout[i];

                if (entry == null)
                    continue;

                if (!result.Contains(entry))
                    result.Add(entry);
            }

            return result;
        }
    }
}