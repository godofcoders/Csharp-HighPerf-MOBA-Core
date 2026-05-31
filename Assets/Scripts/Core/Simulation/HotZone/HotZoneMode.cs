using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class HotZoneMode : MonoBehaviour, IAIGameModeMacroStateProvider
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
                ServiceProvider.Register<IAIGameModeMacroStateProvider>(this);
        }

        private void OnDisable()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
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
