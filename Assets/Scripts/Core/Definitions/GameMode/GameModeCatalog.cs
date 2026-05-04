using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Single asset listing every available GameModeDefinition. The
    /// GameModeLoader resolves SceneSelection.SelectedMode against this
    /// catalog at match start. The GameModeSelectScreen can also drive
    /// its UI from this catalog so adding a mode is one-asset-edit
    /// (add new GameModeDefinition + reference in catalog), no code.
    /// </summary>
    [CreateAssetMenu(fileName = "GameModeCatalog", menuName = "MOBA/Game Modes/Game Mode Catalog")]
    public class GameModeCatalog : ScriptableObject
    {
        [Tooltip("All available game modes. Phase 1 has Gem Grab; append future modes here.")]
        public GameModeDefinition[] Modes;

        /// <summary>Returns the GameModeDefinition matching the supplied
        /// id, or null if no entry has that id.</summary>
        public GameModeDefinition Find(GameModeId id)
        {
            if (Modes == null) return null;
            for (int i = 0; i < Modes.Length; i++)
            {
                if (Modes[i] != null && Modes[i].Id == id)
                    return Modes[i];
            }
            return null;
        }
    }
}
