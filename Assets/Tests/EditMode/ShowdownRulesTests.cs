using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class ShowdownRulesTests
    {
        [Test]
        public void DuoRoster_AssignsTwoPlayersToEachOfFiveTeams()
        {
            for (int teamIndex = 0; teamIndex < ShowdownRules.DuoTeamCount; teamIndex++)
            {
                TeamType expected = TeamRelationshipUtility.GetSoloTeam(teamIndex);
                Assert.AreEqual(expected, ShowdownRules.GetDuoTeamForRosterIndex(teamIndex * 2));
                Assert.AreEqual(expected, ShowdownRules.GetDuoTeamForRosterIndex(teamIndex * 2 + 1));
            }
        }

        [Test]
        public void DuoSpawnOrdinals_KeepTeammatesAdjacent()
        {
            Assert.AreEqual(0, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo1, 0));
            Assert.AreEqual(1, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo1, 1));
            Assert.AreEqual(8, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo5, 0));
            Assert.AreEqual(9, ShowdownRules.GetDuoSpawnOrdinal(TeamType.Solo5, 1));
        }

        [Test]
        public void DuoRules_UseFiveTeamsAndFifteenSecondRespawn()
        {
            Assert.AreEqual(10, ShowdownRules.DuoContestantCount);
            Assert.AreEqual(15f, ShowdownRules.DuoRespawnDelaySeconds);
            Assert.IsTrue(ShowdownRules.IsDuoTeam(TeamType.Solo5));
            Assert.IsFalse(ShowdownRules.IsDuoTeam(TeamType.Solo6));
            Assert.AreEqual("Solo Showdown", ShowdownRules.GetDisplayName(ShowdownVariant.Solo));
            Assert.AreEqual("Duo Showdown", ShowdownRules.GetDisplayName(ShowdownVariant.Duo));
        }

        [Test]
        public void DuoElimination_RequiresBothTeamMembersToBeDead()
        {
            Assert.IsFalse(ShowdownRules.IsTeamEliminated(2));
            Assert.IsFalse(ShowdownRules.IsTeamEliminated(1));
            Assert.IsTrue(ShowdownRules.IsTeamEliminated(0));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(9, 4)]
        public void DefeatDrop_GuaranteesOneThenDropsHalfCarriedCubes(
            int carriedCubeCount,
            int expectedDropCount)
        {
            Assert.AreEqual(
                expectedDropCount,
                ShowdownRules.CalculatePowerCubeDropsOnDefeat(carriedCubeCount));
        }

        [Test]
        public void PowerCubePickup_WorksForAnyTeamAndUpdatesStatsAndCountEvent()
        {
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BaseHealth = 2000f;
            definition.BaseDamage = 400f;
            definition.BaseMoveSpeed = 5f;
            definition.ProgressionBonuses = null;
            definition.SuperChargeSources = null;

            GameObject cubeObject = new GameObject("PowerCubeTest");
            try
            {
                PowerCube cube = cubeObject.AddComponent<PowerCube>();
                BrawlerState carrier = new BrawlerState(definition, TeamType.Solo4);
                int reportedCount = -1;
                carrier.OnPowerCubeCountChanged += count => reportedCount = count;

                Assert.IsTrue(cube.TryPickupBy(carrier));
                Assert.AreEqual(1, carrier.PowerCubeCount);
                Assert.AreEqual(1, reportedCount);
                Assert.AreEqual(2100f, carrier.MaxHealth.Value, 0.001f);
                Assert.AreEqual(420f, carrier.Damage.Value, 0.001f);
            }
            finally
            {
                if (cubeObject != null)
                    Object.DestroyImmediate(cubeObject);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void SoloResults_ShowOnlyTheLocalPlayer()
        {
            Assert.IsTrue(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Solo,
                TeamType.Solo2,
                TeamType.Solo2,
                true));
            Assert.IsFalse(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Solo,
                TeamType.Solo2,
                TeamType.Solo2,
                false));
        }

        [Test]
        public void DuoResults_ShowLocalPlayerAndTeammateOnly()
        {
            Assert.IsTrue(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Duo,
                TeamType.Solo2,
                TeamType.Solo2,
                true));
            Assert.IsTrue(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Duo,
                TeamType.Solo2,
                TeamType.Solo2,
                false));
            Assert.IsFalse(ShowdownRules.ShouldShowResultEntry(
                ShowdownVariant.Duo,
                TeamType.Solo2,
                TeamType.Solo3,
                false));
        }
    }
}
