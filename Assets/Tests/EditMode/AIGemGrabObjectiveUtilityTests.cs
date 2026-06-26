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

        [Test]
        public void EvaluateCandidate_ValuesSwingPickupThatTakesGemLead()
        {
            AIGemPickupEvaluation evaluation = AIGemGrabObjectiveUtility.EvaluateCandidate(
                _profile,
                new AIGemPickupCandidateContext(
                    0,
                    4,
                    5,
                    10,
                    1,
                    2,
                    5f,
                    11f,
                    1f,
                    0.4f,
                    false,
                    false,
                    AIGameModeMacroCall.Neutral,
                    _profile.GemPickupMinimumScore));

            Assert.IsTrue(evaluation.ShouldPickup);
            Assert.IsTrue(evaluation.Reason.Contains("swing"));
        }

        [Test]
        public void EvaluateCandidate_AddsUrgentResetPressure_WhenEnemyCountdownIsLate()
        {
            AIGemPickupEvaluation evaluation = AIGemGrabObjectiveUtility.EvaluateCandidate(
                _profile,
                new AIGemPickupCandidateContext(
                    0,
                    6,
                    10,
                    10,
                    1,
                    1,
                    6f,
                    11f,
                    1f,
                    1.6f,
                    false,
                    true,
                    AIGameModeMacroCall.Reset,
                    _profile.GemPickupMinimumScore,
                    winTimerRemainingSeconds: 2f));

            Assert.IsTrue(evaluation.ShouldPickup);
            Assert.IsTrue(evaluation.IsDenyPickup);
            Assert.IsTrue(evaluation.Reason.Contains("urgent_reset"));
        }

        [Test]
        public void EvaluateMineControl_EnemyCountdownBoostsObjectiveRetake()
        {
            AIGemMineControlEvaluation evaluation =
                AIGemGrabObjectiveUtility.EvaluateMineControl(
                    AIActionType.Objective,
                    new AIGemMineControlContext(
                        Macro(
                            AIGameModeMacroCall.Reset,
                            ownGems: 6,
                            enemyGems: 10,
                            enemyCountdown: true,
                            timer: 2f),
                        selfCarriedGems: 0,
                        healthRatio: 1f,
                        allyPressure: 0.5f,
                        hasLiveTarget: false,
                        hasGemPickup: false,
                        shouldPickupGem: false,
                        gemPickupScore: 0f));

            Assert.Greater(evaluation.Delta, 30f);
            Assert.AreEqual("reset_countdown_mine", evaluation.Reason);
        }

        [Test]
        public void EvaluateMineControl_CarrierWithCountdownAvoidsMineGreed()
        {
            AIGemMineControlEvaluation evaluation =
                AIGemGrabObjectiveUtility.EvaluateMineControl(
                    AIActionType.Objective,
                    new AIGemMineControlContext(
                        Macro(
                            AIGameModeMacroCall.Hold,
                            ownGems: 10,
                            enemyGems: 7,
                            ownCountdown: true,
                            timer: 5f),
                        selfCarriedGems: 5,
                        healthRatio: 0.7f,
                        allyPressure: 0f,
                        hasLiveTarget: false,
                        hasGemPickup: true,
                        shouldPickupGem: true,
                        gemPickupScore: 80f));

            Assert.Less(evaluation.Delta, 0f);
            Assert.AreEqual("carrier_countdown_safety", evaluation.Reason);
        }

        private static AIGameModeMacroState Macro(
            AIGameModeMacroCall call,
            int ownGems,
            int enemyGems,
            bool ownCountdown = false,
            bool enemyCountdown = false,
            float timer = 0f)
        {
            return new AIGameModeMacroState(
                call,
                AIGameModeObjectivePhase.Contest,
                ownGems,
                enemyGems,
                10,
                timer,
                90f,
                ownGems > enemyGems,
                enemyGems > ownGems,
                ownCountdown,
                enemyCountdown);
        }
    }
}
