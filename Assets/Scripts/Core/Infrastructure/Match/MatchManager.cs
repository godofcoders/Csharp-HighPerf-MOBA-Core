using System;
using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _countdownDuration = 3f;

        // Match State
        public MatchState CurrentState { get; private set; } = MatchState.Waiting;

        // When the countdown began. -1 = no countdown active. HUD reads
        // CountdownRemainingSeconds (computed from this + _countdownDuration).
        private float _countdownStartTime = -1f;

        /// <summary>Seconds left in the pre-match countdown. 0 outside of
        /// the CountingDown state. Drives the 3-2-1-GO overlay.</summary>
        public float CountdownRemainingSeconds
        {
            get
            {
                if (CurrentState != MatchState.CountingDown || _countdownStartTime < 0f)
                    return 0f;
                float elapsed = Time.time - _countdownStartTime;
                float remaining = _countdownDuration - elapsed;
                return remaining < 0f ? 0f : remaining;
            }
        }

        /// <summary>Total countdown length authored on this MatchManager.
        /// Public so the overlay can compute "GO!" hold duration etc.</summary>
        public float CountdownDuration => _countdownDuration;

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

        private void Start()
        {
            StartMatchFlow();
        }

        public void StartMatchFlow()
        {
            DeployableMatchCleanup.DespawnAllActiveDeployables();
            _winner = TeamType.Neutral;
            _winnerKnown = false;
            _countdownStartTime = Time.time;
            ChangeState(MatchState.CountingDown);
            Invoke(nameof(BeginGameplay), _countdownDuration);
        }

        private void BeginGameplay()
        {
            if (CurrentState != MatchState.CountingDown)
                return;

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
    }
}
