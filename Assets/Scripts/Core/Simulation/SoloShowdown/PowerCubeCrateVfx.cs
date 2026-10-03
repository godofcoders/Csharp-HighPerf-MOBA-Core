using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Simulation
{
    public sealed class PowerCubeCrateVfx : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Material _runtimeMaterial;

        public static GameObject CreateBreakBurst(Vector3 position)
        {
            GameObject effect = CreateEffect("PowerCubeChestBreakVFX", position, out ParticleSystem particles);

            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.46f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.25f, 2.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.30f, 0.12f, 0.035f, 1f),
                new Color(0.98f, 0.68f, 0.12f, 1f));
            main.gravityModifier = 1.05f;
            main.maxParticles = 28;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.72f, 0.42f, 0.58f);

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-7f, 7f);

            particles.Emit(22);
            particles.Play(true);
            return effect;
        }

        public static GameObject CreateRevealBurst(Vector3 position)
        {
            GameObject effect = CreateEffect("PowerCubeRevealVFX", position, out ParticleSystem particles);

            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.52f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.28f, 0.82f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.075f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 1f, 0.16f, 0.95f),
                new Color(1f, 0.86f, 0.20f, 1f));
            main.gravityModifier = -0.08f;
            main.maxParticles = 24;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.24f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.15f),
                    new Keyframe(0.25f, 1f),
                    new Keyframe(1f, 0f)));

            particles.Emit(16);
            particles.Play(true);
            return effect;
        }

        private static GameObject CreateEffect(
            string effectName,
            Vector3 position,
            out ParticleSystem particles)
        {
            GameObject effect = new GameObject(effectName);
            effect.transform.position = position;

            particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.12f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = fade;

            ParticleSystemRenderer particleRenderer = effect.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.sortingOrder = 8;

            PowerCubeCrateVfx owner = effect.AddComponent<PowerCubeCrateVfx>();
            owner._runtimeMaterial = CreateParticleMaterial();
            if (owner._runtimeMaterial != null)
                particleRenderer.sharedMaterial = owner._runtimeMaterial;

            return effect;
        }

        private static Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit") ??
                            Shader.Find("Sprites/Default") ??
                            Shader.Find("Standard");
            if (shader == null)
                return null;

            Material material = new Material(shader)
            {
                name = "Runtime_PowerCubeChestParticles",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, Color.white);
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, Color.white);

            return material;
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
        }
    }
}
