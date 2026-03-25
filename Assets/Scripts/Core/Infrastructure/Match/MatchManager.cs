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

        // Scores
        private Dictionary<TeamType, int> _teamScores = new Dictionary<TeamType, int>();

        // Events for UI to listen to
        public Action<MatchState> OnStateChanged;
        public Action<TeamType, int> OnScoreUpdated;

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
            ChangeState(MatchState.CountingDown);
            // In a real project, you'd trigger a 3-2-1 UI animation here
            Invoke(nameof(BeginGameplay), _countdownDuration);
        }

        private void BeginGameplay()
        {
            ChangeState(MatchState.Active);
        }

        public void AddScore(TeamType team, int amount)
        {
            if (CurrentState != MatchState.Active) return;

            _teamScores[team] += amount;
            OnScoreUpdated?.Invoke(team, _teamScores[team]);

            // Simple Win Condition Check
            if (_teamScores[team] >= 10) // e.g., First to 10 kills or gems
            {
                EndMatch(team);
            }
        }

        private void EndMatch(TeamType winner)
        {
            ChangeState(MatchState.Ended);
            Debug.Log($"Match Over! Winner: {winner}");
        }

        private void ChangeState(MatchState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}