using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using UnityEngine.Pool;

namespace MOBA.Core.Infrastructure
{
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance { get; private set; }

        [Header("Spawn Points")]
        [SerializeField] private GameObject _brawlerBasePrefab;
        [SerializeField] private List<Transform> _blueSpawnPoints;
        [SerializeField] private List<Transform> _redSpawnPoints;
        [SerializeField] private List<Transform> _soloSpawnPoints;
        [SerializeField] private float _respawnDelay = 5.0f;

        /// <summary>Seconds between death and respawn. Read by the death
        /// overlay HUD to drive the countdown display.</summary>
        public float RespawnDelaySeconds => _respawnDelay;
        [SerializeField] private CameraController _mainCameraController;

        /// <summary>
        /// Replace the spawn-point lists at runtime. Called by MapLoader
        /// after it instantiates a map prefab and discovers
        /// SpawnPointMarker components within it. The inspector-assigned
        /// fields stay as a fallback for direct Match-scene launches that
        /// skip MapLoader.
        /// </summary>
        public void SetSpawnPoints(System.Collections.Generic.List<Transform> blue, System.Collections.Generic.List<Transform> red)
        {
            if (blue != null) _blueSpawnPoints = blue;
            if (red != null) _redSpawnPoints = red;
        }

        public void SetSpawnPoints(
            System.Collections.Generic.List<Transform> blue,
            System.Collections.Generic.List<Transform> red,
            System.Collections.Generic.List<Transform> solo)
        {
            SetSpawnPoints(blue, red);
            if (solo != null) _soloSpawnPoints = solo;
        }

        public void SetPlayerTarget(Transform playerTransform)
        {
            if (_mainCameraController != null)
            {
                _mainCameraController.SetTarget(playerTransform);
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>When false, RequestRespawn is a no-op. Set to false by
        /// modes that suppress mid-round respawns (e.g. Knockout) and
        /// re-enabled at round transitions.</summary>
        public bool AllowAutoRespawn = true;

        public void RequestRespawn(BrawlerController brawler, TeamType team)
        {
            if (!AllowAutoRespawn) return;
            StartCoroutine(RespawnRoutine(brawler, team));
        }

        /// <summary>Force-respawn a brawler at one of their team's spawn
        /// points, bypassing the AllowAutoRespawn gate. Used by Knockout
        /// at round start.</summary>
        public void ForceRespawn(BrawlerController brawler, TeamType team)
        {
            var list = ResolveSpawnListForTeam(team);
            if (list == null || list.Count == 0 || brawler == null) return;
            Transform pt = list[0];
            brawler.gameObject.SetActive(true);
            brawler.Respawn(pt.position);
        }

        private IEnumerator RespawnRoutine(BrawlerController brawler, TeamType team)
        {
            yield return new WaitForSeconds(_respawnDelay);

            var spawnList = ResolveSpawnListForTeam(team);

            if (spawnList != null && spawnList.Count > 0)
            {
                // For now, we just pick the first point; later you can pick the safest one
                Transform spawnPoint = spawnList[0];
                brawler.Respawn(spawnPoint.position);
            }
        }

        public void PrepareMatch(List<MatchParticipant> roster)
        {
            int blueIdx = 0;
            int redIdx = 0;
            int soloIdx = 0;
            List<Transform> soloSpawnPoints = BuildSoloSpawnPointList();
            bool[] blueSpawnClaims = _blueSpawnPoints != null
                ? new bool[_blueSpawnPoints.Count]
                : new bool[0];
            bool[] redSpawnClaims = _redSpawnPoints != null
                ? new bool[_redSpawnPoints.Count]
                : new bool[0];
            bool[] soloSpawnClaims = soloSpawnPoints != null
                ? new bool[soloSpawnPoints.Count]
                : new bool[0];

            foreach (var participant in roster)
            {
                // 1. Determine spawn location
                int teamOrdinal;
                List<Transform> spawnList;
                bool[] spawnClaims;

                if (participant.Team == TeamType.Blue)
                {
                    teamOrdinal = blueIdx++;
                    spawnList = _blueSpawnPoints;
                    spawnClaims = blueSpawnClaims;
                }
                else if (participant.Team == TeamType.Red)
                {
                    teamOrdinal = redIdx++;
                    spawnList = _redSpawnPoints;
                    spawnClaims = redSpawnClaims;
                }
                else
                {
                    teamOrdinal = soloIdx++;
                    spawnList = soloSpawnPoints;
                    spawnClaims = soloSpawnClaims;
                }

                Transform spawnPoint = ResolveSpawnPoint(
                    participant,
                    spawnList,
                    spawnClaims,
                    teamOrdinal);

                if (spawnPoint == null)
                {
                    Debug.LogError($"[SpawnManager] Missing spawn point for {participant.Name} on {participant.Team}.");
                    continue;
                }

                // 2. Instantiate the Brawler Bridge
                GameObject go = Instantiate(_brawlerBasePrefab, spawnPoint.position, spawnPoint.rotation);
                BrawlerController controller = go.GetComponent<BrawlerController>();

                // 3. Inject the specific Brawler Definition
                controller.InitializeFromMatchmaking(participant.SelectedBrawler, participant.Team);

                // 4. If participant is AI, attach the Brain
                if (participant.IsAI)
                {
                    // 1. Add/Get the AI Brain
                    var aiBrain = go.GetComponent<BrawlerAIController>();
                    if (aiBrain == null) aiBrain = go.AddComponent<BrawlerAIController>();

                    // 2. INJECT the reference (Assuming you made the field public or added a setter)
                    aiBrain.SetTarget(controller);
                }
                else
                {
                    var playerSource = go.GetComponent<PlayerCommandSource>();
                    if (playerSource == null)
                        playerSource = go.AddComponent<PlayerCommandSource>();

                    controller.SetCommandSource(playerSource);

                    SetPlayerTarget(controller.PresentationFollowTarget);
                }
            }
        }

        private List<Transform> ResolveSpawnListForTeam(TeamType team)
        {
            if (team == TeamType.Blue)
                return _blueSpawnPoints;

            if (team == TeamType.Red)
                return _redSpawnPoints;

            return BuildSoloSpawnPointList();
        }

        private List<Transform> BuildSoloSpawnPointList()
        {
            if (_soloSpawnPoints != null && _soloSpawnPoints.Count > 0)
                return _soloSpawnPoints;

            List<Transform> combined = new List<Transform>(
                (_blueSpawnPoints != null ? _blueSpawnPoints.Count : 0) +
                (_redSpawnPoints != null ? _redSpawnPoints.Count : 0));

            AddSpawnPoints(combined, _blueSpawnPoints);
            AddSpawnPoints(combined, _redSpawnPoints);
            return combined;
        }

        private static void AddSpawnPoints(List<Transform> target, List<Transform> source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && !target.Contains(source[i]))
                    target.Add(source[i]);
            }
        }

