using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class ShowdownPoisonAvoidanceUtilityTests
    {
        [Test]
        public void ClampDestinationToSafeZone_ClampsOutsidePointInsideSafetyInset()
        {
            Vector3 destination = ShowdownPoisonAvoidanceUtility.ClampDestinationToSafeZone(
                new Vector3(15f, 2f, -12f),
                new Vector3(2f, 0f, 3f),
                new Vector2(10f, 8f),
                safetyInset: 1f);

            Assert.That(destination, Is.EqualTo(new Vector3(11f, 2f, -4f)));
        }

        [Test]
        public void ClampDestinationToSafeZone_PreservesSafePointAndHeight()
        {
            Vector3 safePoint = new Vector3(4f, 1.75f, -2f);

            Vector3 destination = ShowdownPoisonAvoidanceUtility.ClampDestinationToSafeZone(
                safePoint,
                Vector3.zero,
                new Vector2(10f, 6f),
                safetyInset: 0.8f);

            Assert.That(destination, Is.EqualTo(safePoint));
        }

        [Test]
        public void ClampDestinationToSafeZone_RemainsValidInVerySmallFinalZone()
        {
            Vector3 destination = ShowdownPoisonAvoidanceUtility.ClampDestinationToSafeZone(
                new Vector3(20f, 0f, 20f),
                new Vector3(5f, 0f, -3f),
                new Vector2(0.5f, 0.5f),
                safetyInset: 2f);

            Assert.That(destination.x, Is.EqualTo(5.1f).Within(0.001f));
            Assert.That(destination.z, Is.EqualTo(-2.9f).Within(0.001f));
        }

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
