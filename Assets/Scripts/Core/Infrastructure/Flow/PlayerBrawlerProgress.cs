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
        private static readonly Dictionary<string, string> _skillTreeActiveSelections =
            new Dictionary<string, string>(8);
        private static readonly Dictionary<string, string> _skillTreeUnlockedSelections =
            new Dictionary<string, string>(8);
        private const string LevelKeyPrefix = "MOBA.BrawlerPowerLevel.";
        private const string LoadoutKeyPrefix = "MOBA.BrawlerLoadout.";
        private const string SkillTreeActiveKeyPrefix = "MOBA.BrawlerSkillTree.Active.";
        private const string SkillTreeUnlockedKeyPrefix = "MOBA.BrawlerSkillTree.Unlocked.";
        private const string SelectedBrawlerKey = "MOBA.SelectedBrawler";

        private static bool _hasLoadedSelectedBrawler;
        private static string _selectedBrawlerId;

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

        public static void SetSelectedBrawler(BrawlerDefinition def)
        {
            string id = BuildBrawlerPersistenceId(def);
            _hasLoadedSelectedBrawler = true;
            _selectedBrawlerId = id;

            if (string.IsNullOrWhiteSpace(id))
                PlayerPrefs.DeleteKey(SelectedBrawlerKey);
            else
                PlayerPrefs.SetString(SelectedBrawlerKey, id);

            PlayerPrefs.Save();
        }

        public static bool TryGetSelectedBrawler(
            IEnumerable<BrawlerDefinition> candidates,
            out BrawlerDefinition selected)
        {
            selected = null;

            string savedId = GetSelectedBrawlerId();
            if (string.IsNullOrWhiteSpace(savedId) || candidates == null)
                return false;

            foreach (BrawlerDefinition candidate in candidates)
            {
                if (!MatchesBrawlerPersistenceId(candidate, savedId))
                    continue;

                selected = candidate;
                return true;
            }

            return false;
        }

        public static BrawlerDefinition ResolveSelectedBrawler(
            IEnumerable<BrawlerDefinition> candidates,
            BrawlerDefinition fallback)
        {
            return TryGetSelectedBrawler(candidates, out BrawlerDefinition selected)
                ? selected
                : fallback;
        }

        public static bool HasSavedSelectedBrawler()
        {
            return !string.IsNullOrWhiteSpace(GetSelectedBrawlerId());
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

        public static List<string> GetActiveSkillTreeNodeIds(
            BrawlerDefinition def,
            BrawlerSkillTreeDefinition tree)
        {
            List<string> defaults = BuildDefaultSkillTreeNodeIds(tree);
            if (def == null || tree == null)
                return defaults;

            string key = BuildSkillTreeKey(SkillTreeActiveKeyPrefix, def);
            string serialized = GetCachedPlayerPref(_skillTreeActiveSelections, key);
            return string.IsNullOrWhiteSpace(serialized)
                ? defaults
                : DeserializeIds(serialized);
        }

        public static void SetActiveSkillTreeNodeIds(
            BrawlerDefinition def,
            IEnumerable<string> nodeIds)
        {
            SetSkillTreeNodeIds(def, nodeIds, SkillTreeActiveKeyPrefix, _skillTreeActiveSelections);
        }

        public static List<string> GetUnlockedSkillTreeNodeIds(
            BrawlerDefinition def,
            BrawlerSkillTreeDefinition tree)
        {
            List<string> defaults = new List<string>(BuildDefaultSkillTreeNodeIds(tree));
            if (tree?.Nodes != null)
            {
                for (int i = 0; i < tree.Nodes.Length; i++)
                {
                    BrawlerSkillTreeNodeDefinition node = tree.Nodes[i];
                    if (node != null && node.StartsUnlocked && !defaults.Contains(node.EffectiveId))
                        defaults.Add(node.EffectiveId);
                }
            }

            if (def == null || tree == null)
                return defaults;

            string key = BuildSkillTreeKey(SkillTreeUnlockedKeyPrefix, def);
            string serialized = GetCachedPlayerPref(_skillTreeUnlockedSelections, key);
            return string.IsNullOrWhiteSpace(serialized)
                ? defaults
                : DeserializeIds(serialized);
        }

        public static void SetUnlockedSkillTreeNodeIds(
            BrawlerDefinition def,
            IEnumerable<string> nodeIds)
        {
            SetSkillTreeNodeIds(def, nodeIds, SkillTreeUnlockedKeyPrefix, _skillTreeUnlockedSelections);
        }

        public static bool IsSkillTreeNodeUnlocked(
            BrawlerDefinition def,
            BrawlerSkillTreeDefinition tree,
            string nodeId)
        {
            return GetUnlockedSkillTreeNodeIds(def, tree).Contains(nodeId);
        }

        public static bool UnlockSkillTreeNode(
            BrawlerDefinition def,
            BrawlerSkillTreeDefinition tree,
            string nodeId)
        {
            if (def == null || tree == null || !tree.TryGetNode(nodeId, out BrawlerSkillTreeNodeDefinition node))
                return false;

            List<string> unlocked = GetUnlockedSkillTreeNodeIds(def, tree);
            if (unlocked.Contains(node.EffectiveId))
                return false;

            unlocked.Add(node.EffectiveId);
            SetUnlockedSkillTreeNodeIds(def, unlocked);
            return true;
        }

        public static string BuildOptionPersistenceId(BrawlerBuildOptionDefinition option)
        {
            if (option == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(option.name)
                ? option.name
                : option.OptionName;
        }

        public static string BuildBrawlerPersistenceId(BrawlerDefinition def)
        {
            if (def == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(def.name)
                ? def.name
                : def.BrawlerName;
        }

        private static string GetSelectedBrawlerId()
        {
            if (_hasLoadedSelectedBrawler)
                return _selectedBrawlerId;

            _hasLoadedSelectedBrawler = true;
            _selectedBrawlerId = PlayerPrefs.GetString(SelectedBrawlerKey, string.Empty);
            return _selectedBrawlerId;
        }

        private static bool MatchesBrawlerPersistenceId(
            BrawlerDefinition def,
            string savedId)
        {
            if (def == null || string.IsNullOrWhiteSpace(savedId))
                return false;

            return string.Equals(BuildBrawlerPersistenceId(def), savedId, System.StringComparison.Ordinal) ||
                   string.Equals(def.BrawlerName, savedId, System.StringComparison.Ordinal) ||
                   MatchesLegacyBrawlerAlias(def, savedId);
        }

        private static bool MatchesLegacyBrawlerAlias(
            BrawlerDefinition def,
            string savedId)
        {
            string legacyAlias = ResolveLegacyBrawlerAlias(def);
            if (string.IsNullOrEmpty(legacyAlias))
                return false;

            string normalizedSavedId = NormalizePersistenceText(savedId);
            if (legacyAlias == "jessie" && normalizedSavedId == "jesse")
                return true;
            if (legacyAlias == "elprimo" && normalizedSavedId == "primo")
                return true;

            return string.Equals(
                legacyAlias,
                normalizedSavedId,
                System.StringComparison.Ordinal);
        }

        private static string ResolveLegacyBrawlerAlias(BrawlerDefinition def)
        {
            if (def == null)
                return string.Empty;

            string normalizedName = NormalizePersistenceText(def.name);
            if (string.IsNullOrEmpty(normalizedName))
                return string.Empty;

            if (normalizedName.StartsWith("colt"))
                return "colt";
            if (normalizedName.StartsWith("jessie") || normalizedName.StartsWith("jesse"))
                return "jessie";
            if (normalizedName.StartsWith("byron"))
                return "byron";
            if (normalizedName.StartsWith("barley"))
                return "barley";
            if (normalizedName == "bo" || normalizedName.StartsWith("bodefinition"))
                return "bo";
            if (normalizedName.StartsWith("elprimo") || normalizedName == "primo")
                return "elprimo";
            if (normalizedName.StartsWith("piper"))
                return "piper";
            if (normalizedName.StartsWith("leon"))
                return "leon";

            return string.Empty;
        }

        private static string NormalizePersistenceText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
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

        private static List<string> BuildDefaultSkillTreeNodeIds(BrawlerSkillTreeDefinition tree)
        {
            List<string> ids = new List<string>(8);
            if (tree == null)
                return ids;

            List<BrawlerSkillTreeNodeDefinition> nodes = tree.BuildDefaultActiveNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                BrawlerSkillTreeNodeDefinition node = nodes[i];
                if (node != null && !ids.Contains(node.EffectiveId))
                    ids.Add(node.EffectiveId);
            }

            return ids;
        }

        private static void SetSkillTreeNodeIds(
            BrawlerDefinition def,
            IEnumerable<string> nodeIds,
            string keyPrefix,
            Dictionary<string, string> cache)
        {
            if (def == null)
                return;

            List<string> ids = new List<string>(8);
            if (nodeIds != null)
            {
                foreach (string nodeId in nodeIds)
                {
                    if (!string.IsNullOrWhiteSpace(nodeId) && !ids.Contains(nodeId))
                        ids.Add(nodeId);
                }
            }

            string key = BuildSkillTreeKey(keyPrefix, def);
            string serialized = string.Join("|", ids);
            cache[key] = serialized;

            if (ids.Count == 0)
                PlayerPrefs.DeleteKey(key);
            else
                PlayerPrefs.SetString(key, serialized);

            PlayerPrefs.Save();
        }

        private static string GetCachedPlayerPref(Dictionary<string, string> cache, string key)
        {
            if (cache.TryGetValue(key, out string cached))
                return cached;

            string loaded = PlayerPrefs.GetString(key, string.Empty);
            cache[key] = loaded;
            return loaded;
        }

        private static List<string> DeserializeIds(string serialized)
        {
            string[] parts = serialized.Split('|');
            List<string> ids = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]) && !ids.Contains(parts[i]))
                    ids.Add(parts[i]);
            }

            return ids;
        }

        private static string BuildSkillTreeKey(string prefix, BrawlerDefinition def)
        {
            string brawlerId = def != null && !string.IsNullOrWhiteSpace(def.name)
                ? def.name
                : "Unknown";
            return prefix + brawlerId;
        }
    }
}
