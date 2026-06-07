using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIIntentValidationUtilityTests
    {
        [Test]
        public void ValidateGemPickupIntent_RejectsPickupOutsideSearchEnvelope()
        {
            AIGemPickupDecision decision = new AIGemPickupDecision(
                true,
                true,
                new Vector3(4f, 0f, 0f),
                1,
                1,
                1f,
                50f,
                0f,
                true,
                false,
                false,
                "test");

            AIIntentValidationResult result =
                AIIntentValidationUtility.ValidateGemPickupIntent(
                    decision,
                    Vector3.zero,
                    null,
                    null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("gem_outside_search", result.Reason);
        }

        [Test]
        public void ValidateObjectiveIntent_RejectsNoneObjective()
        {
            AIObjectiveCandidate objective = new AIObjectiveCandidate(
                AIObjectiveType.None,
                Vector3.zero,
                1f,
                1f,
                "None",
                false);

            AIIntentValidationResult result =
                AIIntentValidationUtility.ValidateObjectiveIntent(
                    objective,
                    null,
                    null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("objective_none", result.Reason);
        }
    }
}
