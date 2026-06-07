using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AITacticalStopPolicyTests
    {
        [Test]
        public void Evaluate_RejectsIllegalStopImmediately()
        {
            AITacticalStopDecision decision = AITacticalStopPolicy.Evaluate(
                false,
                20u,
                10u,
                30u,
                "none_action");

            Assert.IsFalse(decision.CanHoldStop);
            Assert.IsTrue(decision.ShouldAbandon);
            Assert.AreEqual("illegal_none_action", decision.Reason);
        }

        [Test]
        public void Evaluate_AbandonsLegalStopAfterMaxHold()
        {
            AITacticalStopDecision decision = AITacticalStopPolicy.Evaluate(
                true,
                20u,
                10u,
                10u,
                "hold_position");

            Assert.IsFalse(decision.CanHoldStop);
            Assert.IsTrue(decision.ShouldAbandon);
            Assert.AreEqual("max_hold_hold_position", decision.Reason);
        }
    }
}
