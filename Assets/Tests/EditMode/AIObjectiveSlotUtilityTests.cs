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
                2u,
                center,
                objectiveRadius: 1f);

            Vector3 largeSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Tank,
                2u,
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
                2u,
                center,
                objectiveRadius: 3f);

            Vector3 oddSlot = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                TeamType.Blue,
                BrawlerArchetype.Fighter,
                3u,
                center,
                objectiveRadius: 3f);

            Assert.Greater(evenSlot.x, 0f);
            Assert.Less(oddSlot.x, 0f);
            Assert.AreEqual(evenSlot.z, oddSlot.z);
        }
    }
}
