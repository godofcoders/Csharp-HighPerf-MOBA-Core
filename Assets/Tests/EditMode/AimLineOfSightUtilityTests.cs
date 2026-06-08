using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AimLineOfSightUtilityTests
    {
        [Test]
        public void Trace_ReturnsFullRange_WhenLaneIsClear()
        {
            AStarSolver pathfinder = CreatePathfinder(blockedX: -1);

            AimLineTraceResult result = AimLineOfSightUtility.Trace(
                pathfinder,
                new Vector3(0.5f, 0f, 0.5f),
                Vector3.right,
                3f,
                projectileRadius: 0f);

            Assert.IsFalse(result.IsBlocked);
            Assert.AreEqual(3f, result.ClearDistance, 0.01f);
        }

        [Test]
        public void Trace_StopsBeforeBlockedCell()
        {
            AStarSolver pathfinder = CreatePathfinder(blockedX: 2);

            AimLineTraceResult result = AimLineOfSightUtility.Trace(
                pathfinder,
                new Vector3(0.5f, 0f, 0.5f),
                Vector3.right,
                4f,
                projectileRadius: 0f);

            Assert.IsTrue(result.IsBlocked);
            Assert.Less(result.ClearDistance, 2f);
            Assert.Greater(result.ClearDistance, 1f);
        }

        [Test]
        public void HasLineOfSight_ReturnsFalse_WhenBlockedCellIsBetweenPoints()
        {
            AStarSolver pathfinder = CreatePathfinder(blockedX: 2);

            bool hasLineOfSight = AimLineOfSightUtility.HasLineOfSight(
                pathfinder,
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(4.5f, 0f, 0.5f),
                projectileRadius: 0f);

            Assert.IsFalse(hasLineOfSight);
        }

        private static AStarSolver CreatePathfinder(int blockedX)
        {
            bool[,] walkable = new bool[6, 1];
            for (int x = 0; x < walkable.GetLength(0); x++)
                walkable[x, 0] = x != blockedX;

            return new AStarSolver(walkable, 1f, Vector3.zero);
        }
    }
}
