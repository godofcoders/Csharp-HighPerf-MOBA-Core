using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    public static class NanopowerCatalog
    {
        private static NanopowerDefinition[] _coltDefaults;
        private static NanopowerDefinition[] _jessieDefaults;
        private static NanopowerDefinition[] _byronDefaults;

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
            else if (output.Count == 0 &&
                     (IsNamedBrawler(brawler, "jessie") || IsNamedBrawler(brawler, "jesse")))
            {
                NanopowerDefinition[] defaults = GetJessieDefaults();
                for (int i = 0; i < defaults.Length; i++)
                    output.Add(defaults[i]);
            }
            else if (output.Count == 0 && IsNamedBrawler(brawler, "byron"))
            {
                NanopowerDefinition[] defaults = GetByronDefaults();
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

        private static NanopowerDefinition[] GetJessieDefaults()
        {
            if (_jessieDefaults != null)
                return _jessieDefaults;

            _jessieDefaults = new[]
            {
                CreateRuntimeNanopower(
                    "JessieNanoShockCoil",
                    "Shock Coil",
                    "+10% damage. Jessie gets stronger lane pressure when she finds bounce angles.",
                    new Color(1f, 0.73f, 0.20f, 1f),
                    outgoingDamageBonusPercent: 0.10f),

                CreateRuntimeNanopower(
                    "JessieNanoCircuitSkates",
                    "Circuit Skates",
                    "+8% movement speed. Quicker repositioning for safe poke and turret setup.",
                    new Color(0.20f, 0.82f, 1f, 1f),
                    moveSpeedBonusPercent: 0.08f),

                CreateRuntimeNanopower(
                    "JessieNanoScrapShield",
                    "Scrap Shield",
                    "+320 health and 5% damage reduction. Better survivability while controlling space.",
                    new Color(0.58f, 0.90f, 0.30f, 1f),
                    bonusMaxHealth: 320f,
                    incomingDamageReductionPercent: 0.05f)
            };

            return _jessieDefaults;
        }

        private static NanopowerDefinition[] GetByronDefaults()
        {
            if (_byronDefaults != null)
                return _byronDefaults;

            _byronDefaults = new[]
            {
                CreateRuntimeNanopower(
                    "ByronNanoPotentDose",
                    "Potent Dose",
                    "+11% damage. Byron's pressure becomes more punishing when enemies ignore his lane.",
                    new Color(0.70f, 0.34f, 1f, 1f),
                    outgoingDamageBonusPercent: 0.11f),

                CreateRuntimeNanopower(
                    "ByronNanoSwiftSerum",
                    "Swift Serum",
                    "+8% movement speed. Faster rotations for healing angles, kiting, and cleanup.",
                    new Color(0.18f, 0.95f, 0.82f, 1f),
                    moveSpeedBonusPercent: 0.08f),

                CreateRuntimeNanopower(
                    "ByronNanoTriageCoat",
                    "Triage Coat",
                    "+300 health and 7% damage reduction. Keeps Byron alive while supporting from mid range.",
                    new Color(0.48f, 0.94f, 0.36f, 1f),
                    bonusMaxHealth: 300f,
                    incomingDamageReductionPercent: 0.07f)
            };

            return _byronDefaults;
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
