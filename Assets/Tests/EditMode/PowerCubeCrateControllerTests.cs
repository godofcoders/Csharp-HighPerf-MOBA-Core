using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
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
        public void BuildFallbackPresentation_CreatesCompactHealthBarAboveChest()
        {
            _crateObject = new GameObject("PowerCubeChestTest");
            PowerCubeCrateController crate =
                _crateObject.AddComponent<PowerCubeCrateController>();

            crate.BuildFallbackPresentation();

            Transform bar = _crateObject.transform.Find(
                PowerCubeCrateHealthBarView.RootName);
            Assert.That(bar, Is.Not.Null);
            Assert.That(bar.localPosition.y, Is.GreaterThan(0.75f));
            Assert.That(bar.localScale.x, Is.LessThan(0.006f));
            Assert.That(bar.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(bar.Find("Frame"), Is.Not.Null);
            Assert.That(bar.Find("Background/Fill"), Is.Not.Null);
        }

        [Test]
        public void HealthBar_SetHealthRatioShrinksFillAndShowsCriticalColor()
        {
            _crateObject = new GameObject("PowerCubeChestTest");
            PowerCubeCrateController crate =
                _crateObject.AddComponent<PowerCubeCrateController>();
            crate.BuildFallbackPresentation();

            PowerCubeCrateHealthBarView view =
                _crateObject.GetComponentInChildren<PowerCubeCrateHealthBarView>(true);
            RectTransform fill = _crateObject.transform
                .Find("ChestHealthBar/Background/Fill")
                .GetComponent<RectTransform>();
            float fullWidth = fill.rect.width;

            view.SetHealthRatio(0.20f);

            Assert.That(view.DisplayedHealthRatio, Is.EqualTo(0.20f).Within(0.001f));
            Assert.That(fill.rect.width, Is.EqualTo(fullWidth * 0.20f).Within(0.01f));
            Assert.That(fill.GetComponent<Image>().color.r,
                Is.GreaterThan(fill.GetComponent<Image>().color.g));
        }

        [Test]
        public void AimPriorityScore_PrefersNearestInRangePowerCubeChest()
        {
            _crateObject = new GameObject("NearPowerCubeChest");
            _crateObject.transform.position = new Vector3(2f, 0f, 0f);
            PowerCubeCrateController near =
                _crateObject.AddComponent<PowerCubeCrateController>();

            GameObject farObject = new GameObject("FarPowerCubeChest");
            farObject.transform.position = new Vector3(5f, 0f, 0f);
            _effects.Add(farObject);
            PowerCubeCrateController far =
                farObject.AddComponent<PowerCubeCrateController>();

            float nearScore = ShowdownCrateTargetUtility.CalculateAimPriorityScore(
                near,
                Vector3.zero,
                maxRange: 8f);
            float farScore = ShowdownCrateTargetUtility.CalculateAimPriorityScore(
                far,
                Vector3.zero,
                maxRange: 8f);

            Assert.That(nearScore, Is.GreaterThan(farScore));
        }

        [Test]
        public void AimPriorityScore_RejectsChestOutsideAttackRange()
        {
            _crateObject = new GameObject("OutOfRangePowerCubeChest");
            _crateObject.transform.position = new Vector3(12f, 0f, 0f);
            PowerCubeCrateController crate =
                _crateObject.AddComponent<PowerCubeCrateController>();

            float score = ShowdownCrateTargetUtility.CalculateAimPriorityScore(
                crate,
                Vector3.zero,
                maxRange: 6f);

            Assert.That(score, Is.EqualTo(float.MinValue));
        }

        [Test]
        public void AITargetBonus_PrefersNearbyDamagedChestWhenPowerCubeCountIsLow()
        {
            float usefulChest = ShowdownCrateTargetUtility.CalculateAITargetBonus(
                distance: 3f,
                attackRange: 7f,
                crateHealthRatio: 0.35f,
                selfHealthRatio: 1f,
                powerCubeCount: 0,
                isCurrentTarget: false);
            float lowValueChest = ShowdownCrateTargetUtility.CalculateAITargetBonus(
                distance: 9f,
                attackRange: 7f,
                crateHealthRatio: 1f,
                selfHealthRatio: 1f,
                powerCubeCount: 6,
                isCurrentTarget: false);

            Assert.That(usefulChest, Is.GreaterThan(lowValueChest));
        }

        [Test]
        public void EnemyThreatSuppression_InterruptsChestFarmingInsideDangerRadius()
        {
            Assert.That(
                ShowdownCrateTargetUtility.ShouldSuppressForEnemyThreat(
                    nearestEnemyDistance: 4f,
                    attackRange: 7f),
                Is.True);
            Assert.That(
                ShowdownCrateTargetUtility.ShouldSuppressForEnemyThreat(
                    nearestEnemyDistance: 10f,
                    attackRange: 7f),
                Is.False);
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
