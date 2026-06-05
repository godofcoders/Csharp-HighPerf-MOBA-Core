using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIObjectiveIntentArbitrationUtilityTests
    {
        [Test]
        public void Evaluate_CarrierSafetySuppressesObjectiveAndApproach()
        {
            AIObjectiveIntentContext context = Context(
                Macro(AIGameModeMacroCall.Hold, ownCountdown: true),
                selfCarriedGems: 6,
                isCarrierPlaybook: true,
                selfIsCarrierAnchor: true);

            AIObjectiveIntentArbitrationResult objective =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Objective,
                    context);
            AIObjectiveIntentArbitrationResult retreat =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Retreat,
                    context);

            Assert.Less(objective.Delta, 0f);
            Assert.AreEqual("carrier_safety", objective.Reason);
            Assert.Greater(retreat.Delta, 0f);
            Assert.AreEqual("carrier_safety", retreat.Reason);
        }

        [Test]
        public void Evaluate_EnemyCountdownResetBoostsNonCarrierObjectivePressure()
        {
            AIObjectiveIntentContext context = Context(
                Macro(AIGameModeMacroCall.Reset, enemyCountdown: true));

            AIObjectiveIntentArbitrationResult search =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Search,
                    context);
            AIObjectiveIntentArbitrationResult retreat =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Retreat,
                    context);

            Assert.Greater(search.Delta, 0f);
            Assert.AreEqual("countdown_reset", search.Reason);
            Assert.Less(retreat.Delta, 0f);
        }

        [Test]
        public void Evaluate_GemPickupPrioritizesSearchOverWander()
        {
            AIObjectiveIntentContext context = Context(
                Macro(AIGameModeMacroCall.Neutral),
                hasGemPickup: true,
                shouldPickupGem: true,
                gemPickupScore: 80f);

            AIObjectiveIntentArbitrationResult search =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Search,
                    context);
            AIObjectiveIntentArbitrationResult wander =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Wander,
                    context);

            Assert.Greater(search.Delta, 0f);
            Assert.AreEqual("gem_pickup", search.Reason);
            Assert.Less(wander.Delta, 0f);
        }

        [Test]
        public void Evaluate_LaneHoldDoesNotFightCountdownReset()
        {
            AIObjectiveIntentContext context = Context(
                Macro(AIGameModeMacroCall.Reset, enemyCountdown: true),
                hasLaneHold: true);

            AIObjectiveIntentArbitrationResult search =
                AIObjectiveIntentArbitrationUtility.Evaluate(
                    AIActionType.Search,
                    context);

            Assert.AreEqual("countdown_reset", search.Reason);
            Assert.AreEqual(18f, search.Delta);
        }

        private static AIObjectiveIntentContext Context(
            AIGameModeMacroState macroState,
            int selfCarriedGems = 0,
            bool hasLiveTarget = false,
            bool hasLaneHold = false,
            bool hasGemPickup = false,
            bool shouldPickupGem = false,
            float gemPickupScore = 0f,
            bool isCarrierPlaybook = false,
            bool selfIsCarrierAnchor = false)
        {
            return new AIObjectiveIntentContext(
                macroState,
                selfCarriedGems,
                hasLiveTarget,
                hasLaneHold,
                hasGemPickup,
                shouldPickupGem,
                gemPickupScore,
                isCarrierPlaybook,
                selfIsCarrierAnchor);
        }

        private static AIGameModeMacroState Macro(
            AIGameModeMacroCall call,
            bool ownCountdown = false,
            bool enemyCountdown = false)
        {
            return new AIGameModeMacroState(
                GameModeId.GemGrab,
                call,
                AIGameModeObjectivePhase.Contest,
                0,
                0,
                10,
                0f,
                90f,
                false,
                false,
                ownCountdown,
                enemyCountdown,
                "test");
        }
    }
}
