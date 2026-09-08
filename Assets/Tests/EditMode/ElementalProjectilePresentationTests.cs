using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class ElementalProjectilePresentationTests
    {
        private const string Profiles = "Assets/_Game/Data/Presentation/Projectiles/";
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
                if (item != null) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [TestCase(Profiles + "Colt_MainProjectile_Presentation.asset", BrawlerElementType.Fire)]
        [TestCase(Profiles + "Colt_SuperProjectile_Presentation.asset", BrawlerElementType.Fire)]
        [TestCase(Profiles + "Byron_MainProjectile_Presentation.asset", BrawlerElementType.Water)]
        [TestCase(Profiles + "Byron_SuperProjectile_Presentation.asset", BrawlerElementType.Water)]
        [TestCase(Profiles + "Bo_ArrowProjectile_Presentation.asset", BrawlerElementType.Air)]
        [TestCase(Profiles + "Jesse_MainProjectile_Presentation.asset", BrawlerElementType.Lightning)]
        [TestCase(Profiles + "Scrappy_ProjectilePresentation.asset", BrawlerElementType.Lightning)]
        [TestCase(Profiles + "Piper_MainProjectile_Presentation.asset", BrawlerElementType.Ice)]
        [TestCase(Profiles + "Leon_DiscProjectile_Presentation.asset", BrawlerElementType.Shadow)]
        [TestCase("Assets/Scriptables/Brawlers/Barley/Barley_main_Presentation.asset", BrawlerElementType.Nature)]
        public void AuthoredProfile_UsesDirectionalBlasterWithItsElement(string path, BrawlerElementType element)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ProjectilePresentationProfile>(path);
            Assert.That(profile, Is.Not.Null, path);
            Assert.That(profile.ElementType, Is.EqualTo(element));
            Assert.That(profile.RuntimeShape, Is.EqualTo(ProjectileRuntimeShape.BlasterBolt));
            Assert.That(profile.VisualPrefab, Is.Null);
            Assert.That(profile.UseSpin, Is.False);
            Assert.That(profile.FaceMovementDirection, Is.True);
            Assert.That(profile.BoltLength, Is.GreaterThan(profile.BoltWidth));
        }

        [Test]
        public void Leon_KeepsFourShotBurst_UsingShadowBolts()
        {
            var ability = AssetDatabase.LoadAssetAtPath<VolleyProjectileAbilityDefinition>(
                "Assets/Scriptables/Brawlers/Leon/Leon_Main.asset");
            Assert.That(ability.ProjectileCount, Is.EqualTo(4));
            Assert.That(ability.DelayBetweenShots, Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(ability.PresentationProfile.ElementType, Is.EqualTo(BrawlerElementType.Shadow));
            Assert.That(ability.PresentationProfile.RuntimeShape, Is.EqualTo(ProjectileRuntimeShape.BlasterBolt));
        }

        [Test]
        public void ClearAndReuse_RemovesOldWake_WithoutRebuildingRenderers()
        {
            ElementalProjectileView view = CreateView();
            ProjectilePresentationProfile profile = CreateProfile();
            view.Configure(profile, 1f);
            var trail = view.GetComponent<TrailRenderer>();
            var particles = view.GetComponentInChildren<ParticleSystem>();
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>();
            trail.AddPosition(Vector3.zero);
            trail.AddPosition(Vector3.forward);
            particles.Emit(5);

            view.Clear();
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(trail.emitting, Is.False);
            Assert.That(trail.positionCount, Is.Zero);
            Assert.That(particles.particleCount, Is.Zero);

            profile.ElementType = BrawlerElementType.Water;
            profile.UseRuntimeTrail = false;
            view.Configure(profile, 1f);
            CollectionAssert.AreEqual(renderers, view.GetComponentsInChildren<Renderer>());
            Assert.That(trail.emitting, Is.False);
            Assert.That(trail.positionCount, Is.Zero);
            Assert.That(particles.particleCount, Is.Zero);
            Assert.That(view.transform.Find("ElementSheath").GetComponent<LineRenderer>().startColor,
                Is.EqualTo(BrawlerElementUtility.ToColor(BrawlerElementType.Water)));
            Assert.That(view.GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void Bolt_UsesLocalForward_PowerScaleAndAnimatedElementAccents()
        {
            ElementalProjectileView view = CreateView();
            ProjectilePresentationProfile profile = CreateProfile();
            view.Configure(profile, 1.22f);
            var core = view.transform.Find("BlasterCore").GetComponent<LineRenderer>();
            Assert.That(core.useWorldSpace, Is.False);
            Assert.That(core.GetPosition(0).z, Is.LessThan(0f));
            Assert.That(core.GetPosition(2).z, Is.GreaterThan(0f));
            Assert.That(core.widthMultiplier, Is.EqualTo(profile.BoltWidth * 1.22f * 0.65f).Within(0.0001f));
            var motion = view.transform.Find("ElementMotion0").GetComponent<LineRenderer>();
            Vector3 before = motion.GetPosition(7);
            Random.State randomBefore = Random.state;
            view.TickVisual(0.12f);
            Assert.That(Vector3.Distance(before, motion.GetPosition(7)), Is.GreaterThan(0.001f));
            Assert.That(Random.state, Is.EqualTo(randomBefore), "VFX motion must not consume gameplay randomness.");
        }

        [Test]
        public void BlasterMaterial_AndAllWakeTextures_AreIncludedInResources()
        {
            Material material = Resources.Load<Material>("VFX/ElementalBlaster");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("MOBA/VFX/Elemental Blaster"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            foreach (string texture in new[] { "circle_03", "smoke_03", "spark_03" })
                Assert.That(Resources.Load<Texture2D>("VFX/Particles/" + texture), Is.Not.Null, texture);
        }

        private ElementalProjectileView CreateView()
        {
            var root = new GameObject("ElementalProjectileTest");
            _created.Add(root);
            return root.AddComponent<ElementalProjectileView>();
        }

        private ProjectilePresentationProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<ProjectilePresentationProfile>();
            _created.Add(profile);
            profile.RuntimeShape = ProjectileRuntimeShape.BlasterBolt;
            profile.ElementType = BrawlerElementType.Fire;
            return profile;
        }
    }
}
