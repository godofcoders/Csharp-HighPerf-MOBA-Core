using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class ShowdownPoisonAvoidanceUtilityTests
    {
        [Test]
        public void CalculateActionPenalty_DoesNotPenalizeSafeInteriorActions()
        {
            float penalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Approach,
                selfDistanceBeyondSafeZone: 0f,
                selfEdgeDistance: 8f,
                dangerBuffer: 2.8f,
                hasLiveTarget: true,
                targetDistanceBeyondSafeZone: 0f);

            Assert.That(penalty, Is.EqualTo(0f));
        }

        [Test]
        public void CalculateActionPenalty_StronglyRejectsPoisonPursuit()
        {
            float approachPenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Approach,
                selfDistanceBeyondSafeZone: 0f,
                selfEdgeDistance: 1f,
                dangerBuffer: 2.8f,
                hasLiveTarget: true,
                targetDistanceBeyondSafeZone: 2f);
            float retreatPenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Retreat,
                selfDistanceBeyondSafeZone: 0f,
                selfEdgeDistance: 1f,
                dangerBuffer: 2.8f,
                hasLiveTarget: true,
                targetDistanceBeyondSafeZone: 2f);

            Assert.That(approachPenalty, Is.LessThan(-60f));
            Assert.That(retreatPenalty, Is.EqualTo(0f));
        }

        [Test]
        public void CalculateActionPenalty_PreservesEscapeChoicesWhenAlreadyOutside()
        {
            float wanderPenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Wander,
                selfDistanceBeyondSafeZone: 1.5f,
                selfEdgeDistance: -1.5f,
                dangerBuffer: 2.8f,
                hasLiveTarget: false,
                targetDistanceBeyondSafeZone: 0f);
            float objectivePenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Objective,
                selfDistanceBeyondSafeZone: 1.5f,
                selfEdgeDistance: -1.5f,
                dangerBuffer: 2.8f,
                hasLiveTarget: false,
                targetDistanceBeyondSafeZone: 0f);
            float evadePenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Evade,
                selfDistanceBeyondSafeZone: 1.5f,
                selfEdgeDistance: -1.5f,
                dangerBuffer: 2.8f,
                hasLiveTarget: false,
                targetDistanceBeyondSafeZone: 0f);

            Assert.That(wanderPenalty, Is.LessThan(-75f));
            Assert.That(objectivePenalty, Is.EqualTo(0f));
            Assert.That(evadePenalty, Is.EqualTo(0f));
        }

        [Test]
        public void CalculateActionPenalty_DiscouragesIdleRoamingNearBoundary()
        {
            float wanderPenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Wander,
                selfDistanceBeyondSafeZone: 0f,
                selfEdgeDistance: 2f,
                dangerBuffer: 2.8f,
                hasLiveTarget: false,
                targetDistanceBeyondSafeZone: 0f);
            float searchPenalty = ShowdownPoisonAvoidanceUtility.CalculateActionPenalty(
                AIActionType.Search,
                selfDistanceBeyondSafeZone: 0f,
                selfEdgeDistance: 2f,
                dangerBuffer: 2.8f,
                hasLiveTarget: false,
                targetDistanceBeyondSafeZone: 0f);

            Assert.That(wanderPenalty, Is.EqualTo(-36f));
            Assert.That(searchPenalty, Is.EqualTo(-30f));
        }
    }
}
