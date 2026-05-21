using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static MatchmakingManager Instance { get; private set; }

        [Header("Player Settings")]
        [SerializeField] private BrawlerDefinition _playerBrawler; // Assign your brawler here!

        [Header("Match Settings")]
        [SerializeField] private int _teamSize = 3;
        [SerializeField] private BrawlerDefinition _defaultBotBrawler;
        [SerializeField] private BrawlerDefinition[] _botBrawlerPool;
        [SerializeField] private bool _avoidDuplicateBotBrawlersUntilPoolExhausted = true;

        private List<MatchParticipant> _roster = new List<MatchParticipant>();
        private readonly List<BrawlerDefinition> _botPickBag = new List<BrawlerDefinition>(8);

        public bool IsLobbyFull => _roster.Count >= _teamSize * 2;

        private void Awake() => Instance = this;

        private void Start()
        {
            // Prefer the brawler picked on the BrawlerSelect screen
            // (carried via SceneSelection static). Falls back to the
            // inspector-assigned _playerBrawler so launching the Match
            // scene directly (skipping the menu flow) still works for
            // test play.
            BrawlerDefinition brawler = SceneSelection.SelectedBrawler ?? _playerBrawler;
            if (brawler != null)
            {
                JoinLocalPlayer(brawler);
            }
            else
            {
                Debug.LogError("[Lobby] No Player Brawler available (neither SceneSelection nor inspector fallback set).");
            }
        }

        public void JoinLocalPlayer(BrawlerDefinition selected)
        {
            _roster.Add(new MatchParticipant("Player (You)", TeamType.Blue, selected, false));
            Debug.Log($"[Lobby] Player joined as {selected.BrawlerName}");
            
            FillWithBots();
        }

        private void FillWithBots()
        {
            int totalSlots = _teamSize * 2;
            List<BrawlerDefinition> botPool = BuildBotPool();

            if (botPool.Count == 0)
            {
                Debug.LogError("[Lobby] Cannot fill bot roster: no bot brawlers are assigned.");
                return;
            }

            while (_roster.Count < totalSlots)
            {
                // Fill Team Blue first, then Team Red
                TeamType team = (_roster.Count < _teamSize) ? TeamType.Blue : TeamType.Red;
                BrawlerDefinition botBrawler = PickBotBrawler(botPool);
                string botName = $"Bot {_roster.Count} ({botBrawler.BrawlerName})";
                _roster.Add(new MatchParticipant(botName, team, botBrawler, true));
            }

            Debug.Log("[Lobby] Roster full. Initializing Spawn Sequence...");
            StartMatch();
        }

        private List<BrawlerDefinition> BuildBotPool()
        {
            List<BrawlerDefinition> pool = new List<BrawlerDefinition>(8);

            AddUniqueBot(pool, _defaultBotBrawler);

            if (_botBrawlerPool != null)
            {
                for (int i = 0; i < _botBrawlerPool.Length; i++)
                {
                    AddUniqueBot(pool, _botBrawlerPool[i]);
                }
            }

            return pool;
        }

        private static void AddUniqueBot(List<BrawlerDefinition> pool, BrawlerDefinition candidate)
        {
            if (candidate == null || pool.Contains(candidate))
                return;

            pool.Add(candidate);
        }

        private BrawlerDefinition PickBotBrawler(List<BrawlerDefinition> pool)
        {
            if (pool.Count == 1 || !_avoidDuplicateBotBrawlersUntilPoolExhausted)
            {
                return pool[UnityEngine.Random.Range(0, pool.Count)];
            }

            if (_botPickBag.Count == 0)
            {
                RefillBotPickBag(pool);
            }

            int index = _botPickBag.Count - 1;
            BrawlerDefinition picked = _botPickBag[index];
            _botPickBag.RemoveAt(index);
            return picked;
        }

        private void RefillBotPickBag(List<BrawlerDefinition> pool)
        {
            _botPickBag.Clear();

            for (int i = 0; i < pool.Count; i++)
            {
                _botPickBag.Add(pool[i]);
            }

            for (int i = _botPickBag.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                BrawlerDefinition temp = _botPickBag[i];
                _botPickBag[i] = _botPickBag[swapIndex];
                _botPickBag[swapIndex] = temp;
            }
        }

        private void StartMatch()
        {
            SpawnManager.Instance.PrepareMatch(_roster);
            MatchManager.Instance.StartMatchFlow();
        }
    }
}
