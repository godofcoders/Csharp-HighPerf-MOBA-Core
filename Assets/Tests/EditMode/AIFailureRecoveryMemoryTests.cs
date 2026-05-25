using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIFailureRecoveryMemoryTests
    {
        [Test]
        public void TryCreateNavigationRecovery_RequiresStrongSignalAndRespectsCooldown()
        {
            AIFailureRecoveryMemory memory = new AIFailureRecoveryMemory();
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.EnableFailureRecovery = true;
            profile.NavigationStuckSampleLimit = 2;
            profile.FailureRecoveryCooldownTicks = 10;

            AIFailureRecoverySignal weakSignal = new AIFailureRecoverySignal
            {
                Reason = AIFailureRecoveryReason.NavigationStall,
                Tick = 100u,
                ConsecutiveCount = 1,
                Destination = Vector3.forward,
                DistanceToDestination = 3f
            };

            Assert.IsFalse(memory.TryCreateNavigationRecovery(
                weakSignal,
                profile,
                100u,
                out _));

            AIFailureRecoverySignal strongSignal = weakSignal;
            strongSignal.ConsecutiveCount = 2;

            Assert.IsTrue(memory.TryCreateNavigationRecovery(
                strongSignal,
                profile,
                101u,
                out AIFailureRecoveryRequest firstRequest));
            Assert.AreEqual(AIFailureRecoveryReason.NavigationStall, firstRequest.Reason);

            Assert.IsFalse(memory.TryCreateNavigationRecovery(
                strongSignal,
                profile,
                105u,
                out _));

            Assert.IsTrue(memory.TryCreateNavigationRecovery(
                strongSignal,
                profile,
                111u,
                out AIFailureRecoveryRequest secondRequest));
            Assert.AreNotEqual(firstRequest.SideSign, secondRequest.SideSign);
        }

        [Test]
        public void RecordAbilityResult_SuppressesRepeatedFailedCastsAndClearsOnSuccess()
        {
            AIFailureRecoveryMemory memory = new AIFailureRecoveryMemory();
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.EnableFailureRecovery = true;
            profile.FailedCastRecoveryLimit = 2;
            profile.FailedCastMemoryTicks = 30;
            profile.FailedCastSuppressionTicks = 20;

            memory.RecordAbilityResult(
                AbilitySlotType.Super,
                false,
                50u,
                profile);

            Assert.IsFalse(memory.IsAbilitySuppressed(AbilitySlotType.Super, 50u));

            memory.RecordAbilityResult(
                AbilitySlotType.Super,
                false,
                55u,
                profile);

            Assert.IsTrue(memory.IsAbilitySuppressed(AbilitySlotType.Super, 60u));
            Assert.IsFalse(memory.IsAbilitySuppressed(AbilitySlotType.Super, 76u));

            memory.RecordAbilityResult(
                AbilitySlotType.Super,
                false,
                80u,
                profile);

            memory.RecordAbilityResult(
                AbilitySlotType.Super,
                true,
                81u,
                profile);

            Assert.IsFalse(memory.IsAbilitySuppressed(AbilitySlotType.Super, 82u));
        }

        [Test]
        public void TryCreateNavigationRecovery_StaleDestinationRequiresLowProgress()
        {
            AIFailureRecoveryMemory memory = new AIFailureRecoveryMemory();
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.EnableFailureRecovery = true;
            profile.StaleDestinationRecoveryTicks = 60;
            profile.StaleDestinationProgressThreshold = 0.5f;

            AIFailureRecoverySignal progressingSignal = new AIFailureRecoverySignal
            {
                Reason = AIFailureRecoveryReason.StaleDestination,
                Tick = 90u,
                ConsecutiveCount = 1,
                DestinationAgeTicks = 90u,
                ProgressDistance = 1.2f,
                Destination = Vector3.forward,
                DistanceToDestination = 5f
            };

            Assert.IsFalse(memory.TryCreateNavigationRecovery(
                progressingSignal,
                profile,
                90u,
                out _));

            progressingSignal.ProgressDistance = 0.25f;

            Assert.IsTrue(memory.TryCreateNavigationRecovery(
                progressingSignal,
                profile,
                90u,
                out AIFailureRecoveryRequest request));
            Assert.AreEqual(AIFailureRecoveryReason.StaleDestination, request.Reason);
        }
    }
}
