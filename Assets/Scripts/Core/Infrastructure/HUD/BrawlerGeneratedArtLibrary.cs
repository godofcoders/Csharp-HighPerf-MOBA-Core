using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Runtime lookup for generated menu/loading/portrait art. This keeps
    /// presentation assets optional: authored definition sprites still win,
    /// while generated art fills any gaps without requiring scene wiring.
    /// </summary>
    public static class BrawlerGeneratedArtLibrary
    {
        public const string LoadingHomeBackgroundPath = "UI/Generated/loading_home_bg";
        public const string HomeLobbyBackgroundPath = "UI/Generated/home_lobby_bg";

        public static Sprite LoadLoadingHomeBackground()
        {
            return Resources.Load<Sprite>(LoadingHomeBackgroundPath);
        }

        public static Sprite LoadHomeLobbyBackground()
        {
            return Resources.Load<Sprite>(HomeLobbyBackgroundPath);
        }

        public static Sprite LoadPortrait(BrawlerDefinition definition, bool preferDefinitionSprite = true)
        {
            if (definition == null)
                return null;

            if (preferDefinitionSprite && definition.Portrait != null)
                return definition.Portrait;

            return LoadPortraitByKey(ResolvePortraitKey(definition));
        }

        public static Sprite LoadPortraitByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            return Resources.Load<Sprite>($"UI/Generated/portraits/{key}");
        }

        public static string ResolvePortraitKey(BrawlerDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string displayName = !string.IsNullOrWhiteSpace(definition.BrawlerName)
                ? definition.BrawlerName
                : definition.name;

            string normalized = Normalize(displayName);
            if (normalized == "elprimo")
                return "el_primo";
            if (normalized == "jessie" || normalized == "jesse")
                return "jessie";
            if (normalized == "colt")
                return "colt";
            if (normalized == "byron")
                return "byron";
            if (normalized == "barley")
                return "barley";
            if (normalized == "bo")
                return "bo";
            if (normalized == "piper")
                return "piper";
            if (normalized == "leon")
                return "leon";

            return normalized;
        }

        public static string ResolveDisplayName(BrawlerDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(definition.BrawlerName)
                ? definition.BrawlerName
                : definition.name;
        }

        private static string Normalize(string value)
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
    }
}
