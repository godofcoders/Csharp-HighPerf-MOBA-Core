using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class CombatFeedbackSettings
    {
        public const string EffectsVolumeKey = "CombatFeedback.EffectsVolume";
        public const float DefaultEffectsVolume = 0.75f;

        public static float EffectsVolume
        {
            get => Sanitize(PlayerPrefs.GetFloat(EffectsVolumeKey, DefaultEffectsVolume));
            set => PlayerPrefs.SetFloat(EffectsVolumeKey, Sanitize(value));
        }

        private static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? DefaultEffectsVolume : Mathf.Clamp01(value);
    }
}
