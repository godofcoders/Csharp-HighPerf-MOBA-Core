using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class MatchHUDFormatterTests
    {
        [Test]
        public void FormatClock_FormatsMinuteAndSeconds()
        {
            Assert.AreEqual("2:05", MatchHUDFormatter.FormatClock(125.8f));
            Assert.AreEqual("0:00", MatchHUDFormatter.FormatClock(-3f));
        }

        [Test]
        public void FormatActiveGemGrabStatus_ShowsGemTargetAndMatchClock()
        {
            string status = MatchHUDFormatter.FormatActiveGemGrabStatus(
                blueGems: 3,
                redGems: 7,
                gemsToWin: 10,
                hasLeader: false,
                leadingTeam: TeamType.Blue,
                winTimerRemainingSeconds: 0f,
                matchTimeRemainingSeconds: 92f);

            Assert.AreEqual("Gems  Blue 3/10  Red 7/10  Match 1:32", status);
        }

        [Test]
        public void FormatActiveGemGrabStatus_AddsHoldCountdownWhenTeamLeads()
        {
            string status = MatchHUDFormatter.FormatActiveGemGrabStatus(
                blueGems: 10,
                redGems: 8,
                gemsToWin: 10,
                hasLeader: true,
                leadingTeam: TeamType.Blue,
                winTimerRemainingSeconds: 12.34f,
                matchTimeRemainingSeconds: 92f);

            Assert.AreEqual("Gems  Blue 10/10  Red 8/10  Match 1:32  Blue hold 12.3s", status);
        }

        [Test]
        public void FormatGemGrabHoldStatus_UsesObjectivePromptUntilCountdownStarts()
        {
            Assert.AreEqual(
                "Collect gems",
                MatchHUDFormatter.FormatGemGrabHoldStatus(false, TeamType.Red, 0f));

            Assert.AreEqual(
                "Red hold 6.8s",
                MatchHUDFormatter.FormatGemGrabHoldStatus(true, TeamType.Red, 6.78f));
        }
    }
}
