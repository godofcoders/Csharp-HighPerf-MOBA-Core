using System.Collections.Generic;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Per-brawler player progression. Backed by PlayerPrefs so upgrades
    /// survive app restarts while keeping the current lightweight API.
    ///
    /// Default level for any unknown brawler is 1.
    /// </summary>
    public static class PlayerBrawlerProgress
    {
        private static readonly Dictionary<BrawlerDefinition, int> _levels =
            new Dictionary<BrawlerDefinition, int>(8);
        private static readonly Dictionary<string, string> _loadoutSelections =
            new Dictionary<string, string>(16);
        private const string LevelKeyPrefix = "MOBA.BrawlerPowerLevel.";
        private const string LoadoutKeyPrefix = "MOBA.BrawlerLoadout.";

        public const int MinLevel = 1;
        public const int MaxLevel = 11;

        public static int GetLevel(BrawlerDefinition def)
        {
            if (def == null) return MinLevel;

            if (_levels.TryGetValue(def, out int lvl))
                return lvl;

            int loaded = PlayerPrefs.GetInt(BuildLevelKey(def), MinLevel);
            loaded = ClampLevel(loaded);
            _levels[def] = loaded;
            return loaded;
        }

        public static void SetLevel(BrawlerDefinition def, int level)
        {
            if (def == null) return;
            level = ClampLevel(level);
            _levels[def] = level;
            PlayerPrefs.SetInt(BuildLevelKey(def), level);
            PlayerPrefs.Save();
        }

        /// <summary>Convenience: bump a brawler's level by 1, capped at MaxLevel.</summary>
        public static void IncrementLevel(BrawlerDefinition def)
        {
            SetLevel(def, GetLevel(def) + 1);
        }

        public static bool CanUpgrade(BrawlerDefinition def)
        {
            return def != null && GetLevel(def) < MaxLevel;
        }

        public static int Upgrade(BrawlerDefinition def)
        {
            if (def == null)
                return MinLevel;

            int next = ClampLevel(GetLevel(def) + 1);
            SetLevel(def, next);
            return next;
        }

        public static string GetSelectedLoadoutOptionId(
            BrawlerDefinition def,
            string slotId)
        {
            if (def == null || string.IsNullOrWhiteSpace(slotId))
                return string.Empty;

            string key = BuildLoadoutKey(def, slotId);
            if (_loadoutSelections.TryGetValue(key, out string cached))
                return cached;

            string loaded = PlayerPrefs.GetString(key, string.Empty);
            _loadoutSelections[key] = loaded;
            return loaded;
        }

        public static void SetSelectedLoadoutOption(
            BrawlerDefinition def,
            string slotId,
            BrawlerBuildOptionDefinition option)
        {
            if (def == null || string.IsNullOrWhiteSpace(slotId))
                return;

            string key = BuildLoadoutKey(def, slotId);
            string optionId = BuildOptionPersistenceId(option);

            if (string.IsNullOrWhiteSpace(optionId))
            {
                _loadoutSelections.Remove(key);
                PlayerPrefs.DeleteKey(key);
            }
            else
            {
                _loadoutSelections[key] = optionId;
                PlayerPrefs.SetString(key, optionId);
            }

            PlayerPrefs.Save();
        }

        public static void ClearSelectedLoadoutOption(
            BrawlerDefinition def,
            string slotId)
        {
            SetSelectedLoadoutOption(def, slotId, null);
        }

        public static string BuildOptionPersistenceId(BrawlerBuildOptionDefinition option)
        {
            if (option == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(option.name)
                ? option.name
                : option.OptionName;
        }

        private static int ClampLevel(int level)
        {
            if (level < MinLevel) return MinLevel;
            if (level > MaxLevel) return MaxLevel;
            return level;
        }

        private static string BuildLevelKey(BrawlerDefinition def)
        {
            string id = def != null && !string.IsNullOrWhiteSpace(def.name)
                ? def.name
                : "Unknown";
            return LevelKeyPrefix + id;
        }

        private static string BuildLoadoutKey(BrawlerDefinition def, string slotId)
        {
            string brawlerId = def != null && !string.IsNullOrWhiteSpace(def.name)
                ? def.name
                : "Unknown";
            return $"{LoadoutKeyPrefix}{brawlerId}.{slotId}";
        }
    }
}
