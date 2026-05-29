namespace MOBA.Core.Simulation.AI
{
    public static class AITuningRuntimeOverrides
    {
        private static AITuningModifierSet _modifiers;
        private static bool _hasModifierOverrides;
        private static int _version;

        public static int Version => _version;
        public static bool HasModifierOverrides => _hasModifierOverrides;
        public static AITuningCatalog CatalogOverride { get; private set; }

        public static void Set(
            AITuningCatalog catalogOverride,
            bool hasModifierOverrides,
            AITuningModifierSet modifiers)
        {
            CatalogOverride = catalogOverride;
            _hasModifierOverrides = hasModifierOverrides;
            _modifiers = modifiers;
            _version++;
        }

        public static void Clear()
        {
            CatalogOverride = null;
            _hasModifierOverrides = false;
            _modifiers = default;
            _version++;
        }

        public static void ApplyTo(BrawlerAIProfile profile)
        {
            if (!_hasModifierOverrides || profile == null)
                return;

            _modifiers.ApplyTo(profile);
        }

        public static string GetDebugSummary()
        {
            if (!_hasModifierOverrides && CatalogOverride == null)
                return "RuntimeTuning=None";

            string catalog = CatalogOverride != null
                ? CatalogOverride.name
                : "None";
            string modifiers = _hasModifierOverrides
                ? _modifiers.GetDebugSummary("Runtime")
                : "Runtime=None";

            return $"RuntimeTuning catalog={catalog} {modifiers}";
        }

        public static void ResetForTests()
        {
            CatalogOverride = null;
            _hasModifierOverrides = false;
            _modifiers = default;
            _version = 0;
        }
    }
}
