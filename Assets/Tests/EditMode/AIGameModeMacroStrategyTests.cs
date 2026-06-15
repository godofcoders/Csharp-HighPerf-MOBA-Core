using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIGameModeMacroStrategyTests
    {
        [TearDown]
        public void TearDown()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>();
        }

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

        [Test]
        public void ResolveKnockout_Resets_WhenDownPlayers()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveKnockout(
                ownRoundsWon: 0,
                enemyRoundsWon: 0,
                roundsToWin: 2,
                ownAlive: 1,
                enemyAlive: 3,
                teamSize: 3,
                matchTimeRemainingSeconds: 80f);

            Assert.AreEqual(GameModeId.Knockout, state.Mode);
            Assert.AreEqual(AIGameModeMacroCall.Reset, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.Contest, state.Phase);
            Assert.AreEqual("down_players", state.Reason);
        }

        [Test]
        public void ResolveKnockout_Pushes_WhenNumbersAdvantage()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveKnockout(
                ownRoundsWon: 0,
                enemyRoundsWon: 0,
                roundsToWin: 2,
                ownAlive: 3,
                enemyAlive: 1,
                teamSize: 3,
                matchTimeRemainingSeconds: 80f);

            Assert.AreEqual(AIGameModeMacroCall.Push, state.Call);
            Assert.AreEqual("numbers_advantage", state.Reason);
        }

        [Test]
        public void ResolveBrawlBall_DefendsWhenEnemyHasBall()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveBrawlBall(
                ownGoals: 0,
                enemyGoals: 1,
                goalsToWin: 2,
                ownHasBall: false,
                enemyHasBall: true,
                matchTimeRemainingSeconds: 90f);

            Assert.AreEqual(GameModeId.BrawlBall, state.Mode);
            Assert.AreEqual(AIGameModeMacroCall.Reset, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.FinalPressure, state.Phase);
            Assert.AreEqual("defend_score_point", state.Reason);
        }

        [Test]
        public void ResolveBrawlBall_PushesWithPossession()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveBrawlBall(
                ownGoals: 0,
                enemyGoals: 0,
                goalsToWin: 2,
                ownHasBall: true,
                enemyHasBall: false,
                matchTimeRemainingSeconds: 90f);

            Assert.AreEqual(AIGameModeMacroCall.Push, state.Call);
            Assert.AreEqual("ball_possession", state.Reason);
        }

        [Test]
        public void ResolveHotZone_DeniesNearEnemyFinish()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveHotZone(
                ownProgress: 72f,
                enemyProgress: 95f,
                progressToWin: 100f,
                ownControllingZone: false,
                enemyControllingZone: true,
                matchTimeRemainingSeconds: 50f);

            Assert.AreEqual(GameModeId.HotZone, state.Mode);
            Assert.AreEqual(AIGameModeMacroCall.Reset, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.FinalPressure, state.Phase);
            Assert.AreEqual("deny_zone_finish", state.Reason);
        }

        [Test]
        public void ResolveHotZone_HoldsWhenLeadingAndControlling()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveHotZone(
                ownProgress: 55f,
                enemyProgress: 35f,
                progressToWin: 100f,
                ownControllingZone: true,
                enemyControllingZone: false,
                matchTimeRemainingSeconds: 70f);

            Assert.AreEqual(AIGameModeMacroCall.Hold, state.Call);
            Assert.AreEqual("hold_zone_lead", state.Reason);
        }

        [Test]
        public void ResolveSoloShowdown_HoldsDuringCrowdedOpening()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveSoloShowdown(
                ownAlive: 1,
                aliveOpponents: 5,
                totalAlive: 6,
                outsideSafeZone: false,
                distanceBeyondSafeZone: 0f,
                matchTimeRemainingSeconds: 0f);

            Assert.AreEqual(GameModeId.SoloShowdown, state.Mode);
            Assert.AreEqual(AIGameModeMacroCall.Hold, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.Opening, state.Phase);
            Assert.AreEqual("survive_field", state.Reason);
        }

        [Test]
        public void ResolveSoloShowdown_ResetsWhenOutsideSafeZone()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveSoloShowdown(
                ownAlive: 1,
                aliveOpponents: 3,
                totalAlive: 4,
                outsideSafeZone: true,
                distanceBeyondSafeZone: 2.5f,
                matchTimeRemainingSeconds: 0f);

            Assert.AreEqual(AIGameModeMacroCall.Reset, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.FinalPressure, state.Phase);
            Assert.AreEqual("poison_escape_urgent", state.Reason);
            Assert.IsTrue(state.IsBehind);
        }

        [Test]
        public void ResolveSoloShowdown_PushesFinalDuel()
        {
            AIGameModeMacroState state = AIGameModeMacroStrategy.ResolveSoloShowdown(
                ownAlive: 1,
                aliveOpponents: 1,
                totalAlive: 2,
                outsideSafeZone: false,
                distanceBeyondSafeZone: 0f,
                matchTimeRemainingSeconds: 0f);

            Assert.AreEqual(AIGameModeMacroCall.Push, state.Call);
            Assert.AreEqual(AIGameModeObjectivePhase.FinalPressure, state.Phase);
            Assert.AreEqual("final_duel", state.Reason);
            Assert.IsTrue(state.IsLeading);
        }

        [Test]
        public void ResolveCurrentMode_UsesRegisteredRuntimeProvider()
        {
            ServiceProvider.Register<IAIGameModeMacroStateProvider>(
                new FakeMacroStateProvider());

            AIGameModeMacroState state =
                AIGameModeMacroStrategy.ResolveCurrentMode(TeamType.Blue);

            Assert.AreEqual(GameModeId.HotZone, state.Mode);
            Assert.AreEqual(AIGameModeMacroCall.Push, state.Call);
            Assert.AreEqual("runtime_provider", state.Reason);
        }

        [Test]
        public void BrawlBallMode_ProvidesGoalAwareMacroState()
        {
            var gameObject = new GameObject("BrawlBallModeTest");
            try
            {
                var mode = gameObject.AddComponent<BrawlBallMode>();
                mode.RecordGoal(TeamType.Blue);

                bool resolved = mode.TryResolveMacroState(
                    TeamType.Blue,
                    out AIGameModeMacroState state);

                Assert.IsTrue(resolved);
                Assert.AreEqual(GameModeId.BrawlBall, state.Mode);
                Assert.AreEqual(AIGameModeMacroCall.Hold, state.Call);
                Assert.AreEqual("protect_lead", state.Reason);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HotZoneMode_ProvidesProgressAwareMacroState()
        {
            var gameObject = new GameObject("HotZoneModeTest");
            try
            {
                var mode = gameObject.AddComponent<HotZoneMode>();
                mode.SetProgressForDebug(35f, 55f);

                bool resolved = mode.TryResolveMacroState(
                    TeamType.Blue,
                    out AIGameModeMacroState state);

                Assert.IsTrue(resolved);
                Assert.AreEqual(GameModeId.HotZone, state.Mode);
                Assert.AreEqual(AIGameModeMacroCall.Push, state.Call);
                Assert.AreEqual("contest_zone", state.Reason);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class FakeMacroStateProvider : IAIGameModeMacroStateProvider
        {
            public GameModeId ModeId => GameModeId.HotZone;

            public bool TryResolveMacroState(
                TeamType team,
                out AIGameModeMacroState state)
            {
                state = new AIGameModeMacroState(
                    GameModeId.HotZone,
                    AIGameModeMacroCall.Push,
                    AIGameModeObjectivePhase.Contest,
                    20,
                    40,
                    100,
                    0f,
                    90f,
                    false,
                    true,
                    false,
                    false,
                    "runtime_provider");
                return true;
            }
        }
    }
}
