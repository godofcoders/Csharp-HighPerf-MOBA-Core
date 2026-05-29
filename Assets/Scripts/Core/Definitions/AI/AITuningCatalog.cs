using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    [CreateAssetMenu(fileName = "AITuningCatalog", menuName = "MOBA/AI/Tuning Catalog")]
    public sealed class AITuningCatalog : ScriptableObject
    {
        public const string DefaultResourcesPath = "AI/AI_TuningCatalog";

        [Header("Global")]
        public bool Enabled = true;
        public bool ApplyGlobalModifiers = true;
        public AITuningModifierSet GlobalModifiers;

        [Header("Presets")]
        public AITuningPreset DefaultPreset;
        public AITuningPreset[] Presets;

        public bool TryGetPreset(
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            out AITuningPreset preset)
        {
            preset = null;
            int bestScore = int.MinValue;

            if (DefaultPreset != null && DefaultPreset.Enabled)
            {
                preset = DefaultPreset;
                bestScore = DefaultPreset.GetMatchScore(difficulty, personality);
            }

            if (Presets == null)
                return preset != null;

            for (int i = 0; i < Presets.Length; i++)
            {
                AITuningPreset candidate = Presets[i];
                if (candidate == null)
                    continue;

                int score = candidate.GetMatchScore(difficulty, personality);
                if (score <= bestScore)
                    continue;

                preset = candidate;
                bestScore = score;
            }

            return preset != null;
        }

        public void ApplyTo(
            BrawlerAIProfile profile,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            if (!Enabled || profile == null)
                return;

            if (ApplyGlobalModifiers)
                GlobalModifiers.ApplyTo(profile);

            if (TryGetPreset(difficulty, personality, out AITuningPreset preset))
                preset.ApplyTo(profile);
        }

        public string GetDebugSummary(
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            string presetName = TryGetPreset(difficulty, personality, out AITuningPreset preset) &&
                                preset != null
                ? preset.name
                : "None";

            return $"Catalog={name} preset={presetName}";
        }
    }

    public static class AITuningCatalogProvider
    {
        private static AITuningCatalog _cachedResourcesCatalog;
        private static bool _resourcesCatalogLoaded;

        public static AITuningCatalog Resolve(AITuningCatalog explicitCatalog = null)
        {
            if (AITuningRuntimeOverrides.CatalogOverride != null)
                return AITuningRuntimeOverrides.CatalogOverride;

            if (explicitCatalog != null)
                return explicitCatalog;

            if (!_resourcesCatalogLoaded)
            {
                _cachedResourcesCatalog =
                    Resources.Load<AITuningCatalog>(AITuningCatalog.DefaultResourcesPath);
                _resourcesCatalogLoaded = true;
            }

            return _cachedResourcesCatalog;
        }

        public static void ResetForTests()
        {
            _cachedResourcesCatalog = null;
            _resourcesCatalogLoaded = false;
        }
    }
}