        private static Transform ResolveSpawnPoint(
            MatchParticipant participant,
            List<Transform> spawnList,
            bool[] spawnClaims,
            int teamOrdinal)
        {
            if (spawnList == null || spawnList.Count == 0)
                return null;

            int preferredIndex = AITeamCompositionPlanner.GetPreferredSpawnIndex(
                participant != null ? participant.SelectedBrawler : null,
                spawnList.Count,
                teamOrdinal);

            int selectedIndex = FindNearestFreeSpawnIndex(
                spawnList,
                spawnClaims,
                preferredIndex);

            if (selectedIndex < 0)
                selectedIndex = Mathf.Clamp(preferredIndex, 0, spawnList.Count - 1);

            if (spawnClaims != null && selectedIndex < spawnClaims.Length)
                spawnClaims[selectedIndex] = true;

            return spawnList[selectedIndex];
        }

        private static int FindNearestFreeSpawnIndex(
            List<Transform> spawnList,
            bool[] spawnClaims,
            int preferredIndex)
        {
            if (spawnClaims == null || spawnClaims.Length == 0)
                return Mathf.Clamp(preferredIndex, 0, spawnList.Count - 1);

            int clampedPreferred = Mathf.Clamp(preferredIndex, 0, spawnClaims.Length - 1);
            if (!spawnClaims[clampedPreferred] && spawnList[clampedPreferred] != null)
                return clampedPreferred;

            for (int offset = 1; offset < spawnClaims.Length; offset++)
            {
                int left = clampedPreferred - offset;
                if (left >= 0 && !spawnClaims[left] && spawnList[left] != null)
                    return left;

                int right = clampedPreferred + offset;
                if (right < spawnClaims.Length && !spawnClaims[right] && spawnList[right] != null)
                    return right;
            }

            return -1;
        }
    }
}
