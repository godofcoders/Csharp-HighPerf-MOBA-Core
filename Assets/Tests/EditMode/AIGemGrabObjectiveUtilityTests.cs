using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIGemGrabObjectiveUtilityTests
    {
        private BrawlerAIProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            _profile.ApplyArchetypeDefaults(BrawlerArchetype.Fighter);
        }

        [TearDown]
        public void TearDown()
        {
            if (_profile != null)
                Object.DestroyImmediate(_profile);
        }

        [Test]
        public void EvaluateCandidate_SecuresThresholdPickup_EvenWhenContested()
        {
            AIGemPickupEvaluation evaluation = AIGemGrabObjectiveUtility.EvaluateCandidate(
                _profile,
                new AIGemPickupCandidateContext(
                    0,
                    8,
                    6,
                    10,
                    1,
                    2,
                    4f,
                    11f,
                    1f,
                    1f,
                    false,
                    false,
                    AIGameModeMacroCall.Neutral,
                    _profile.GemPickupMinimumScore));

            Assert.IsTrue(evaluation.ShouldPickup);
            Assert.IsTrue(evaluation.IsThresholdPickup);
            Assert.AreEqual("base|cluster|secure", evaluation.Reason);
        }

        [Test]
        public void EvaluateCandidate_DeniesRiskyCarrierPickup_DuringOwnCountdown()
        {
            AIGemPickupEvaluation evaluation = AIGemGrabObjectiveUtility.EvaluateCandidate(
                _profile,
                new AIGemPickupCandidateContext(
                    7,
                    10,
                    6,
                    10,
                    1,
                    1,
                    5f,
                    11f,
                    0.35f,
                    1f,
                    true,
                    false,
                    AIGameModeMacroCall.Hold,
                    _profile.GemPickupMinimumScore));

            Assert.IsFalse(evaluation.ShouldPickup);
            Assert.IsFalse(evaluation.IsSafe);
            Assert.IsTrue(evaluation.Reason.Contains("carrier_risk"));
        }

        [Test]
        public void EvaluateCandidate_PrioritizesCountdownResetDenial()
        {
            AIGemPickupEvaluation evaluation = AIGemGrabObjectiveUtility.EvaluateCandidate(
                _profile,
                new AIGemPickupCandidateContext(
                    0,
                    5,
                    10,
                    10,
                    1,
                    1,
                    6f,
                    11f,
                    1f,
                    1f,
                    false,
                    true,
                    AIGameModeMacroCall.Reset,
                    _profile.GemPickupMinimumScore));

            Assert.IsTrue(evaluation.ShouldPickup);
            Assert.IsTrue(evaluation.IsDenyPickup);
            Assert.IsTrue(evaluation.Reason.Contains("reset_countdown"));
        }
    }
}
