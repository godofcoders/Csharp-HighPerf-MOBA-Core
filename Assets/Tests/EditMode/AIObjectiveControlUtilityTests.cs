using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIObjectiveControlUtilityTests
    {
        [Test]
        public void ResolveForTeam_ReturnsContested_WhenBothTeamsArePresent()
        {
            AIObjectiveControlState state =
                AIObjectiveControlUtility.ResolveForTeam(
                    TeamType.Neutral,
                    TeamType.Blue,
                    friendlyPresence: 1,
                    enemyPresence: 1);

            Assert.AreEqual(AIObjectiveControlState.Contested, state);
        }

        [Test]
        public void ResolveForTeam_ReturnsEnemyControlled_WhenEnemyOwnsObjective()
        {
            AIObjectiveControlState state =
                AIObjectiveControlUtility.ResolveForTeam(
                    TeamType.Red,
                    TeamType.Blue,
                    friendlyPresence: 0,
                    enemyPresence: 1);

            Assert.AreEqual(AIObjectiveControlState.EnemyControlled, state);
        }

        [Test]
        public void UtilityScoreDelta_PrioritizesEnemyAndContestedObjectives()
        {
            float friendlyDelta =
                AIObjectiveControlUtility.GetUtilityScoreDelta(
                    AIObjectiveControlState.FriendlyControlled);
            float contestedDelta =
                AIObjectiveControlUtility.GetUtilityScoreDelta(
                    AIObjectiveControlState.Contested);
            float enemyDelta =
                AIObjectiveControlUtility.GetUtilityScoreDelta(
                    AIObjectiveControlState.EnemyControlled);

            Assert.Greater(contestedDelta, friendlyDelta);
            Assert.Greater(enemyDelta, contestedDelta);
        }

        [Test]
        public void PresenceDelta_ReducesOverFriendlySaturation()
        {
            float friendlyHeavyDelta =
                AIObjectiveControlUtility.GetUtilityPresenceDelta(
                    friendlyPresence: 3,
                    enemyPresence: 0);
            float enemyHeavyDelta =
                AIObjectiveControlUtility.GetUtilityPresenceDelta(
                    friendlyPresence: 0,
                    enemyPresence: 3);

            Assert.Less(friendlyHeavyDelta, 0f);
            Assert.Greater(enemyHeavyDelta, 0f);
        }
    }
}
