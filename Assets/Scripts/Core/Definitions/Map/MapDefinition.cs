using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Authoring-time data for one playable map. Pairs a designer-friendly
    /// label + icon with the runtime map prefab that MapLoader instantiates
    /// in the Match scene.
    ///
    /// SupportedModes lets the same map be wired into multiple modes (e.g.
    /// "Skullcreek" can host both GemGrab and Knockout) and lets the
    /// mode-select UI filter the list of maps shown.
    /// </summary>
    [CreateAssetMenu(fileName = "Map", menuName = "MOBA/Maps/Map")]
    public class MapDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName;
        [Tooltip("Optional preview / thumbnail sprite for the map-select UI.")]
        public Sprite Icon;

        [Header("Runtime")]
        [Tooltip("Map prefab instantiated by MapLoader. Should contain ground geometry, walls, lighting, and SpawnPointMarker children.")]
        public GameObject MapPrefab;

        [Header("Modes")]
        [Tooltip("Which game modes this map supports. The map-select UI filters by this list.")]
        public GameModeId[] SupportedModes;

        [Header("Match Modifiers")]
        [Tooltip("Game modes where this map offers match-start nanopower selection. Leave empty for a classic start.")]
        public GameModeId[] NanopowerEnabledModes;

        public bool SupportsMode(GameModeId id)
        {
            if (SupportedModes == null) return false;
            for (int i = 0; i < SupportedModes.Length; i++)
                if (SupportedModes[i] == id) return true;
            return false;
        }

        public bool EnablesNanopowersForMode(GameModeId id)
        {
            if (!SupportsMode(id) || NanopowerEnabledModes == null)
                return false;

            for (int i = 0; i < NanopowerEnabledModes.Length; i++)
                if (NanopowerEnabledModes[i] == id) return true;

            return false;
        }
    }
}
