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
            var list = team == TeamType.Blue ? _blueSpawnPoints : _redSpawnPoints;
            if (list == null || list.Count == 0 || brawler == null) return;
            Transform pt = list[0];
            brawler.gameObject.SetActive(true);
            brawler.Respawn(pt.position);
        }

        private IEnumerator RespawnRoutine(BrawlerController brawler, TeamType team)
        {
            yield return new WaitForSeconds(_respawnDelay);

            // FIX: Use the lists we defined for matchmaking
            var spawnList = (team == TeamType.Blue) ? _blueSpawnPoints : _redSpawnPoints;

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

            foreach (var participant in roster)
            {
                // 1. Determine spawn location
                Transform spawnPoint = (participant.Team == TeamType.Blue)
                    ? _blueSpawnPoints[blueIdx++]
                    : _redSpawnPoints[redIdx++];

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
    }
}