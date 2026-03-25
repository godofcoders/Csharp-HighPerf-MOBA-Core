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
        [SerializeField] private CameraController _mainCameraController;

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

        public void RequestRespawn(BrawlerController brawler, TeamType team)
        {
            StartCoroutine(RespawnRoutine(brawler, team));
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
                    var mobileInput = FindObjectOfType<MobileInputBridge>();
                    if (mobileInput != null)
                        mobileInput.SetTarget(controller);

                    // camera follow fix
                    SetPlayerTarget(controller.transform);
                }
            }
        }
    }
}