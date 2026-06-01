using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIObjectiveSlotCommitmentTests
    {
        [Test]
        public void SelectRole_HoldsDefensiveRoleDuringBriefRoleChange()
        {
            var commitment = new AIObjectiveSlotCommitment();

            AIObjectiveSlotRole first = commitment.SelectRole(
                Candidate(AIObjectiveControlState.FriendlyControlled, "ZoneA", 1, 0),
                BrawlerArchetype.Sniper,
                currentTick: 100u,
                out AIObjectiveSlotRole firstDesired);

            AIObjectiveSlotRole held = commitment.SelectRole(
                Candidate(AIObjectiveControlState.EnemyControlled, "ZoneA", 0, 1),
                BrawlerArchetype.Sniper,
                currentTick: 110u,
                out AIObjectiveSlotRole heldDesired);

            AIObjectiveSlotRole switched = commitment.SelectRole(
                Candidate(AIObjectiveControlState.EnemyControlled, "ZoneA", 0, 1),
                BrawlerArchetype.Sniper,
                currentTick: 131u,
                out AIObjectiveSlotRole switchedDesired);

            Assert.AreEqual(AIObjectiveSlotRole.Perimeter, first);
            Assert.AreEqual(AIObjectiveSlotRole.Perimeter, firstDesired);
            Assert.AreEqual(AIObjectiveSlotRole.Perimeter, held);
            Assert.AreEqual(AIObjectiveSlotRole.Pressure, heldDesired);
            Assert.AreEqual(AIObjectiveSlotRole.Pressure, switched);
            Assert.AreEqual(AIObjectiveSlotRole.Pressure, switchedDesired);
        }

        [Test]
        public void SelectRole_UrgentlySwitchesFrontlineToBreaker()
        {
            var commitment = new AIObjectiveSlotCommitment();

            AIObjectiveSlotRole first = commitment.SelectRole(
                Candidate(AIObjectiveControlState.FriendlyControlled, "ZoneA", 1, 0),
                BrawlerArchetype.Tank,
                currentTick: 20u,
                out _);

            AIObjectiveSlotRole urgent = commitment.SelectRole(
                Candidate(AIObjectiveControlState.EnemyControlled, "ZoneA", 0, 1),
                BrawlerArchetype.Tank,
                currentTick: 21u,
                out AIObjectiveSlotRole desired);

            Assert.AreEqual(AIObjectiveSlotRole.Anchor, first);
            Assert.AreEqual(AIObjectiveSlotRole.Breaker, desired);
            Assert.AreEqual(AIObjectiveSlotRole.Breaker, urgent);
        }

        [Test]
        public void SelectRole_ObjectiveChangeStartsNewCommitment()
        {
            var commitment = new AIObjectiveSlotCommitment();

            AIObjectiveSlotRole first = commitment.SelectRole(
                Candidate(AIObjectiveControlState.FriendlyControlled, "ZoneA", 1, 0),
                BrawlerArchetype.Sniper,
                currentTick: 40u,
                out _);

            AIObjectiveSlotRole second = commitment.SelectRole(
                Candidate(AIObjectiveControlState.EnemyControlled, "ZoneB", 0, 1),
                BrawlerArchetype.Sniper,
                currentTick: 41u,
                out AIObjectiveSlotRole desired);

            Assert.AreEqual(AIObjectiveSlotRole.Perimeter, first);
            Assert.AreEqual(AIObjectiveSlotRole.Pressure, desired);
            Assert.AreEqual(AIObjectiveSlotRole.Pressure, second);
        }

        private static AIObjectiveCandidate Candidate(
            AIObjectiveControlState controlState,
            string name,
            int friendlyPresence,
            int enemyPresence)
        {
            return new AIObjectiveCandidate(
                AIObjectiveType.HotZone,
                Vector3.zero,
                80f,
                3.5f,
                name,
                true,
                controlState,
                friendlyPresence,
                enemyPresence);
        }
    }
}
