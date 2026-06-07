using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIIdleHesitationMemoryTests
    {
        [Test]
        public void Evaluate_RecoversAfterLowConfidenceNoDestinationPersists()
        {
            var memory = new AIIdleHesitationMemory();
            AIIdleHesitationContext context = Context(100u);

            AIIdleHesitationDecision watch = memory.Evaluate(context);
            context = Context(112u);
            AIIdleHesitationDecision recover = memory.Evaluate(context);

            Assert.IsTrue(watch.IsHesitating);
            Assert.IsFalse(watch.ShouldRecover);
            Assert.IsTrue(recover.ShouldRecover);
            Assert.AreEqual("no_target_no_destination", recover.Reason);
        }

        [Test]
        public void Evaluate_ResetsWhenDestinationExists()
        {
            var memory = new AIIdleHesitationMemory();

            memory.Evaluate(Context(100u));
            AIIdleHesitationDecision destination =
                memory.Evaluate(Context(112u, hasDestination: true));
            AIIdleHesitationDecision freshWatch =
                memory.Evaluate(Context(113u));

            Assert.IsFalse(destination.IsHesitating);
            Assert.IsFalse(destination.ShouldRecover);
            Assert.IsTrue(freshWatch.IsHesitating);
            Assert.IsFalse(freshWatch.ShouldRecover);
            Assert.AreEqual(0u, freshWatch.ElapsedTicks);
        }

        [Test]
        public void Evaluate_DoesNotRecoverDuringCombatOrEmergencyAction()
        {
            var memory = new AIIdleHesitationMemory();

            AIIdleHesitationDecision combat =
                memory.Evaluate(Context(100u, hasLiveTarget: true));
            AIIdleHesitationDecision retreat =
                memory.Evaluate(
                    Context(
                        112u,
                        actionType: AIActionType.Retreat,
                        actionScore: 70f));

            Assert.IsFalse(combat.ShouldRecover);
            Assert.AreEqual("combat", combat.Reason);
            Assert.IsFalse(retreat.ShouldRecover);
            Assert.AreEqual("emergency_action", retreat.Reason);
        }

        [Test]
        public void Evaluate_RespectsCooldownAfterRecovery()
        {
            var memory = new AIIdleHesitationMemory();

            memory.Evaluate(Context(100u));
            AIIdleHesitationDecision first = memory.Evaluate(Context(112u));
            AIIdleHesitationDecision cooldown = memory.Evaluate(Context(113u));

            Assert.IsTrue(first.ShouldRecover);
            Assert.IsFalse(cooldown.ShouldRecover);
            Assert.AreEqual("cooldown", cooldown.Reason);
        }

        [Test]
        public void Evaluate_RecoversWhenConfidentActionProducedNoDestination()
        {
            var memory = new AIIdleHesitationMemory();

            memory.Evaluate(
                Context(
                    100u,
                    actionType: AIActionType.Objective,
                    actionScore: 90f));
            AIIdleHesitationDecision recover = memory.Evaluate(
                Context(
                    112u,
                    actionType: AIActionType.Objective,
                    actionScore: 90f));

            Assert.IsTrue(recover.ShouldRecover);
            Assert.AreEqual("no_target_no_destination", recover.Reason);
        }

        private static AIIdleHesitationContext Context(
            uint tick,
            bool hasLiveTarget = false,
            bool hasRecentTargetMemory = false,
            bool hasDestination = false,
            bool hasDanger = false,
            AIActionType actionType = AIActionType.Wander,
            float actionScore = 5f)
        {
            return new AIIdleHesitationContext(
                tick,
                new AIActionScore(actionType, actionScore),
                hasLiveTarget,
                hasRecentTargetMemory,
                hasDestination,
                hasDanger,
                recoveryTicks: 12u,
                cooldownTicks: 24u,
                lowScoreThreshold: 8f);
        }
    }
}
