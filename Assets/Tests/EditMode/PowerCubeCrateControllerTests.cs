using System.Collections.Generic;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MOBA.Tests.EditMode
{
    public class PowerCubeCrateControllerTests
    {
        private GameObject _crateObject;
        private readonly List<GameObject> _effects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (_crateObject != null)
                Object.DestroyImmediate(_crateObject);

            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null)
                    Object.DestroyImmediate(_effects[i]);
            }

            _effects.Clear();
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

        [Test]
        public void BreakBurst_UsesWorldSpaceParticlesAndSelfCleaningStopAction()
        {
            GameObject effect = PowerCubeCrateVfx.CreateBreakBurst(new Vector3(2f, 1f, 3f));
            _effects.Add(effect);

            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;

            Assert.That(effect.name, Is.EqualTo("PowerCubeChestBreakVFX"));
            Assert.That(effect.transform.position, Is.EqualTo(new Vector3(2f, 1f, 3f)));
            Assert.That(main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(main.stopAction, Is.EqualTo(ParticleSystemStopAction.Destroy));
            Assert.That(main.gravityModifier.constant, Is.GreaterThan(0f));
            Assert.That(particles.particleCount, Is.GreaterThan(0));
            Assert.That(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null);
        }

        [Test]
        public void RevealBurst_UsesDistinctRisingParticleTreatment()
        {
            GameObject effect = PowerCubeCrateVfx.CreateRevealBurst(Vector3.zero);
            _effects.Add(effect);

            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;

            Assert.That(effect.name, Is.EqualTo("PowerCubeRevealVFX"));
            Assert.That(main.gravityModifier.constant, Is.LessThan(0f));
            Assert.That(particles.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.Sphere));
            Assert.That(particles.sizeOverLifetime.enabled, Is.True);
            Assert.That(particles.particleCount, Is.GreaterThan(0));
        }
    }
}
