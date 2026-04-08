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