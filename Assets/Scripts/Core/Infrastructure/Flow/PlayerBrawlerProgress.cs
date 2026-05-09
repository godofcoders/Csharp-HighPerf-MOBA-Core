using System.Collections.Generic;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Per-brawler player progression. Phase 1: in-memory only (resets on
    /// app launch). Phase 4 will swap the backing store for PlayerPrefs /
    /// JSON save with the same API.
    ///
    /// Default level for any unknown brawler is 1.
    /// </summary>
    public static class PlayerBrawlerProgress
    {
        private static readonly Dictionary<BrawlerDefinition, int> _levels =
            new Dictionary<BrawlerDefinition, int>(8);

        public const int MinLevel = 1;
        public const int MaxLevel = 11;

        public static int GetLevel(BrawlerDefinition def)
        {
            if (def == null) return MinLevel;
            return _levels.TryGetValue(def, out int lvl) ? lvl : MinLevel;
        }

        public static void SetLevel(BrawlerDefinition def, int level)
        {
            if (def == null) return;
            if (level < MinLevel) level = MinLevel;
            if (level > MaxLevel) level = MaxLevel;
            _levels[def] = level;
        }

        /// <summary>Convenience: bump a brawler's level by 1, capped at MaxLevel.</summary>
        public static void IncrementLevel(BrawlerDefinition def)
        {
            SetLevel(def, GetLevel(def) + 1);
        }
    }
}
