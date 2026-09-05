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

            if (TryResolveKnownPortraitKey(definition.name, out string key))
                return key;

            if (TryResolveKnownPortraitKey(definition.BrawlerName, out key))
                return key;

            string displayName = !string.IsNullOrWhiteSpace(definition.BrawlerName)
                ? definition.BrawlerName
                : definition.name;

            return Normalize(displayName);
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

        private static bool TryResolveKnownPortraitKey(string value, out string key)
        {
            key = string.Empty;
            string normalized = Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return false;

            if (normalized.StartsWith("elprimo") || normalized == "primo")
            {
                key = "el_primo";
                return true;
            }

            if (normalized.StartsWith("jessie") || normalized.StartsWith("jesse"))
            {
                key = "jessie";
                return true;
            }

            if (normalized.StartsWith("colt"))
            {
                key = "colt";
                return true;
            }

            if (normalized.StartsWith("byron"))
            {
                key = "byron";
                return true;
            }

            if (normalized.StartsWith("barley"))
            {
                key = "barley";
                return true;
            }

            if (normalized == "bo" || normalized.StartsWith("bodefinition"))
            {
                key = "bo";
                return true;
            }

            if (normalized.StartsWith("piper"))
            {
                key = "piper";
                return true;
            }

            if (normalized.StartsWith("leon"))
            {
                key = "leon";
                return true;
            }

            return false;
        }
    }
}
