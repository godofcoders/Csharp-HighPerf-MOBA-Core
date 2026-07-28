using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public sealed class QuestDefinition
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly string Category;
        public readonly QuestMetricType Metric;
        public readonly int TargetValue;
        public readonly string BrawlerKey;
        public readonly GameModeId RequiredMode;
        public readonly bool HasRequiredMode;

        public QuestDefinition(
            string id,
            string title,
            string description,
            string category,
            QuestMetricType metric,
            int targetValue,
            string brawlerKey = "",
            bool hasRequiredMode = false,
            GameModeId requiredMode = GameModeId.GemGrab)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "quest" : id;
            Title = string.IsNullOrWhiteSpace(title) ? "Quest" : title;
            Description = description ?? string.Empty;
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            Metric = metric;
            TargetValue = targetValue > 0 ? targetValue : 1;
            BrawlerKey = brawlerKey ?? string.Empty;
            HasRequiredMode = hasRequiredMode;
            RequiredMode = requiredMode;
        }

        public bool RequiresBrawler => !string.IsNullOrWhiteSpace(BrawlerKey);

        public bool MatchesBrawler(BrawlerDefinition definition, string displayName)
        {
            if (!RequiresBrawler)
                return true;

            string required = Normalize(BrawlerKey);
            if (string.IsNullOrWhiteSpace(required))
                return true;

            string assetName = Normalize(definition != null ? definition.name : string.Empty);
            string brawlerName = Normalize(displayName);
            return assetName == required ||
                   brawlerName == required ||
                   assetName.Contains(required) ||
                   brawlerName.Contains(required);
        }

        public bool MatchesMode(GameModeId mode)
        {
            return !HasRequiredMode || RequiredMode == mode;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.ToLowerInvariant();
            char[] buffer = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    buffer[count++] = c;
            }

            return count > 0 ? new string(buffer, 0, count) : string.Empty;
        }
    }
}
