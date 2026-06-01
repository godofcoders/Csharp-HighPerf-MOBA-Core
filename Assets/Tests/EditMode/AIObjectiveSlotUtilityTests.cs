using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIObjectiveSlotUtilityTests
    {
        [Test]
        public void GetObjectiveSlotPosition_ScalesOffsetWithObjectiveRadius()
        {
            Vector3 center = Vector3.zero;

            Vector3 smallSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Tank,
                2,
                center,
                objectiveRadius: 1f);

            Vector3 largeSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Tank,
                2,
                center,
                objectiveRadius: 5f);

            Assert.Greater(largeSlot.magnitude, smallSlot.magnitude);
        }

        [Test]
        public void GetObjectiveSlotPosition_SplitsSideByStableEntityId()
        {
            Vector3 center = Vector3.zero;

            Vector3 evenSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Fighter,
                2,
                center,
                objectiveRadius: 3f);

            Vector3 oddSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Fighter,
                3,
                center,
                objectiveRadius: 3f);

            Assert.Greater(evenSlot.x, 0f);
            Assert.Less(oddSlot.x, 0f);
            Assert.AreEqual(evenSlot.z, oddSlot.z);
        }

        [Test]
        public void GetObjectiveSlotPosition_EnemyControlledMovesFrontlineIntoBreakerRole()
        {
            Vector3 center = Vector3.zero;

            Vector3 defaultSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Tank,
                2,
                center,
                objectiveRadius: 3f);

            Vector3 breakerSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Tank,
                2,
                center,
                objectiveRadius: 3f,
                AIObjectiveControlState.EnemyControlled,
                friendlyPresence: 0,
                enemyPresence: 1);

            Assert.AreEqual(AIObjectiveSlotRole.Breaker,
                AIObjectiveSlotUtility.GetObjectiveSlotRole(
                    BrawlerArchetype.Tank,
                    AIObjectiveControlState.EnemyControlled,
                    friendlyPresence: 0,
                    enemyPresence: 1));
            Assert.Greater(breakerSlot.z, defaultSlot.z);
        }

        [Test]
        public void GetObjectiveSlotPosition_FriendlyControlledKeepsBacklineOnPerimeter()
        {
            Vector3 center = Vector3.zero;

            Vector3 defaultSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Sniper,
                2,
                center,
                objectiveRadius: 3f);

            Vector3 perimeterSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Sniper,
                2,
                center,
                objectiveRadius: 3f,
                AIObjectiveControlState.FriendlyControlled,
                friendlyPresence: 1,
                enemyPresence: 0);

            Assert.AreEqual(AIObjectiveSlotRole.Perimeter,
                AIObjectiveSlotUtility.GetObjectiveSlotRole(
                    BrawlerArchetype.Sniper,
                    AIObjectiveControlState.FriendlyControlled,
                    friendlyPresence: 1,
                    enemyPresence: 0));
            Assert.Less(perimeterSlot.z, defaultSlot.z);
        }

        [Test]
        public void GetObjectiveSlotPosition_FriendlySaturationWidensContestSlots()
        {
            Vector3 center = Vector3.zero;

            Vector3 balancedSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Fighter,
                2,
                center,
                objectiveRadius: 3f,
                AIObjectiveControlState.Contested,
                friendlyPresence: 1,
                enemyPresence: 1);

            Vector3 saturatedSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Fighter,
                2,
                center,
                objectiveRadius: 3f,
                AIObjectiveControlState.Contested,
                friendlyPresence: 3,
                enemyPresence: 1);

            Assert.Greater(saturatedSlot.x, balancedSlot.x);
        }
    }
}
