using MOBA.Core.Definitions;

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

        public static void Reset()
        {
            SelectedBrawler = null;
            SelectedMode = GameModeId.GemGrab;
        }
    }

    /// <summary>Identifies a game mode. Phase 1 has only Gem Grab; the
    /// enum is here so future modes (Knockout, Heist, etc.) can append
    /// without rewiring scene flow.</summary>
    public enum GameModeId
    {
        GemGrab = 0,
    }
}
