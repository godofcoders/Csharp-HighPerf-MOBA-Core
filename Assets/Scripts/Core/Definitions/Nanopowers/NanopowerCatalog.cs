using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    public static class NanopowerCatalog
    {
        private static NanopowerDefinition[] _coltDefaults;

        public static void BuildOptions(BrawlerDefinition brawler, List<NanopowerDefinition> output)
        {
            if (output == null)
                return;

            output.Clear();

            if (brawler == null)
                return;

            if (brawler.NanopowerOptions != null)
            {
                for (int i = 0; i < brawler.NanopowerOptions.Length; i++)
                {
                    NanopowerDefinition option = brawler.NanopowerOptions[i];
                    if (option != null && !output.Contains(option))
                        output.Add(option);
                }
            }

            if (output.Count == 0 && IsNamedBrawler(brawler, "colt"))
            {
                NanopowerDefinition[] defaults = GetColtDefaults();
                for (int i = 0; i < defaults.Length; i++)
                    output.Add(defaults[i]);
            }
        }

        private static NanopowerDefinition[] GetColtDefaults()
        {
            if (_coltDefaults != null)
                return _coltDefaults;

            _coltDefaults = new[]
            {
                CreateRuntimeNanopower(
                    "ColtNanoHighCaliber",
                    "High-Caliber Core",
                    "+12% damage. Colt's bullets hit harder for the whole match.",
                    new Color(1f, 0.58f, 0.16f, 1f),
                    outgoingDamageBonusPercent: 0.12f),

                CreateRuntimeNanopower(
                    "ColtNanoQuickdraw",
                    "Quickdraw Boots",
                    "+9% movement speed. Better strafing, chasing, and lane pressure.",
                    new Color(0.16f, 0.76f, 1f, 1f),
                    moveSpeedBonusPercent: 0.09f),

                CreateRuntimeNanopower(
                    "ColtNanoKevlar",
                    "Kevlar Plating",
                    "+350 health and 6% damage reduction. More room to trade safely.",
                    new Color(0.64f, 0.82f, 0.26f, 1f),
                    bonusMaxHealth: 350f,
                    incomingDamageReductionPercent: 0.06f)
            };

            return _coltDefaults;
        }

        private static NanopowerDefinition CreateRuntimeNanopower(
            string assetName,
            string displayName,
            string description,
            Color accentColor,
            float outgoingDamageBonusPercent = 0f,
            float damageStatBonusPercent = 0f,
            float moveSpeedBonusPercent = 0f,
            float bonusMaxHealth = 0f,
            float incomingDamageReductionPercent = 0f)
        {
            NanopowerDefinition definition = ScriptableObject.CreateInstance<NanopowerDefinition>();
            definition.name = assetName;
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.OptionName = displayName;
            definition.PassiveName = displayName;
            definition.Description = description;
            definition.Category = PassiveCategory.MatchModifier;
            definition.AccentColor = accentColor;
            definition.OutgoingDamageBonusPercent = outgoingDamageBonusPercent;
            definition.DamageStatBonusPercent = damageStatBonusPercent;
            definition.MoveSpeedBonusPercent = moveSpeedBonusPercent;
            definition.BonusMaxHealth = bonusMaxHealth;
            definition.IncomingDamageReductionPercent = incomingDamageReductionPercent;
            return definition;
        }

        private static bool IsNamedBrawler(BrawlerDefinition brawler, string expected)
        {
            if (brawler == null || string.IsNullOrEmpty(expected))
                return false;

            string lowerExpected = expected.ToLowerInvariant();
            if (!string.IsNullOrEmpty(brawler.BrawlerName) &&
                brawler.BrawlerName.ToLowerInvariant().Contains(lowerExpected))
            {
                return true;
            }

            return !string.IsNullOrEmpty(brawler.name) &&
                   brawler.name.ToLowerInvariant().Contains(lowerExpected);
        }
    }
}
