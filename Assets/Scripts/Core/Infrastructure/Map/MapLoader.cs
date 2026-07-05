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
            BushPatchPresentation.InstallUnder(SpawnedMapInstance);

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
            List<Transform> solo = new List<Transform>(8);

            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker m = markers[i];
                if (m == null) continue;
                if (m.Team == TeamType.Blue) blue.Add(m.transform);
                else if (m.Team == TeamType.Red) red.Add(m.transform);
                else if (TeamRelationshipUtility.IsSoloTeam(m.Team)) solo.Add(m.transform);
            }

            bool hasTeamSpawns = blue.Count > 0 && red.Count > 0;
            bool hasSoloSpawns = solo.Count > 0;
            if (!hasTeamSpawns &&
                (!hasSoloSpawns || SceneSelection.SelectedMode != GameModeId.SoloShowdown))
            {
                Debug.LogWarning($"[MapLoader] Map '{ResolveLoadedMapName()}' has {blue.Count} Blue + {red.Count} Red + {solo.Count} Solo SpawnPointMarkers. Team modes need Blue/Red markers; Solo Showdown can use Solo markers. SpawnManager will fall back to inspector-assigned points.");
                return;
            }

            bool swappedTeamOrientation = NormalizeTeamSpawnOrientation(blue, red);

            sm.SetSpawnPoints(blue, red, solo);
            Debug.Log($"[MapLoader] Map '{ResolveLoadedMapName()}' loaded with {blue.Count} Blue + {red.Count} Red + {solo.Count} Solo spawn points. Orientation={(swappedTeamOrientation ? "normalized" : "authored")}");
        }

        public static bool NormalizeTeamSpawnOrientation(
            List<Transform> blue,
            List<Transform> red)
        {
            bool swapped = false;

            if (blue != null &&
                red != null &&
                blue.Count > 0 &&
                red.Count > 0 &&
                GetAverageZ(blue) > GetAverageZ(red))
            {
                SwapListContents(blue, red);
                swapped = true;
            }

            SortByHorizontalLane(blue);
            SortByHorizontalLane(red);
            return swapped;
        }

        private static void SwapListContents(
            List<Transform> first,
            List<Transform> second)
        {
            List<Transform> temp = new List<Transform>(first);
            first.Clear();
            first.AddRange(second);
            second.Clear();
            second.AddRange(temp);
        }

        private static float GetAverageZ(List<Transform> points)
        {
            if (points == null || points.Count == 0)
                return 0f;

            float total = 0f;
            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[i];
                if (point == null)
                    continue;

                total += point.position.z;
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private static void SortByHorizontalLane(List<Transform> points)
        {
            if (points == null || points.Count <= 1)
                return;

            points.Sort(CompareSpawnPointLane);
        }

        private static int CompareSpawnPointLane(Transform a, Transform b)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            int x = a.position.x.CompareTo(b.position.x);
            if (x != 0)
                return x;

            return a.position.z.CompareTo(b.position.z);
        }

        private string ResolveLoadedMapName()
        {
            if (SpawnedMapInstance != null)
                return SpawnedMapInstance.name;

            if (_mapPrefab != null)
                return _mapPrefab.name;

            return "SelectedMap";
        }
    }
}
