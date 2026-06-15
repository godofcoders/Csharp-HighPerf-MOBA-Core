using MOBA.Core.Definitions;
// MapDefinition lives in MOBA.Core.Definitions too — same namespace, no extra using needed.

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Static carrier for selections that survive scene transitions:
    /// which brawler the player picked, which game mode, etc. Static so
    /// the Match scene's spawning code can read it without needing a
    /// scene-bound singleton instance to survive the transition.
    ///
    /// Cleared on returning to MainMenu (call <see cref="Reset"/> from
    /// MainMenuScreen.OnEnable).
    /// </summary>
    public static class SceneSelection
    {
        public static BrawlerDefinition SelectedBrawler;
        public static GameModeId SelectedMode = GameModeId.GemGrab;
        /// <summary>Picked on the map-select UI. MapLoader prefers this
        /// over its inspector _mapPrefab fallback when set.</summary>
        public static MapDefinition SelectedMap;

        /// <summary>True when the brawler-select screen was opened from
        /// MainMenu's preview tap (just to swap the showcased brawler).
        /// On confirm, BrawlerSelectScreen routes back to MainMenu instead
        /// of advancing to GameModeSelect, then clears this flag.</summary>
        public static bool PickerReturnsToMainMenu;

        public static void Reset()
        {
            // Note: SelectedBrawler PERSISTS across reset so MainMenu can
            // keep showing the player's last pick. Only mode is wiped (so
            // post-match flow re-asks rather than auto-rematching).
            SelectedMode = GameModeId.GemGrab;
        }
    }

    /// <summary>Identifies a game mode. Phase 1 has only Gem Grab; the
    /// enum is here so future modes (Knockout, Heist, etc.) can append
    /// without rewiring scene flow.</summary>
    public enum GameModeId
    {
        GemGrab = 0,
        Knockout = 1,
        BrawlBall = 2,
        HotZone = 3,
        SoloShowdown = 4,
    }
}
