using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Tag component placed on empty GameObjects inside a map prefab to
    /// mark them as spawn points and identify which team they belong to.
    /// MapLoader walks the spawned map's children for these markers and
    /// hands the resulting Transform lists to SpawnManager.
    ///
    /// Setup:
    ///   - Inside your map prefab, create empty GameObjects at each spawn
    ///     position.
    ///   - Drop SpawnPointMarker on each; set the Team enum.
    ///   - Recommended: name them "Spawn_Blue_1", "Spawn_Red_2" etc. for
    ///     clarity in the hierarchy. The marker's Team is what counts;
    ///     names are cosmetic.
    /// </summary>
    public class SpawnPointMarker : MonoBehaviour
    {
        [Tooltip("Which team this spawn point belongs to. Brawlers of this team will respawn here.")]
        public TeamType Team = TeamType.Blue;
    }
}
