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

        private BrawlBallController _ball;

        public int BlueGoals { get; private set; }
        public int RedGoals { get; private set; }
        public int GoalsToWin => _goalsToWin;
        public BrawlerController BallCarrier => IsValidCarrier(_ballCarrier) ? _ballCarrier : null;
        public BrawlBallController Ball => _ball;
        public Vector3 BallPosition => BallCarrier != null
            ? BallCarrier.Position
            : (_ball != null
                ? _ball.CurrentPosition
                : (_ballTransform != null ? _ballTransform.position : Vector3.zero));

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
            if (_ball != null)
                _ball.AssignCarrierFromMode(carrier);

            SetBallCarrierState(carrier);
        }

        public void ClearBallCarrier()
        {
            if (_ball != null)
                _ball.ClearCarrierFromMode();

            SetBallCarrierState(null);
        }

        public void RegisterBall(BrawlBallController ball)
        {
            if (ball == null)
                return;

            _ball = ball;
            _ballTransform = ball.transform;

            if (BallCarrier != null)
                _ball.AssignCarrierFromMode(BallCarrier);
        }

        public void UnregisterBall(BrawlBallController ball)
        {
            if (_ball != ball)
                return;

            _ball = null;
            if (_ballTransform == ball.transform)
                _ballTransform = null;
        }

        public void ResetBall()
        {
            if (_ball != null)
                _ball.ResetToSpawn();
            else
                ClearBallCarrier();
        }

        internal void NotifyBallPickedUp(BrawlerController carrier, Vector3 position)
        {
            SetBallCarrierState(carrier);
            BrawlBallEventBus.RaiseBallPickedUp(carrier, position);
        }

        internal void NotifyBallDropped(Vector3 position)
        {
            SetBallCarrierState(null);
            BrawlBallEventBus.RaiseBallDropped(position);
        }

        internal void NotifyBallReset(Vector3 position)
        {
            SetBallCarrierState(null);
            BrawlBallEventBus.RaiseBallReset(position);
        }

        public void RecordGoal(TeamType scoringTeam)
        {
            if (scoringTeam == TeamType.Blue)
                BlueGoals++;
            else if (scoringTeam == TeamType.Red)
                RedGoals++;
            else
                return;

            ResetBall();

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
            BrawlerController carrier = BallCarrier;
            bool ownHasBall = carrier != null && carrier.Team == team;
            bool enemyHasBall = carrier != null && carrier.Team == enemyTeam;

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
                (BallCarrier == null && _ball == null && _ballTransform == null))
            {
                return false;
            }

            TeamType enemyTeam = team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
            BrawlerController carrier = BallCarrier;
            bool ownHasBall = carrier != null && carrier.Team == team;
            bool enemyHasBall = carrier != null && carrier.Team == enemyTeam;

            int friendlyPresence = ownHasBall ? 1 : 0;
            int enemyPresence = enemyHasBall ? 1 : 0;
            AIObjectiveControlState controlState =
                AIObjectiveControlUtility.ResolveForTeam(
                    ownHasBall
                        ? team
                        : (enemyHasBall ? enemyTeam : TeamType.Neutral),
                    team,
                    friendlyPresence,
                    enemyPresence);

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
                true,
                controlState,
                friendlyPresence,
                enemyPresence);
            return true;
        }

        private void SetBallCarrierState(BrawlerController carrier)
        {
            if (!IsValidCarrier(carrier))
                carrier = null;

            if (_ballCarrier == carrier)
                return;

            _ballCarrier = carrier;
            BrawlBallEventBus.RaiseCarrierChanged(_ballCarrier);
        }

        private static bool IsValidCarrier(BrawlerController carrier)
        {
            return SpatialEntityUtility.IsAlive(carrier) &&
                   carrier.State != null &&
                   !carrier.State.IsDead &&
                   carrier.gameObject.activeInHierarchy;
        }
    }
}
