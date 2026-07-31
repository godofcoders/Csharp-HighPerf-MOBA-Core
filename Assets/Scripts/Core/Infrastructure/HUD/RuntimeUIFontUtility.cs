using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class RuntimeUIFontUtility
    {
        private const string BuiltInRuntimeFontName = "LegacyRuntime.ttf";

        private static Font _runtimeFont;

        public static Font GetDefaultFont()
        {
            if (_runtimeFont != null)
                return _runtimeFont;

            _runtimeFont = Resources.GetBuiltinResource<Font>(BuiltInRuntimeFontName);
            if (_runtimeFont == null)
                Debug.LogWarning($"Unable to load Unity built-in runtime font '{BuiltInRuntimeFontName}'.");

            return _runtimeFont;
        }
    }
}
