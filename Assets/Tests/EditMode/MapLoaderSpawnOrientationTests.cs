using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class MapLoaderSpawnOrientationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void NormalizeTeamSpawnOrientation_SwapsBlueToBottomWhenAuthoredAtTop()
        {
            List<Transform> blue = new List<Transform>
            {
                Point("BlueRightTop", 1f, 4f),
                Point("BlueLeftTop", -1f, 4f)
            };
            List<Transform> red = new List<Transform>
            {
                Point("RedRightBottom", 1f, -4f),
                Point("RedLeftBottom", -1f, -4f)
            };

            bool swapped = MapLoader.NormalizeTeamSpawnOrientation(blue, red);

            Assert.IsTrue(swapped);
            Assert.Less(GetAverageZ(blue), GetAverageZ(red));
            Assert.AreEqual(-1f, blue[0].position.x);
            Assert.AreEqual(1f, blue[1].position.x);
            Assert.AreEqual(-1f, red[0].position.x);
            Assert.AreEqual(1f, red[1].position.x);
        }

        [Test]
        public void NormalizeTeamSpawnOrientation_KeepsBlueAtBottomWhenAlreadyCorrect()
        {
            List<Transform> blue = new List<Transform>
            {
                Point("BlueRightBottom", 1f, -4f),
                Point("BlueLeftBottom", -1f, -4f)
            };
            List<Transform> red = new List<Transform>
            {
                Point("RedRightTop", 1f, 4f),
                Point("RedLeftTop", -1f, 4f)
            };

            bool swapped = MapLoader.NormalizeTeamSpawnOrientation(blue, red);

            Assert.IsFalse(swapped);
            Assert.Less(GetAverageZ(blue), GetAverageZ(red));
            Assert.AreEqual(-1f, blue[0].position.x);
            Assert.AreEqual(1f, blue[1].position.x);
        }

        private Transform Point(string name, float x, float z)
        {
            GameObject go = new GameObject(name);
            _objects.Add(go);
            go.transform.position = new Vector3(x, 0f, z);
            return go.transform;
        }

        private static float GetAverageZ(List<Transform> points)
        {
            float total = 0f;
            for (int i = 0; i < points.Count; i++)
                total += points[i].position.z;

            return total / points.Count;
        }
    }
}
