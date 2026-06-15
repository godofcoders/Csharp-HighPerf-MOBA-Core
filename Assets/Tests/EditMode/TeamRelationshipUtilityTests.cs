using MOBA.Core.Simulation;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class TeamRelationshipUtilityTests
    {
        [Test]
        public void AreEnemies_TreatsSoloTeamsAsMutualEnemies()
        {
            Assert.IsTrue(TeamRelationshipUtility.AreEnemies(TeamType.Solo1, TeamType.Solo2));
            Assert.IsFalse(TeamRelationshipUtility.AreEnemies(TeamType.Solo1, TeamType.Solo1));
        }

        [Test]
        public void AreAllies_DoesNotTreatNeutralAsAlly()
        {
            Assert.IsFalse(TeamRelationshipUtility.AreAllies(TeamType.Neutral, TeamType.Neutral));
            Assert.IsTrue(TeamRelationshipUtility.AreAllies(TeamType.Blue, TeamType.Blue));
        }

        [Test]
        public void CanAffectTeam_AllowsEnemyProjectilesToHitNeutralBreakables()
        {
            Assert.IsTrue(TeamRelationshipUtility.CanAffectTeam(
                ProjectileHitTeamRule.EnemiesOnly,
                TeamType.Solo1,
                TeamType.Neutral));

            Assert.IsFalse(TeamRelationshipUtility.CanAffectTeam(
                ProjectileHitTeamRule.AlliesOnly,
                TeamType.Solo1,
                TeamType.Neutral));
        }
    }
}
