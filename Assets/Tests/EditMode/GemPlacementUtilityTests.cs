using System.Collections.Generic;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class GemPlacementUtilityTests
    {
        [Test]
        public void GetClusterOffset_FirstRingKeepsReadableSpacing()
        {
            const float spacing = 0.82f;

            for (int i = 1; i <= 6; i++)
            {
                Vector3 current = GemPlacementUtility.GetClusterOffset(i, spacing);
                Vector3 next = GemPlacementUtility.GetClusterOffset(i == 6 ? 1 : i + 1, spacing);

                Assert.GreaterOrEqual(XZDistance(current, next), spacing * 0.98f);
            }
        }

        [Test]
        public void ResolveReadablePosition_SkipsReservedCenter()
        {
            var reserved = new List<Vector3>
            {
                Vector3.zero
            };

            Vector3 position = GemPlacementUtility.ResolveReadablePosition(
                Vector3.zero,
                0,
                0.82f,
                null,
                reserved);

            Assert.GreaterOrEqual(XZDistance(position, Vector3.zero), 0.82f * 0.98f);
        }

        [Test]
        public void ResolveReadablePosition_PreservesRequestedHeight()
        {
            Vector3 center = new Vector3(2f, 1.25f, -3f);
            Vector3 position = GemPlacementUtility.ResolveReadablePosition(
                center,
                3,
                0.82f,
                null,
                null);

            Assert.AreEqual(center.y, position.y, 0.0001f);
        }

        private static float XZDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
