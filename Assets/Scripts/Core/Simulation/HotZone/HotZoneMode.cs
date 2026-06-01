using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class HotZoneMode : MonoBehaviour,
        IAIGameModeMacroStateProvider,
        IAIRuntimeObjectiveProvider
    {
        public static HotZoneMode Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField, Min(1f)] private float _progressToWin = 100f;
        [SerializeField, Min(0f)] private float _progressPerSecond = 7.5f;

        [Header("Control Points")]
        [SerializeField] private HotZoneControlPoint[] _controlPoints;
        [SerializeField] private bool _autoDiscoverControlPoints = true;

        public float BlueProgress { get; private set; }
        public float RedProgress { get; private set; }
        public float ProgressToWin => _progressToWin;
        public GameModeId ModeId => GameModeId.HotZone;

        private bool _matchScored;
        private int _controlCacheFrame = -1;
        private float _cachedBlueControl;
        private float _cachedRedControl;

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

        private void OnDisable()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void Start()
        {
            if (_autoDiscoverControlPoints &&
                (_controlPoints == null || _controlPoints.Length == 0))
            {
                _controlPoints = FindObjectsOfType<HotZoneControlPoint>(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        private void Update()
        {
            if (_matchScored || _controlPoints == null || _controlPoints.Length == 0)
                return;

            RefreshControlCache();

            float blueControl = _cachedBlueControl;
            float redControl = _cachedRedControl;
            float delta = Time.deltaTime * _progressPerSecond;

            if (blueControl > 0f)
                BlueProgress = Mathf.Min(_progressToWin, BlueProgress + delta * blueControl);

            if (redControl > 0f)
                RedProgress = Mathf.Min(_progressToWin, RedProgress + delta * redControl);

            if (BlueProgress >= _progressToWin)
                ScoreMatch(TeamType.Blue);
            else if (RedProgress >= _progressToWin)
                ScoreMatch(TeamType.Red);
        }

        public void SetProgressForDebug(float blueProgress, float redProgress)
        {
            BlueProgress = Mathf.Clamp(blueProgress, 0f, _progressToWin);
            RedProgress = Mathf.Clamp(redProgress, 0f, _progressToWin);
        }

        public bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state)
        {
            state = AIGameModeMacroState.Neutral;
            if (team == TeamType.Neutral)
                return false;

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            state = AIGameModeMacroStrategy.ResolveHotZone(
                GetTeamProgress(team),
                GetTeamProgress(enemyTeam),
                _progressToWin,
                IsTeamControllingAnyZone(team),
                IsTeamControllingAnyZone(enemyTeam),
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

            if (team == TeamType.Neutral ||
                _controlPoints == null ||
                _controlPoints.Length == 0)
            {
                return false;
            }

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            bool found = false;
            float bestScore = float.MinValue;

            for (int i = 0; i < _controlPoints.Length; i++)
            {
                HotZoneControlPoint point = _controlPoints[i];
                if (point == null)
                    continue;

                point.GetLiveOccupantCounts(
                    out int bluePresence,
                    out int redPresence);

                int friendlyPresence = team == TeamType.Blue
                    ? bluePresence
                    : redPresence;
                int enemyPresence = team == TeamType.Blue
                    ? redPresence
                    : bluePresence;

                TeamType controllingTeam = ResolveControllingTeam(
                    bluePresence,
                    redPresence);
                AIObjectiveControlState controlState =
                    AIObjectiveControlUtility.ResolveForTeam(
                        controllingTeam,
                        team,
                        friendlyPresence,
                        enemyPresence);

                float weight = 78f * point.ControlWeight;

                if (controlState == AIObjectiveControlState.Contested)
                    weight += 18f;
                else if (controllingTeam == enemyTeam)
                    weight += 22f;
                else if (controllingTeam == TeamType.Neutral)
                    weight += 12f;
                else if (controllingTeam == team)
                    weight += 6f;

                Vector3 position = point.transform.position;
                float distSq = (position - selfPosition).sqrMagnitude;
                float score = weight - distSq * 0.04f;

                if (!found || score > bestScore)
                {
                    bestScore = score;
                    objective = new AIObjectiveCandidate(
                        AIObjectiveType.HotZone,
                        position,
                        weight,
                        3.5f,
                        point.name,
                        true,
                        controlState,
                        friendlyPresence,
                        enemyPresence);
                    found = true;
                }
            }

            return found;
        }

        public float GetTeamProgress(TeamType team)
        {
            if (team == TeamType.Blue)
                return BlueProgress;

            if (team == TeamType.Red)
                return RedProgress;

            return 0f;
        }

        public bool IsTeamControllingAnyZone(TeamType team)
        {
            return GetControlWeight(team) > 0f;
        }

        private float GetControlWeight(TeamType team)
        {
            RefreshControlCache();

            if (team == TeamType.Blue)
                return _cachedBlueControl;

            if (team == TeamType.Red)
                return _cachedRedControl;

            return 0f;
        }

        private static TeamType ResolveControllingTeam(int bluePresence, int redPresence)
        {
            if (bluePresence > 0 && redPresence == 0)
                return TeamType.Blue;

            if (redPresence > 0 && bluePresence == 0)
                return TeamType.Red;

            return TeamType.Neutral;
        }

        private void RefreshControlCache()
        {
            if (_controlCacheFrame == Time.frameCount)
                return;

            _controlCacheFrame = Time.frameCount;
            _cachedBlueControl = 0f;
            _cachedRedControl = 0f;

            if (_controlPoints == null)
                return;

            for (int i = 0; i < _controlPoints.Length; i++)
            {
                HotZoneControlPoint point = _controlPoints[i];
                if (point == null)
                    continue;

                TeamType controllingTeam = point.GetControllingTeam();
                if (controllingTeam == TeamType.Blue)
                    _cachedBlueControl += point.ControlWeight;
                else if (controllingTeam == TeamType.Red)
                    _cachedRedControl += point.ControlWeight;
            }
        }

        private void ScoreMatch(TeamType winner)
        {
            _matchScored = true;
            MatchManager.Instance?.AddScore(winner, 10);
        }
    }
}
