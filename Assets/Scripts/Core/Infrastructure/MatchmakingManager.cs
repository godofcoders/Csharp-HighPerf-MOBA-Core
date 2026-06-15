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
        [SerializeField] private int _soloShowdownContestantCount = 8;
        [SerializeField] private BrawlerDefinition _defaultBotBrawler;
        [SerializeField] private BrawlerDefinition[] _botBrawlerPool;
        [SerializeField] private bool _useCompositionAwareBotSelection = true;
        [SerializeField] private bool _avoidDuplicateBotBrawlersUntilPoolExhausted = true;
        [SerializeField] private int _maxSameBotBrawlerPerTeam = 1;
        [SerializeField] private int _maxSameArchetypePerTeam = 1;
        [SerializeField] private bool _logBotCompositionDraft = false;

        private List<MatchParticipant> _roster = new List<MatchParticipant>();
        private readonly List<BrawlerDefinition> _botPickBag = new List<BrawlerDefinition>(8);

        public bool IsLobbyFull => _roster.Count >= GetTargetRosterSize();

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
            _roster.Clear();
            _botPickBag.Clear();

            TeamType playerTeam = SceneSelection.SelectedMode == GameModeId.SoloShowdown
                ? TeamType.Solo1
                : TeamType.Blue;

            _roster.Add(new MatchParticipant("Player (You)", playerTeam, selected, false));
            Debug.Log($"[Lobby] Player joined as {selected.BrawlerName}");
            
            FillWithBots();
        }

        private void FillWithBots()
        {
            int totalSlots = GetTargetRosterSize();
            List<BrawlerDefinition> botPool = BuildBotPool();

            if (botPool.Count == 0)
            {
                Debug.LogError("[Lobby] Cannot fill bot roster: no bot brawlers are assigned.");
                return;
            }

            while (_roster.Count < totalSlots)
            {
                TeamType team = ResolveParticipantTeam(_roster.Count);
                BrawlerDefinition botBrawler;
                string pickReason;
                if (SceneSelection.SelectedMode == GameModeId.SoloShowdown)
                    botBrawler = PickSoloBotBrawler(botPool, out pickReason);
                else
                    botBrawler = PickBotBrawler(botPool, team, out pickReason);

                string botName = $"Bot {_roster.Count} ({botBrawler.BrawlerName})";
                _roster.Add(new MatchParticipant(botName, team, botBrawler, true));

                if (_logBotCompositionDraft)
                {
                    Debug.Log(
                        $"[LobbyDraft] team={team} " +
                        $"mode={SceneSelection.SelectedMode} " +
                        $"pick={botBrawler.BrawlerName} " +
                        $"role={botBrawler.Archetype} " +
                        $"reason={pickReason}");
                }
            }

            Debug.Log("[Lobby] Roster full. Initializing Spawn Sequence...");
            StartMatch();
        }

        private int GetTargetRosterSize()
        {
            if (SceneSelection.SelectedMode == GameModeId.SoloShowdown)
            {
                return Mathf.Clamp(
                    _soloShowdownContestantCount,
                    2,
                    TeamRelationshipUtility.MaxSoloTeams);
            }

            return Mathf.Max(1, _teamSize) * 2;
        }

        private TeamType ResolveParticipantTeam(int rosterIndex)
        {
            if (SceneSelection.SelectedMode == GameModeId.SoloShowdown)
                return TeamRelationshipUtility.GetSoloTeam(rosterIndex);

            return rosterIndex < _teamSize ? TeamType.Blue : TeamType.Red;
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

        private BrawlerDefinition PickBotBrawler(
            List<BrawlerDefinition> pool,
            TeamType team,
            out string pickReason)
        {
            if (_useCompositionAwareBotSelection)
            {
                AITeamCompositionPlanner.PickOptions options = AITeamCompositionPlanner.PickOptions.Default;
                options.AvoidDuplicateBrawlersUntilPoolExhausted = _avoidDuplicateBotBrawlersUntilPoolExhausted;
                options.MaxSameBrawlerPerTeam = Mathf.Max(1, _maxSameBotBrawlerPerTeam);
                options.MaxSameArchetypePerTeam = Mathf.Max(1, _maxSameArchetypePerTeam);

                AITeamCompositionPlanner.PickResult result =
                    AITeamCompositionPlanner.PickBotBrawler(
                        pool,
                        _roster,
                        team,
                        SceneSelection.SelectedMode,
                        options);

                if (result.Brawler != null)
                {
                    pickReason = $"{result.Score:0.0}:{result.Reason}";
                    return result.Brawler;
                }
            }

            pickReason = "legacy_bag";

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

        private BrawlerDefinition PickSoloBotBrawler(
            List<BrawlerDefinition> pool,
            out string pickReason)
        {
            pickReason = "showdown_global_bag";

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
