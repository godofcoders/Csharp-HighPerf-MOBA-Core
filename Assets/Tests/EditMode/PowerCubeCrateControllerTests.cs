using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MOBA.Tests.EditMode
{
    public class PowerCubeCrateControllerTests
    {
        private GameObject _crateObject;

        [TearDown]
        public void TearDown()
        {
            if (_crateObject != null)
                Object.DestroyImmediate(_crateObject);
        }

        [Test]
        public void BuildFallbackPresentation_CreatesCompactChestWithMatchedCollider()
        {
            _crateObject = new GameObject("PowerCubeChestTest");
            PowerCubeCrateController crate =
                _crateObject.AddComponent<PowerCubeCrateController>();

            crate.BuildFallbackPresentation();

            Transform visual = _crateObject.transform.Find("ChestVisual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Find("Body"), Is.Not.Null);
            Assert.That(visual.Find("Lid"), Is.Not.Null);
            Assert.That(visual.Find("BandLeft"), Is.Not.Null);
            Assert.That(visual.Find("BandRight"), Is.Not.Null);
            Assert.That(visual.Find("Latch"), Is.Not.Null);

            BoxCollider collider = _crateObject.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.size.x, Is.LessThan(1f));
            Assert.That(collider.size.y, Is.LessThan(0.8f));
            Assert.That(crate.CollisionRadius, Is.LessThan(0.65f));
            Assert.That(visual.GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void BuildFallbackPresentation_IsIdempotent()
        {
            _crateObject = new GameObject("PowerCubeChestTest");
            PowerCubeCrateController crate =
                _crateObject.AddComponent<PowerCubeCrateController>();

            crate.BuildFallbackPresentation();
            crate.BuildFallbackPresentation();

            Transform visual = _crateObject.transform.Find("ChestVisual");
            Assert.That(visual.childCount, Is.EqualTo(5));
            Assert.That(_crateObject.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
        }
    }
}
