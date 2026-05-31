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

        private static AIGameModeMacroState MacroState(
            GameModeId mode,
            AIGameModeMacroCall call)
        {
            return new AIGameModeMacroState(
                mode,
                call,
                AIGameModeObjectivePhase.Contest,
                0,
                0,
                10,
                0f,
                90f,
                false,
                false,
                false,
                false,
                "test");
        }
    }
}
