using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

public class TestMatchStarter : MonoBehaviour
{
    [SerializeField] private BrawlerDefinition _playerBrawler;
    [SerializeField] private BrawlerDefinition _enemyBrawler;

    private void Start()
    {
        List<MatchParticipant> roster = new List<MatchParticipant>
        {
            new MatchParticipant("Player", TeamType.Blue, _playerBrawler, false),
            new MatchParticipant("Enemy", TeamType.Red, _enemyBrawler, true)
        };

        SpawnManager.Instance.PrepareMatch(roster);
        MatchManager.Instance.StartMatchFlow();
    }
}