using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Single asset listing every available MapDefinition. MapLoader
    /// resolves SceneSelection.SelectedMap against this catalog at match
    /// start. The map-select UI uses GetMapsForMode() to populate per-mode
    /// carousels.
    /// </summary>
    [CreateAssetMenu(fileName = "MapCatalog", menuName = "MOBA/Maps/Map Catalog")]
    public class MapCatalog : ScriptableObject
    {
        [Tooltip("All available maps. Append future entries here.")]
        public MapDefinition[] Maps;

        public MapDefinition Find(string displayName)
        {
            if (Maps == null || string.IsNullOrEmpty(displayName)) return null;
            for (int i = 0; i < Maps.Length; i++)
                if (Maps[i] != null && Maps[i].DisplayName == displayName) return Maps[i];
            return null;
        }

        /// <summary>Returns all maps that declare support for the given
        /// mode. Allocates — call from UI code, not per-frame.</summary>
        public List<MapDefinition> GetMapsForMode(GameModeId mode)
        {
            List<MapDefinition> result = new List<MapDefinition>(8);
            if (Maps == null) return result;
            for (int i = 0; i < Maps.Length; i++)
                if (Maps[i] != null && Maps[i].SupportsMode(mode)) result.Add(Maps[i]);
            return result;
        }
    }
}
