using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class BrawlBallMode : MonoBehaviour,
        IAIGameModeMacroStateProvider,
        IAIRuntimeObjectiveProvider
    {
        public static BrawlBallMode Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField, Min(1)] private int _goalsToWin = 2;

        [Header("Ball State")]
        [SerializeField] private Transform _ballTransform;
        [SerializeField] private BrawlerController _ballCarrier;

        public int BlueGoals { get; private set; }
        public int RedGoals { get; private set; }
        public int GoalsToWin => _goalsToWin;
        public BrawlerController BallCarrier => _ballCarrier;
        public Vector3 BallPosition => _ballCarrier != null
            ? _ballCarrier.Position
            : (_ballTransform != null ? _ballTransform.position : Vector3.zero);

        public GameModeId ModeId => GameModeId.BrawlBall;

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>(this);
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>(this);
        }

        public void SetBallCarrier(BrawlerController carrier)
        {
            _ballCarrier = carrier;
        }

        public void ClearBallCarrier()
        {
            _ballCarrier = null;
        }

        public void RecordGoal(TeamType scoringTeam)
        {
            if (scoringTeam == TeamType.Blue)
                BlueGoals++;
            else if (scoringTeam == TeamType.Red)
                RedGoals++;
            else
                return;

            _ballCarrier = null;

            if (GetTeamGoals(scoringTeam) >= _goalsToWin)
                MatchManager.Instance?.AddScore(scoringTeam, 10);
        }

        public int GetTeamGoals(TeamType team)
        {
            if (team == TeamType.Blue)
                return BlueGoals;

            if (team == TeamType.Red)
                return RedGoals;

            return 0;
        }

        public bool TryResolveMacroState(
            TeamType team,
            out AIGameModeMacroState state)
        {
            state = AIGameModeMacroState.Neutral;
            if (team == TeamType.Neutral)
                return false;

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            bool ownHasBall = _ballCarrier != null && _ballCarrier.Team == team;
            bool enemyHasBall = _ballCarrier != null && _ballCarrier.Team == enemyTeam;

            state = AIGameModeMacroStrategy.ResolveBrawlBall(
                GetTeamGoals(team),
                GetTeamGoals(enemyTeam),
                _goalsToWin,
                ownHasBall,
                enemyHasBall,
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
                (_ballCarrier == null && _ballTransform == null))
            {
                return false;
            }

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            bool ownHasBall = _ballCarrier != null && _ballCarrier.Team == team;
            bool enemyHasBall = _ballCarrier != null && _ballCarrier.Team == enemyTeam;

            float weight = 84f;
            if (enemyHasBall)
                weight += 18f;
            else if (!ownHasBall)
                weight += 10f;

            objective = new AIObjectiveCandidate(
                AIObjectiveType.Ball,
                BallPosition,
                weight,
                2.75f,
                "RuntimeBall",
                true);
            return true;
        }
    }
}
