using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static MatchmakingManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int _teamSize = 3;
        [SerializeField] private BrawlerDefinition _defaultBotBrawler;

        private List<MatchParticipant> _roster = new List<MatchParticipant>();
        public bool IsLobbyFull => _roster.Count >= _teamSize * 2;

        private void Awake() => Instance = this;

        // 1. Add the Local Player
        public void JoinLocalPlayer(BrawlerDefinition selected)
        {
            _roster.Add(new MatchParticipant("Player (You)", TeamType.Blue, selected, false));
            Debug.Log("Player joined Blue team.");

            // In a real game, you'd wait for server pings. 
            // Here, we simulate "finding" bots immediately.
            FillWithBots();
        }

        // 2. Fill remaining slots with AI
        private void FillWithBots()
        {
            int totalSlots = _teamSize * 2;
            while (_roster.Count < totalSlots)
            {
                TeamType team = (_roster.Count < _teamSize) ? TeamType.Blue : TeamType.Red;
                _roster.Add(new MatchParticipant($"Bot {_roster.Count}", team, _defaultBotBrawler, true));
            }

            Debug.Log("Lobby Full. Starting Match...");
            StartMatch();
        }

        private void StartMatch()
        {
            // Pass the roster to the SpawnManager and start the countdown
            SpawnManager.Instance.PrepareMatch(_roster);
            MatchManager.Instance.StartMatchFlow();
        }
    }
}