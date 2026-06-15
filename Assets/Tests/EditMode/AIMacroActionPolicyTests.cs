using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIMacroActionPolicyTests
    {
        [Test]
        public void Evaluate_HotZoneResetBoostsObjectiveDenial()
        {
            AIMacroActionPolicyResult result = AIMacroActionPolicy.Evaluate(
                AIActionType.Objective,
                new AIMacroActionContext(
                    MacroState(GameModeId.HotZone, AIGameModeMacroCall.Reset)));

            Assert.AreEqual(22f, result.Delta);
            Assert.AreEqual("zone_deny", result.Reason);
            Assert.IsTrue(result.HasDelta);
        }

        [Test]
        public void Evaluate_GemCarrierHoldDiscouragesApproachAndEncouragesSafety()
        {
            var context = new AIMacroActionContext(
                MacroState(GameModeId.GemGrab, AIGameModeMacroCall.Hold),
                selfCarriedGems: 4);

            AIMacroActionPolicyResult approach = AIMacroActionPolicy.Evaluate(
                AIActionType.Approach,
                context);

            AIMacroActionPolicyResult retreat = AIMacroActionPolicy.Evaluate(
                AIActionType.Retreat,
                context);

            Assert.AreEqual(-18f, approach.Delta);
            Assert.AreEqual("carrier_hold", approach.Reason);
            Assert.AreEqual(32f, retreat.Delta);
            Assert.AreEqual("carrier_hold", retreat.Reason);
        }

        [Test]
        public void Evaluate_BrawlBallPushCreatesBallPressure()
        {
            var context = new AIMacroActionContext(
                MacroState(GameModeId.BrawlBall, AIGameModeMacroCall.Push));

            AIMacroActionPolicyResult approach = AIMacroActionPolicy.Evaluate(
                AIActionType.Approach,
                context);

            AIMacroActionPolicyResult objective = AIMacroActionPolicy.Evaluate(
                AIActionType.Objective,
                context);

            Assert.AreEqual(18f, approach.Delta);
            Assert.AreEqual("ball_push", approach.Reason);
            Assert.AreEqual(14f, objective.Delta);
            Assert.AreEqual("ball_push", objective.Reason);
        }

        [Test]
        public void Evaluate_ShowdownResetPrioritizesSafeZoneAndRetreat()
        {
            var context = new AIMacroActionContext(
                MacroState(GameModeId.SoloShowdown, AIGameModeMacroCall.Reset));

            AIMacroActionPolicyResult objective = AIMacroActionPolicy.Evaluate(
                AIActionType.Objective,
                context);

            AIMacroActionPolicyResult retreat = AIMacroActionPolicy.Evaluate(
                AIActionType.Retreat,
                context);

            AIMacroActionPolicyResult approach = AIMacroActionPolicy.Evaluate(
                AIActionType.Approach,
                context);

            Assert.AreEqual(24f, objective.Delta);
            Assert.AreEqual("showdown_safe_zone", objective.Reason);
            Assert.AreEqual(20f, retreat.Delta);
            Assert.AreEqual("showdown_survive", retreat.Reason);
            Assert.AreEqual(-18f, approach.Delta);
            Assert.AreEqual("showdown_survive", approach.Reason);
        }

        [Test]
        public void Evaluate_ShowdownPushEncouragesFinalDuel()
        {
            var context = new AIMacroActionContext(
                MacroState(GameModeId.SoloShowdown, AIGameModeMacroCall.Push));

            AIMacroActionPolicyResult approach = AIMacroActionPolicy.Evaluate(
                AIActionType.Approach,
                context);

            AIMacroActionPolicyResult retreat = AIMacroActionPolicy.Evaluate(
                AIActionType.Retreat,
                context);

            Assert.AreEqual(12f, approach.Delta);
            Assert.AreEqual("showdown_duel", approach.Reason);
            Assert.AreEqual(-6f, retreat.Delta);
            Assert.AreEqual("showdown_duel", retreat.Reason);
        }

        [Test]
        public void Evaluate_ResetSuperPressureTargetsGemCarrier()
        {
            AIMacroActionPolicyResult result = AIMacroActionPolicy.Evaluate(
                AIActionType.UseSuper,
                new AIMacroActionContext(
                    MacroState(GameModeId.GemGrab, AIGameModeMacroCall.Reset),
                    targetCarriedGems: 3));

            Assert.AreEqual(18f, result.Delta);
            Assert.AreEqual("carrier_reset", result.Reason);
        }

        [Test]
        public void Evaluate_EnemyCountdownResetBoostsObjectivePressure()
        {
            AIMacroActionPolicyResult result = AIMacroActionPolicy.Evaluate(
                AIActionType.Objective,
                new AIMacroActionContext(
                    MacroState(
                        GameModeId.GemGrab,
                        AIGameModeMacroCall.Reset,
                        enemyCountdown: true,
                        winTimerRemainingSeconds: 3f)));

            Assert.AreEqual(23f, result.Delta, 0.001f);
            Assert.AreEqual("countdown_reset", result.Reason);
        }

        [Test]
        public void Evaluate_EnemyCountdownSuperPressureScalesAgainstCarrier()
        {
            AIMacroActionPolicyResult result = AIMacroActionPolicy.Evaluate(
                AIActionType.UseSuper,
                new AIMacroActionContext(
                    MacroState(
                        GameModeId.GemGrab,
                        AIGameModeMacroCall.Reset,
                        enemyCountdown: true,
                        winTimerRemainingSeconds: 2f),
                    targetCarriedGems: 3));

            Assert.AreEqual(28.8f, result.Delta, 0.001f);
            Assert.AreEqual("countdown_carrier_reset", result.Reason);
        }

        [Test]
        public void Evaluate_OwnCountdownCarrierSafetyScalesWithTimerPressure()
        {
            AIMacroActionPolicyResult result = AIMacroActionPolicy.Evaluate(
                AIActionType.Retreat,
                new AIMacroActionContext(
                    MacroState(
                        GameModeId.GemGrab,
                        AIGameModeMacroCall.Hold,
                        ownCountdown: true,
                        winTimerRemainingSeconds: 4f),
                    selfCarriedGems: 4));

            Assert.AreEqual(57.6f, result.Delta, 0.001f);
            Assert.AreEqual("countdown_carrier_hold", result.Reason);
        }

        private static AIGameModeMacroState MacroState(
            GameModeId mode,
            AIGameModeMacroCall call,
            bool ownCountdown = false,
            bool enemyCountdown = false,
            float winTimerRemainingSeconds = 0f)
        {
            return new AIGameModeMacroState(
                mode,
                call,
                AIGameModeObjectivePhase.Contest,
                0,
                0,
                10,
                winTimerRemainingSeconds,
                90f,
                false,
                false,
                ownCountdown,
                enemyCountdown,
                "test");
        }
    }
}
