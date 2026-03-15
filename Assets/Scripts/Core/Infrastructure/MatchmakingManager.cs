using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public class MatchmakingManager : MonoBehaviour
    {
        public static MatchmakingManager Instance { get; private set; }

        [Header("Player Settings")]
        [SerializeField] private BrawlerDefinition _playerBrawler; // Assign your brawler here!

        [Header("Match Settings")]
        [SerializeField] private int _teamSize = 3;
        [SerializeField] private BrawlerDefinition _defaultBotBrawler;

        private List<MatchParticipant> _roster = new List<MatchParticipant>();
        public bool IsLobbyFull => _roster.Count >= _teamSize * 2;

        private void Awake() => Instance = this;

        private void Start()
        {
            // For now, we auto-join the player on Start for testing.
            // In a finished game, this would be called by a "Play" button.
            if (_playerBrawler != null)
            {
                JoinLocalPlayer(_playerBrawler);
            }
            else
            {
                Debug.LogError("[Lobby] No Player Brawler assigned in MatchmakingManager!");
            }
        }

        public void JoinLocalPlayer(BrawlerDefinition selected)
        {
            _roster.Add(new MatchParticipant("Player (You)", TeamType.Blue, selected, false));
            Debug.Log($"[Lobby] Player joined as {selected.BrawlerName}");
            
            FillWithBots();
        }

        private void FillWithBots()
        {
            int totalSlots = _teamSize * 2;
            while (_roster.Count < totalSlots)
            {
                // Fill Team Blue first, then Team Red
                TeamType team = (_roster.Count < _teamSize) ? TeamType.Blue : TeamType.Red;
                _roster.Add(new MatchParticipant($"Bot {_roster.Count}", team, _defaultBotBrawler, true));
            }

            Debug.Log("[Lobby] Roster full. Initializing Spawn Sequence...");
            StartMatch();
        }

        private void StartMatch()
        {
            SpawnManager.Instance.PrepareMatch(_roster);
            MatchManager.Instance.StartMatchFlow();
        }
    }
}