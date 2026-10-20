using System;
using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public sealed class SimulationCountdown
    {
        public bool IsScheduled { get; private set; }
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }
        public uint DurationTicks { get; private set; }

        public void Schedule(uint currentTick, uint leadInTicks, uint durationTicks)
        {
            StartTick = AddSaturated(currentTick, leadInTicks);
            DurationTicks = durationTicks;
            EndTick = AddSaturated(StartTick, durationTicks);
            IsScheduled = true;
        }

        public uint GetRemainingTicks(uint currentTick)
        {
            if (!IsScheduled)
                return 0u;

            if (currentTick < StartTick)
                return DurationTicks;

            return currentTick < EndTick ? EndTick - currentTick : 0u;
        }

        public bool HasElapsed(uint currentTick)
        {
            return IsScheduled && currentTick >= EndTick;
        }

        public bool ExtendTo(uint currentTick, uint minimumRemainingTicks)
        {
            if (!IsScheduled || GetRemainingTicks(currentTick) >= minimumRemainingTicks)
                return false;

            if (currentTick < StartTick)
            {
                DurationTicks = minimumRemainingTicks;
                EndTick = AddSaturated(StartTick, minimumRemainingTicks);
                return true;
            }

            uint elapsedTicks = currentTick - StartTick;
            DurationTicks = AddSaturated(elapsedTicks, minimumRemainingTicks);
            EndTick = AddSaturated(currentTick, minimumRemainingTicks);
            return true;
        }

        public void Clear()
        {
            IsScheduled = false;
            StartTick = 0u;
            EndTick = 0u;
            DurationTicks = 0u;
        }

        private static uint AddSaturated(uint left, uint right)
        {
            ulong sum = (ulong)left + right;
            return sum >= uint.MaxValue ? uint.MaxValue : (uint)sum;
        }
    }

    public class MatchManager : MonoBehaviour, ITickable
    {
        public static MatchManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _countdownDuration = 5f;

        // Match State
        public MatchState CurrentState { get; private set; } = MatchState.Waiting;

        private readonly SimulationCountdown _countdown = new SimulationCountdown();
        private bool _started;

        /// <summary>Seconds left in the pre-match countdown. 0 outside of
        /// the CountingDown state. Drives the 5-4-3-2-1-GO overlay.</summary>
        public float CountdownRemainingSeconds
        {
            get
            {
                if (CurrentState != MatchState.CountingDown || !_countdown.IsScheduled)
                    return 0f;

                return SimulationTiming.TicksToSeconds(
                    _countdown.GetRemainingTicks(ResolveCurrentTick()));
            }
        }

        /// <summary>Total countdown length authored on this MatchManager.
        /// Public so the overlay can compute "GO!" hold duration etc.</summary>
        public float CountdownDuration => CurrentState == MatchState.CountingDown
            ? SimulationTiming.TicksToSeconds(_countdown.DurationTicks)
            : _countdownDuration;

        // Scores
        private Dictionary<TeamType, int> _teamScores = new Dictionary<TeamType, int>();
        private TeamType _winner = TeamType.Neutral;
        private bool _winnerKnown;

        // Events for UI to listen to
        public Action<MatchState> OnStateChanged;
        public Action<TeamType, int> OnScoreUpdated;
        public bool WinnerKnown => _winnerKnown;
        public TeamType Winner => _winner;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _teamScores[TeamType.Blue] = 0;
            _teamScores[TeamType.Red] = 0;
        }

        private void OnEnable()
        {
            if (_started)
                SimulationClock.Registry?.Register(this, TickPhase.PreTick);
        }

        private void Start()
        {
            _started = true;
            SimulationClock.Registry?.Register(this, TickPhase.PreTick);
            StartMatchFlow();
        }

        private void OnDisable()
        {
            SimulationClock.Registry?.Unregister(this, TickPhase.PreTick);
        }

        private void OnDestroy()
        {
            SimulationClock.Registry?.Unregister(this, TickPhase.PreTick);
            if (Instance == this)
                Instance = null;
        }

        public void Tick(uint currentTick)
        {
            if (CurrentState == MatchState.CountingDown && _countdown.HasElapsed(currentTick))
                BeginGameplay();
        }

        public void StartMatchFlow()
        {
            MatchResultBoard.Reset();
            DeployableMatchCleanup.DespawnAllActiveDeployables();
            _winner = TeamType.Neutral;
            _winnerKnown = false;
            StartCountdown(0f);
        }

        public void StartRoundResetCountdown(float leadInSeconds = 0f)
        {
            if (CurrentState == MatchState.Ended)
                return;

            StartCountdown(leadInSeconds);
        }

        private void StartCountdown(float leadInSeconds)
        {
            _countdown.Schedule(
                ResolveCurrentTick(),
                SimulationTiming.SecondsToTicks(leadInSeconds),
                SimulationTiming.SecondsToTicks(_countdownDuration));
            ChangeState(MatchState.CountingDown);
        }

        public void ExtendCurrentCountdownTo(float minimumRemainingSeconds)
        {
            if (CurrentState != MatchState.CountingDown || !_countdown.IsScheduled)
                return;

            _countdown.ExtendTo(
                ResolveCurrentTick(),
                SimulationTiming.SecondsToTicks(minimumRemainingSeconds));
        }

        private void BeginGameplay()
        {
            if (CurrentState != MatchState.CountingDown)
                return;

            _countdown.Clear();
            ChangeState(MatchState.Active);
        }

        public void AddScore(TeamType team, int amount)
        {
            if (CurrentState != MatchState.Active) return;

            EnsureTeamScore(team);
            _teamScores[team] += amount;
            OnScoreUpdated?.Invoke(team, _teamScores[team]);

            // Simple Win Condition Check
            if (_teamScores[team] >= 10) // e.g., First to 10 kills or gems
            {
                EndMatch(team);
            }
        }

        public int GetScore(TeamType team)
        {
            return _teamScores.TryGetValue(team, out int score) ? score : 0;
        }

        public bool TryGetWinner(out TeamType winner)
        {
            winner = _winner;
            return _winnerKnown;
        }

        public void EndMatch(TeamType winner)
        {
            if (CurrentState == MatchState.Ended)
                return;

            _winner = winner;
            _winnerKnown = winner != TeamType.Neutral;
            _countdown.Clear();
            ChangeState(MatchState.Ended);
            Debug.Log($"Match Over! Winner: {winner}");
        }

        private void EnsureTeamScore(TeamType team)
        {
            if (!_teamScores.ContainsKey(team))
                _teamScores[team] = 0;
        }

        private void ChangeState(MatchState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        private static uint ResolveCurrentTick()
        {
            return ServiceProvider.TryGet(out ISimulationClock clock)
                ? clock.CurrentTick
                : 0u;
        }
    }
}
