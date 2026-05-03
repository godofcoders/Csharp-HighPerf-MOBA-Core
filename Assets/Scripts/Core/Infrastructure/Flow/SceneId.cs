namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Logical scene identifiers. Decouples gameplay code from the scene
    /// asset names — SceneFlow maps each id to a build-settings index /
    /// scene name. If you rename a scene file, only the mapping in
    /// SceneFlow needs to change.
    /// </summary>
    public enum SceneId
    {
        Loading = 0,
        MainMenu = 1,
        BrawlerSelect = 2,
        GameModeSelect = 3,
        Match = 4,
        Results = 5
    }
}
