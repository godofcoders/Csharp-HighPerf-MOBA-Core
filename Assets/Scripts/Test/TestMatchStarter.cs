using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

public class TestMatchStarter : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private BrawlerDefinition _playerBrawler;

    [Header("Targets")]
    [SerializeField] private BrawlerDefinition _allyBrawler;
    [SerializeField] private BrawlerDefinition _enemyBrawler;

    private void Start()
    {
        List<MatchParticipant> roster = new List<MatchParticipant>
        {
            new MatchParticipant("Player_Byron", TeamType.Blue, _playerBrawler, false),
            new MatchParticipant("Blue_Ally", TeamType.Blue, _allyBrawler, true),
            new MatchParticipant("Red_Enemy", TeamType.Red, _enemyBrawler, true)
        };

        SpawnManager.Instance.PrepareMatch(roster);
        MatchManager.Instance.StartMatchFlow();
    }
}