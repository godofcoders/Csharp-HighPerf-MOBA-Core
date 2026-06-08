using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class BrawlerHealthBarColorUtilityTests
    {
        [Test]
        public void ResolvePerspective_ReturnsOwn_WhenEntityMatchesLocalPlayer()
        {
            BrawlerHealthBarPerspective perspective =
                BrawlerHealthBarColorUtility.ResolvePerspective(
                    TeamType.Blue,
                    subjectEntityId: 10,
                    TeamType.Blue,
                    localEntityId: 10);

            Assert.AreEqual(BrawlerHealthBarPerspective.Own, perspective);
        }

        [Test]
        public void ResolvePerspective_ReturnsAlly_WhenTeamMatchesLocalPlayer()
        {
            BrawlerHealthBarPerspective perspective =
                BrawlerHealthBarColorUtility.ResolvePerspective(
                    TeamType.Blue,
                    subjectEntityId: 11,
                    TeamType.Blue,
                    localEntityId: 10);

            Assert.AreEqual(BrawlerHealthBarPerspective.Ally, perspective);
        }

        [Test]
        public void ResolvePerspective_ReturnsEnemy_WhenTeamDiffersFromLocalPlayer()
        {
            BrawlerHealthBarPerspective perspective =
                BrawlerHealthBarColorUtility.ResolvePerspective(
                    TeamType.Red,
                    subjectEntityId: 12,
                    TeamType.Blue,
                    localEntityId: 10);

            Assert.AreEqual(BrawlerHealthBarPerspective.Enemy, perspective);
        }

        [Test]
        public void ResolvePerspective_ReturnsUnknown_WhenLocalPlayerIsMissing()
        {
            BrawlerHealthBarPerspective perspective =
                BrawlerHealthBarColorUtility.ResolvePerspective(
                    TeamType.Red,
                    subjectEntityId: 12,
                    TeamType.Neutral,
                    localEntityId: 0);

            Assert.AreEqual(BrawlerHealthBarPerspective.Unknown, perspective);
        }
    }
}
