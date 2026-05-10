using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Match-scene MonoBehaviour that instantiates a map prefab into a
    /// slot, discovers SpawnPointMarkers within it, and hands them to
    /// SpawnManager. Same architectural pattern as GameModeLoader, applied
    /// to the level geometry side.
    ///
    /// Phase 1 takes a single inspector-assigned _mapPrefab. Phase 3+
    /// (when there are multiple maps) can swap this for a MapCatalog
    /// lookup against SceneSelection.SelectedMap, mirroring how
    /// GameModeLoader resolves modes.
    ///
    /// Execution order matters: MapLoader.Awake must run BEFORE
    /// MatchmakingManager.Start (which calls SpawnManager.PrepareMatch
    /// using whatever spawn points SpawnManager currently holds).
    /// Awake-vs-Start gives us this for free — Awake runs on every
    /// component before any Start runs.
    ///
    /// Setup:
    ///   1. In the Match scene, add an empty GameObject "MapRoot" — this
    ///      will be the slot the map spawns under.
    ///   2. Add this component to a GameObject (could be MapRoot itself,
    ///      could be a sibling).
    ///   3. Assign _mapPrefab = your map prefab (e.g. Map_DefaultMap).
    ///   4. Assign _mapRoot = MapRoot (or leave null to spawn under self).
    ///   5. Inside the map prefab, place empty GameObjects with
    ///      SpawnPointMarker components for each spawn position.
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        [Header("Map")]
        [Tooltip("Inspector fallback map prefab. Used only when SceneSelection.SelectedMap is null (e.g. direct Match-scene launches without going through map-select).")]
        [SerializeField] private GameObject _mapPrefab;

        [Tooltip("Slot under which the spawned map will be parented. If null, this GameObject is used.")]
        [SerializeField] private Transform _mapRoot;

        public GameObject SpawnedMapInstance { get; private set; }

        private void Awake()
        {
            // Prefer the player's selected map (set by MapSelect UI). Fall
            // back to inspector _mapPrefab for direct Match launches.
            GameObject prefab = SceneSelection.SelectedMap != null && SceneSelection.SelectedMap.MapPrefab != null
                ? SceneSelection.SelectedMap.MapPrefab
                : _mapPrefab;

            if (prefab == null)
            {
                Debug.LogError("[MapLoader] No map prefab available (SceneSelection.SelectedMap and _mapPrefab both null). SpawnManager will use its inspector fallback spawn points.");
                return;
            }

            Transform parent = _mapRoot != null ? _mapRoot : transform;
            SpawnedMapInstance = Instantiate(prefab, parent);
            SpawnedMapInstance.name = prefab.name;

            HandSpawnPointsToSpawnManager();
        }

        private void HandSpawnPointsToSpawnManager()
        {
            SpawnManager sm = SpawnManager.Instance;
            if (sm == null)
            {
                Debug.LogWarning("[MapLoader] SpawnManager.Instance is null at MapLoader.Awake — spawn points won't be applied. Check execution order or add SpawnManager to the scene.");
                return;
            }

            // Walk the spawned map's children for SpawnPointMarkers and
            // bucket by team. GetComponentsInChildren includes inactive
            // by default-false; we want only active markers so we keep it
            // strict.
            SpawnPointMarker[] markers = SpawnedMapInstance.GetComponentsInChildren<SpawnPointMarker>(false);

            List<Transform> blue = new List<Transform>(8);
            List<Transform> red = new List<Transform>(8);

            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker m = markers[i];
                if (m == null) continue;
                if (m.Team == TeamType.Blue) blue.Add(m.transform);
                else if (m.Team == TeamType.Red) red.Add(m.transform);
            }

            if (blue.Count == 0 || red.Count == 0)
            {
                Debug.LogWarning($"[MapLoader] Map '{_mapPrefab.name}' has {blue.Count} Blue + {red.Count} Red SpawnPointMarkers. Both teams need at least one. SpawnManager will fall back to inspector-assigned points.");
                return;
            }

            sm.SetSpawnPoints(blue, red);
            Debug.Log($"[MapLoader] Map '{_mapPrefab.name}' loaded with {blue.Count} Blue + {red.Count} Red spawn points.");
        }
    }
}
