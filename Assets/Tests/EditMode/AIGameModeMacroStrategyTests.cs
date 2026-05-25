using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIGameModeMacroStrategyTests
    {
        [Test]
        public void ResolveGemGrab_Holds_WhenOwnTeamHasCountdown()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveGemGrab(
                ownGems: 10,
                enemyGems: 6,
                gemsToWin: 10,
                winTimerRemainingSeconds: 11f,
                matchTimeRemainingSeconds: 80f,
                ownTeamHasCountdown: true,
                enemyTeamHasCountdown: false);

            Assert.AreEqual(AIGameModeMacroCall.Hold, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.Countdown, state.Phase);
            Assert.IsTrue(state.OwnTeamHasCountdown);
        }

        [Test]
        public void ResolveGemGrab_Resets_WhenEnemyTeamHasCountdown()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveGemGrab(
                ownGems: 7,
                enemyGems: 10,
                gemsToWin: 10,
                winTimerRemainingSeconds: 6f,
                matchTimeRemainingSeconds: 80f,
                ownTeamHasCountdown: false,
                enemyTeamHasCountdown: true);

            Assert.AreEqual(AIGameModeMacroCall.Reset, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.Countdown, state.Phase);
            Assert.IsTrue(state.EnemyTeamHasCountdown);
        }

        [Test]
        public void ResolveGemGrab_Pushes_WhenBehindLate()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveGemGrab(
                ownGems: 5,
                enemyGems: 7,
                gemsToWin: 10,
                winTimerRemainingSeconds: 0f,
                matchTimeRemainingSeconds: 18f,
                ownTeamHasCountdown: false,
                enemyTeamHasCountdown: false);

            Assert.AreEqual(AIGameModeMacroCall.Push, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.FinalPressure, state.Phase);
            Assert.IsTrue(state.IsBehind);
        }

        [Test]
        public void ResolveGemGrab_Holds_WhenNearThresholdAndAhead()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveGemGrab(
                ownGems: 9,
                enemyGems: 7,
                gemsToWin: 10,
                winTimerRemainingSeconds: 0f,
                matchTimeRemainingSeconds: 70f,
                ownTeamHasCountdown: false,
                enemyTeamHasCountdown: false);

            Assert.AreEqual(AIGameModeMacroCall.Hold, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.Contest, state.Phase);
            Assert.IsTrue(state.IsLeading);
        }
    }
}
