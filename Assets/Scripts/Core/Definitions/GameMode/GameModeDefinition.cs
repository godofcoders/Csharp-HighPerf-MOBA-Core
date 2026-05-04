using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Authoring-time data for one playable game mode (Gem Grab, Knockout,
    /// Heist, …). The runtime prefab carries everything mode-specific:
    /// the mode coordinator (e.g. GemGrabMode), spawners, win-condition
    /// triggers, mode-specific HUD widgets — anything that wouldn't make
    /// sense outside this mode.
    ///
    /// One asset per mode under Assets/Scriptables/GameModes/. Add to
    /// GameModeCatalog so the loader can resolve a SceneSelection.SelectedMode
    /// pick into the actual prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "GameMode", menuName = "MOBA/Game Modes/Game Mode")]
    public class GameModeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Logical id. Must match a GameModeId enum value to be picked up by SceneSelection.")]
        public GameModeId Id;

        [Tooltip("Designer-facing display name (shown on the GameModeSelect screen).")]
        public string DisplayName;

        [Tooltip("Optional UI icon for the mode-select card.")]
        public Sprite Icon;

        [Header("Runtime")]
        [Tooltip("Prefab instantiated under GameModeLoader's slot at match start. Should contain the mode coordinator + any spawners + mode-specific HUD widgets.")]
        public GameObject ModePrefab;
    }
}
