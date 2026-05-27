using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AITeamCompositionPlannerTests
    {
        private readonly List<Object> _createdAssets = new List<Object>(8);

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void PickBotBrawler_GemGrab_PrefersSupportControlProfile()
        {
            BrawlerDefinition support = MakeBrawler("Support", BrawlerArchetype.Support);
            BrawlerDefinition sniper = MakeBrawler("Sniper", BrawlerArchetype.Sniper);
            BrawlerDefinition tank = MakeBrawler("Tank", BrawlerArchetype.Tank);

            AITeamCompositionPlanner.PickResult result =
                AITeamCompositionPlanner.PickBotBrawler(
                    new[] { sniper, tank, support },
                    new List<MatchParticipant>(),
                    TeamType.Red,
                    GameModeId.GemGrab,
                    TestOptions());

            Assert.AreSame(support, result.Brawler);
            StringAssert.Contains("mode=", result.Reason);
        }

        [Test]
        public void PickBotBrawler_Knockout_PrefersLongRangeProfile()
        {
            BrawlerDefinition support = MakeBrawler("Support", BrawlerArchetype.Support);
            BrawlerDefinition sniper = MakeBrawler("Sniper", BrawlerArchetype.Sniper);
            BrawlerDefinition tank = MakeBrawler("Tank", BrawlerArchetype.Tank);

            AITeamCompositionPlanner.PickResult result =
                AITeamCompositionPlanner.PickBotBrawler(
                    new[] { support, tank, sniper },
                    new List<MatchParticipant>(),
                    TeamType.Red,
                    GameModeId.Knockout,
                    TestOptions());

            Assert.AreSame(sniper, result.Brawler);
            StringAssert.Contains("coverage=", result.Reason);
        }

        [Test]
        public void PickBotBrawler_AvoidsDuplicatingPlayerBrawlerOnSameTeam()
        {
            BrawlerDefinition playerPick = MakeBrawler("Colt", BrawlerArchetype.Sniper);
            BrawlerDefinition controller = MakeBrawler("Jessie", BrawlerArchetype.Controller);
            List<MatchParticipant> roster = new List<MatchParticipant>
            {
                new MatchParticipant("Player", TeamType.Blue, playerPick, false)
            };

            AITeamCompositionPlanner.PickResult result =
                AITeamCompositionPlanner.PickBotBrawler(
                    new[] { playerPick, controller },
                    roster,
                    TeamType.Blue,
                    GameModeId.GemGrab,
                    TestOptions());

            Assert.AreSame(controller, result.Brawler);
            Assert.AreNotSame(playerPick, result.Brawler);
        }

        [Test]
        public void PickBotBrawler_PenalizesDuplicateArchetypesWithinTeam()
        {
            BrawlerDefinition firstController = MakeBrawler("Jessie", BrawlerArchetype.Controller);
            BrawlerDefinition secondController = MakeBrawler("AltController", BrawlerArchetype.Controller);
            BrawlerDefinition support = MakeBrawler("Byron", BrawlerArchetype.Support);
            List<MatchParticipant> roster = new List<MatchParticipant>
            {
                new MatchParticipant("Bot 1", TeamType.Red, firstController, true)
            };

            AITeamCompositionPlanner.PickResult result =
                AITeamCompositionPlanner.PickBotBrawler(
                    new[] { secondController, support },
                    roster,
                    TeamType.Red,
                    GameModeId.GemGrab,
                    TestOptions());

            Assert.AreSame(support, result.Brawler);
        }

        [Test]
        public void PickBotBrawler_AllowsDuplicateWhenPoolIsExhausted()
        {
            BrawlerDefinition onlyOption = MakeBrawler("Only", BrawlerArchetype.Sniper);
            List<MatchParticipant> roster = new List<MatchParticipant>
            {
                new MatchParticipant("Player", TeamType.Blue, onlyOption, false)
            };

            AITeamCompositionPlanner.PickResult result =
                AITeamCompositionPlanner.PickBotBrawler(
                    new[] { onlyOption },
                    roster,
                    TeamType.Blue,
                    GameModeId.Knockout,
                    TestOptions());

            Assert.AreSame(onlyOption, result.Brawler);
            StringAssert.Contains("team_dup_brawler", result.Reason);
        }

        [Test]
        public void GetPreferredSpawnIndex_AssignsAnchorRolesToCenterAndBacklineToEdges()
        {
            BrawlerDefinition support = MakeBrawler("Support", BrawlerArchetype.Support);
            BrawlerDefinition sniper = MakeBrawler("Sniper", BrawlerArchetype.Sniper);
            BrawlerDefinition assassin = MakeBrawler("Assassin", BrawlerArchetype.Assassin);

            Assert.AreEqual(1, AITeamCompositionPlanner.GetPreferredSpawnIndex(support, 3, 0));
            Assert.AreEqual(2, AITeamCompositionPlanner.GetPreferredSpawnIndex(sniper, 3, 0));
            Assert.AreEqual(0, AITeamCompositionPlanner.GetPreferredSpawnIndex(assassin, 3, 0));
            Assert.AreEqual(2, AITeamCompositionPlanner.GetPreferredSpawnIndex(assassin, 3, 1));
        }

        private BrawlerDefinition MakeBrawler(string name, BrawlerArchetype archetype)
        {
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BrawlerName = name;
            definition.Archetype = archetype;
            _createdAssets.Add(definition);
            return definition;
        }

        private static AITeamCompositionPlanner.PickOptions TestOptions()
        {
            AITeamCompositionPlanner.PickOptions options = AITeamCompositionPlanner.PickOptions.Default;
            options.RandomizeTies = false;
            options.RandomCandidateScoreBand = 0f;
            return options;
        }
    }
}
