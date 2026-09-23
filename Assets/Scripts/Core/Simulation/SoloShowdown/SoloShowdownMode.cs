using System.Collections;
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

        [Header("Power Cubes")]
        [SerializeField] private bool _enablePowerCubeCrates = true;
        [SerializeField] private SoloShowdownPowerCubeSpawner _powerCubeSpawner;

        private readonly List<BrawlerController> _contestants =
            new List<BrawlerController>(TeamRelationshipUtility.MaxSoloTeams);
        private readonly Dictionary<TeamType, int> _placementsByTeam =
            new Dictionary<TeamType, int>(TeamRelationshipUtility.MaxSoloTeams);
        private readonly HashSet<TeamType> _eliminatedTeams =
            new HashSet<TeamType>();
        private readonly Dictionary<BrawlerController, Coroutine> _pendingDuoRespawns =
            new Dictionary<BrawlerController, Coroutine>();

        private int _nextPlacement = 1;
        private bool _matchEnding;

        public GameModeId ModeId => GameModeId.SoloShowdown;
        public int RegisteredCount => _contestants.Count;
        public int AliveCount => CountAlive();
        public int RemainingTeamCount => CountRemainingTeams();
        public bool IsDuoShowdown =>
            SceneSelection.SelectedShowdownVariant == ShowdownVariant.Duo;
        public float RespawnDelaySeconds => ShowdownRules.DuoRespawnDelaySeconds;
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

            if (_enablePowerCubeCrates)
            {
                EnsurePowerCubeSpawner();
                _powerCubeSpawner?.SpawnInitialCrates();
            }
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
            CancelAllDuoRespawns();
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
                (IsDuoShowdown && !ShowdownRules.IsDuoTeam(brawler.Team)) ||
                (IsDuoShowdown && GetRegisteredCount(brawler.Team) >= ShowdownRules.DuoPlayersPerTeam) ||
                _contestants.Contains(brawler))
            {
                return;
            }

            _contestants.Add(brawler);
            _nextPlacement = Mathf.Max(_nextPlacement, CountRegisteredTeams());

            if (brawler.State != null)
            {
                BrawlerController captured = brawler;
                captured.State.OnDeath += () => HandleDeath(captured);
            }
        }

        public int GetPlacement(TeamType team)
        {
            return _placementsByTeam.TryGetValue(team, out int placement)
                ? placement
                : 0;
        }

        public int GetRegisteredCount(TeamType team)
        {
            int count = 0;
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null && contestant.Team == team)
                    count++;
            }

            return count;
        }

        public bool IsRespawnPending(BrawlerController brawler)
        {
            return brawler != null && _pendingDuoRespawns.ContainsKey(brawler);
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
            return CountAlive(team) > 0;
        }

        public bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state)
        {
            state = AIGameModeMacroState.Neutral;
            if (!TeamRelationshipUtility.IsSoloTeam(team))
                return false;

            BrawlerController self = FindLivingContestant(team) ?? FindContestant(team);
            int ownAlive = CountAlive(team);
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
            if (!MatchStateUtility.IsCombatResolutionOpen() || _matchEnding)
                return;

            if (dying == null)
                return;

            DropPowerCubesFrom(dying);

            if (IsDuoShowdown)
                HandleDuoDeath(dying);
            else if (!_placementsByTeam.ContainsKey(dying.Team))
                _placementsByTeam[dying.Team] = Mathf.Max(1, _nextPlacement--);

            CheckEndCondition();
        }

        private void HandleDuoDeath(BrawlerController dying)
        {
            if (_eliminatedTeams.Contains(dying.Team))
                return;

            if (!ShowdownRules.IsTeamEliminated(CountAlive(dying.Team)))
            {
                QueueDuoRespawn(dying);
                return;
            }

            _eliminatedTeams.Add(dying.Team);
            CancelTeamRespawns(dying.Team);
            if (!_placementsByTeam.ContainsKey(dying.Team))
                _placementsByTeam[dying.Team] = Mathf.Max(1, _nextPlacement--);
        }

        private void QueueDuoRespawn(BrawlerController brawler)
        {
            CancelDuoRespawn(brawler);
            _pendingDuoRespawns[brawler] = StartCoroutine(DuoRespawnRoutine(brawler));
        }

        private IEnumerator DuoRespawnRoutine(BrawlerController brawler)
        {
            yield return new WaitForSeconds(ShowdownRules.DuoRespawnDelaySeconds);

            _pendingDuoRespawns.Remove(brawler);
            if (brawler == null ||
                _matchEnding ||
                _eliminatedTeams.Contains(brawler.Team) ||
                !IsTeamAlive(brawler.Team) ||
                SpawnManager.Instance == null)
            {
                yield break;
            }

            int memberIndex = GetTeamMemberIndex(brawler);
            int spawnOrdinal = ShowdownRules.GetDuoSpawnOrdinal(
                brawler.Team,
                memberIndex);
            SpawnManager.Instance.ForceRespawn(
                brawler,
                brawler.Team,
                spawnOrdinal);
        }

        private void CancelTeamRespawns(TeamType team)
        {
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null && contestant.Team == team)
                    CancelDuoRespawn(contestant);
            }
        }

        private void CancelDuoRespawn(BrawlerController brawler)
        {
            if (brawler == null ||
                !_pendingDuoRespawns.TryGetValue(brawler, out Coroutine routine))
            {
                return;
            }

            if (routine != null)
                StopCoroutine(routine);
            _pendingDuoRespawns.Remove(brawler);
        }

        private void CancelAllDuoRespawns()
        {
            for (int i = 0; i < _contestants.Count; i++)
                CancelDuoRespawn(_contestants[i]);

            _pendingDuoRespawns.Clear();
        }

        private void DropPowerCubesFrom(BrawlerController dying)
        {
            if (dying == null || dying.State == null)
                return;

            EnsurePowerCubeSpawner(true);

            int dropped = dying.State.CalculateDroppedPowerCubesOnDeath();
            if (dropped > 0)
                _powerCubeSpawner?.SpawnDroppedPowerCubes(dying.Position, dropped);

            dying.State.SetPowerCubeCount(0, false);
        }

        private void CheckEndCondition()
        {
            if (_matchEnding || CountRegisteredTeams() < 2)
                return;

            if (IsDuoShowdown)
            {
                CheckDuoEndCondition();
                return;
            }

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
            if (winner != null && !_placementsByTeam.ContainsKey(winner.Team))
                _placementsByTeam[winner.Team] = 1;

            ScheduleMatchEnd();
        }

        private void CheckDuoEndCondition()
        {
            TeamType survivingTeam = TeamType.Neutral;
            int teamsRemaining = 0;
            HashSet<TeamType> counted = new HashSet<TeamType>();
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant == null ||
                    !counted.Add(contestant.Team) ||
                    _eliminatedTeams.Contains(contestant.Team))
                {
                    continue;
                }

                teamsRemaining++;
                survivingTeam = contestant.Team;
            }

            if (teamsRemaining > 1)
                return;

            _matchEnding = true;
            WinningTeam = teamsRemaining == 1 ? survivingTeam : TeamType.Neutral;
            if (WinningTeam != TeamType.Neutral && !_placementsByTeam.ContainsKey(WinningTeam))
                _placementsByTeam[WinningTeam] = 1;

            ScheduleMatchEnd();
        }

        private void ScheduleMatchEnd()
        {
            CancelAllDuoRespawns();

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

        private int CountAlive(TeamType team)
        {
            int count = 0;
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null &&
                    contestant.Team == team &&
                    IsAliveContestant(contestant))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountRegisteredTeams()
        {
            HashSet<TeamType> teams = new HashSet<TeamType>();
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null)
                    teams.Add(contestant.Team);
            }

            return teams.Count;
        }

        private int CountRemainingTeams()
        {
            if (!IsDuoShowdown)
                return CountAlive();

            HashSet<TeamType> teams = new HashSet<TeamType>();
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null && !_eliminatedTeams.Contains(contestant.Team))
                    teams.Add(contestant.Team);
            }

            return teams.Count;
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

        private BrawlerController FindLivingContestant(TeamType team)
        {
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant != null &&
                    contestant.Team == team &&
                    IsAliveContestant(contestant))
                {
                    return contestant;
                }
            }

            return null;
        }

        private int GetTeamMemberIndex(BrawlerController brawler)
        {
            int memberIndex = 0;
            for (int i = 0; i < _contestants.Count; i++)
            {
                BrawlerController contestant = _contestants[i];
                if (contestant == brawler)
                    return memberIndex;

                if (contestant != null && contestant.Team == brawler.Team)
                    memberIndex++;
            }

            return 0;
        }

        private static bool IsAliveContestant(BrawlerController contestant)
        {
            return SpatialEntityUtility.IsAlive(contestant) &&
                   contestant.State != null &&
                   !contestant.State.IsDead;
        }

        private void EnsurePowerCubeSpawner(bool requiredForDefeatDrop = false)
        {
            if (!_enablePowerCubeCrates && !requiredForDefeatDrop)
                return;

            if (_powerCubeSpawner == null)
                _powerCubeSpawner = GetComponent<SoloShowdownPowerCubeSpawner>();

            if (_powerCubeSpawner == null)
                _powerCubeSpawner = gameObject.AddComponent<SoloShowdownPowerCubeSpawner>();
        }
    }
}
