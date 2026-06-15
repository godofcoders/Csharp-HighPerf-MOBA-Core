using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class SoloShowdownMode : MonoBehaviour,
        IAIGameModeMacroStateProvider,
        IAIRuntimeObjectiveProvider
    {
        public static SoloShowdownMode Instance { get; private set; }

        [Header("Match Rules")]
        [SerializeField, Min(0f)] private float _endMatchDelaySeconds = 1.25f;
        [SerializeField] private bool _autoDiscoverContestants = true;

        [Header("AI Runtime Objective")]
        [SerializeField, Min(0f)] private float _safeZoneObjectiveWeight = 75f;

        private readonly List<BrawlerController> _contestants =
            new List<BrawlerController>(TeamRelationshipUtility.MaxSoloTeams);
        private readonly Dictionary<int, int> _placementsByEntityId =
            new Dictionary<int, int>(TeamRelationshipUtility.MaxSoloTeams);

        private int _nextPlacement = 1;
        private bool _matchEnding;

        public GameModeId ModeId => GameModeId.SoloShowdown;
        public int RegisteredCount => _contestants.Count;
        public int AliveCount => CountAlive();
        public TeamType WinningTeam { get; private set; } = TeamType.Neutral;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (Instance == this)
            {
                ServiceProvider.Register<IAIGameModeMacroStateProvider>(this);
                ServiceProvider.Register<IAIRuntimeObjectiveProvider>(this);
            }
        }

        private void Start()
        {
            if (SpawnManager.Instance != null)
                SpawnManager.Instance.AllowAutoRespawn = false;

            if (_autoDiscoverContestants)
                DiscoverContestants();
        }

        private void Update()
        {
            if (_autoDiscoverContestants && Time.frameCount % 30 == 0)
                DiscoverContestants();

            if (!_matchEnding &&
                MatchManager.Instance != null &&
                MatchManager.Instance.CurrentState == MatchState.Active)
            {
                CheckEndCondition();
            }
        }

        private void OnDisable()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (SpawnManager.Instance != null)
                SpawnManager.Instance.AllowAutoRespawn = true;

            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        public void RegisterBrawler(BrawlerController brawler)
        {
            if (brawler == null ||
                !TeamRelationshipUtility.IsSoloTeam(brawler.Team) ||
                _contestants.Contains(brawler))
            {
                return;
            }

            _contestants.Add(brawler);
            _nextPlacement = Mathf.Max(_nextPlacement, _contestants.Count);

            if (brawler.State != null)
            {
                BrawlerController captured = brawler;
                captured.State.OnDeath += () => HandleDeath(captured);
            }
        }

        public int GetPlacement(TeamType team)
        {
            BrawlerController contestant = FindContestant(team);
            if (contestant == null)
                return 0;

            return _placementsByEntityId.TryGetValue(contestant.EntityID, out int placement)
                ? placement
                : 0;
        }

        public int GetAliveOpponentCount(TeamType team)
        {
            int count = 0;
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (!IsAliveContestant(contestant) ||
                    !TeamRelationshipUtility.AreEnemies(contestant.Team, team))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        public bool IsTeamAlive(TeamType team)
        {
            BrawlerController contestant = FindContestant(team);
            return IsAliveContestant(contestant);
        }

        public bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state)
        {
            state = AIGameModeMacroState.Neutral;
            if (!TeamRelationshipUtility.IsSoloTeam(team))
                return false;

            BrawlerController self = FindContestant(team);
            int ownAlive = IsAliveContestant(self) ? 1 : 0;
            int aliveOpponents = GetAliveOpponentCount(team);
            int totalAlive = ownAlive + aliveOpponents;
            bool outsideSafeZone = false;
            float distanceBeyondSafeZone = 0f;

            if (self != null && SoloShowdownPoisonZone.Instance != null)
            {
                outsideSafeZone = !SoloShowdownPoisonZone.Instance.IsInsideSafeZone(self.Position);
                distanceBeyondSafeZone =
                    SoloShowdownPoisonZone.Instance.GetDistanceBeyondSafeZone(self.Position);
            }

            state = AIGameModeMacroStrategy.ResolveSoloShowdown(
                ownAlive,
                aliveOpponents,
                totalAlive,
                outsideSafeZone,
                distanceBeyondSafeZone,
                matchTimeRemainingSeconds: 0f);
            return true;
        }

        public bool TryGetRuntimeObjective(
            TeamType team,
            AIObjectiveType preferredType,
            Vector3 selfPosition,
            out AIObjectiveCandidate objective)
        {
            objective = default;
            if (!TeamRelationshipUtility.IsSoloTeam(team) ||
                SoloShowdownPoisonZone.Instance == null)
            {
                return false;
            }

            SoloShowdownPoisonZone zone = SoloShowdownPoisonZone.Instance;
            bool outsideSafeZone = !zone.IsInsideSafeZone(selfPosition);
            float weight = outsideSafeZone
                ? _safeZoneObjectiveWeight + 30f
                : _safeZoneObjectiveWeight;

            objective = new AIObjectiveCandidate(
                AIObjectiveType.SafeDefense,
                zone.Center,
                weight,
                Mathf.Max(2f, zone.CurrentSafeRadius),
                "Showdown Safe Zone",
                true,
                AIObjectiveControlState.Neutral,
                friendlyPresence: 0,
                enemyPresence: GetAliveOpponentCount(team));
            return true;
        }

        private void DiscoverContestants()
        {
            BrawlerController[] discovered = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < discovered.Length; i++)
                RegisterBrawler(discovered[i]);
        }

        private void HandleDeath(BrawlerController dying)
        {
            if (dying == null)
                return;

            if (!_placementsByEntityId.ContainsKey(dying.EntityID))
                _placementsByEntityId[dying.EntityID] = Mathf.Max(1, _nextPlacement--);

            if (MatchManager.Instance == null ||
                MatchManager.Instance.CurrentState != MatchState.Active)
            {
                return;
            }

            CheckEndCondition();
        }

        private void CheckEndCondition()
        {
            if (_matchEnding || _contestants.Count < 2)
                return;

            int alive = 0;
            BrawlerController winner = null;
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (!IsAliveContestant(contestant))
                    continue;

                alive++;
                winner = contestant;
            }

            if (alive > 1)
                return;

            _matchEnding = true;
            WinningTeam = winner != null ? winner.Team : TeamType.Neutral;
            if (winner != null && !_placementsByEntityId.ContainsKey(winner.EntityID))
                _placementsByEntityId[winner.EntityID] = 1;

            if (_endMatchDelaySeconds > 0f)
                Invoke(nameof(EndMatchNow), _endMatchDelaySeconds);
            else
                EndMatchNow();
        }

        private void EndMatchNow()
        {
            MatchManager.Instance?.EndMatch(WinningTeam);
        }

        private int CountAlive()
        {
            int count = 0;
            for (int i = 0; i < _contestants.Count; i++)
            {
                if (IsAliveContestant(_contestants[i]))
                    count++;
            }

            return count;
        }

        private BrawlerController FindContestant(TeamType team)
        {
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null && contestant.Team == team)
                    return contestant;
            }

            return null;
        }

        private static bool IsAliveContestant(BrawlerController contestant)
        {
            return SpatialEntityUtility.IsAlive(contestant) &&
                   contestant.State != null &&
                   !contestant.State.IsDead;
        }
    }
}
