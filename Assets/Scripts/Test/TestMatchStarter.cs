using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

public class TestMatchStarter : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private BrawlerDefinition _playerBrawler;

    [Header("Enemies")]
    [SerializeField] private BrawlerDefinition _enemyBrawler1;
    [SerializeField] private BrawlerDefinition _enemyBrawler2;

    private void Start()
    {
        // Defer to MatchmakingManager if it's in the scene — that's the
        // production path driven by SceneSelection.SelectedBrawler from
        // the BrawlerSelectScreen. TestMatchStarter only runs when no
        // MatchmakingManager is present (legacy/standalone-test scenes).
        if (MatchmakingManager.Instance != null)
        {
            Debug.Log("[TestMatchStarter] Skipped — MatchmakingManager is in the scene and will drive the roster from SceneSelection.");
            return;
        }

        List<MatchParticipant> roster = new List<MatchParticipant>
        {
            new MatchParticipant("Player_Jessie", TeamType.Blue, _playerBrawler, false),
            new MatchParticipant("Red_Enemy_1", TeamType.Red, _enemyBrawler1, true),
            new MatchParticipant("Red_Enemy_2", TeamType.Red, _enemyBrawler2, true)
        };

        SpawnManager.Instance.PrepareMatch(roster);
        MatchManager.Instance.StartMatchFlow();
    }
}