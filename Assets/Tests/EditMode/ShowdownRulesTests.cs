using MOBA.Core.Simulation;
using NUnit.Framework;

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
        }

        [Test]
        public void DuoElimination_RequiresBothTeamMembersToBeDead()
        {
            Assert.IsFalse(ShowdownRules.IsTeamEliminated(2));
            Assert.IsFalse(ShowdownRules.IsTeamEliminated(1));
            Assert.IsTrue(ShowdownRules.IsTeamEliminated(0));
        }
    }
}
