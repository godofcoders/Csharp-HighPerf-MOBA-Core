using MOBA.Core.Definitions;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    /// <summary>Reusable presentation for pooled blaster shots; never owns collision or damage.</summary>
    public sealed class ElementalProjectileView : MonoBehaviour
    {
        private static readonly int RibbonId = Shader.PropertyToID("_Ribbon");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private Material _material;
        private MaterialPropertyBlock _properties;
        private LineRenderer _core;
        private LineRenderer _sheath;
        private TrailRenderer _trail;
        private readonly LineRenderer[] _accents = new LineRenderer[2];
        private ParticleSystem _particles;
        private float _width;
        private float _length;
        private float _elapsed;
        private BrawlerElementType _element;
        private Color _color;

        public void Configure(ProjectilePresentationProfile profile, float powerScale)
        {
            EnsureRenderers();
            Clear();
            _element = profile.ElementType;
            _color = BrawlerElementUtility.ToColor(_element);
            _width = Mathf.Max(0.02f, profile.BoltWidth) * powerScale;
            _length = Mathf.Max(0.1f, profile.BoltLength) * powerScale;
            transform.localPosition = profile.LocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            SetBolt(_sheath, _width * 2.1f, _color, _length);
            SetBolt(_core, _width * 0.65f, Color.Lerp(_color, Color.white, 0.82f), _length * 0.82f);
            _trail.time = Mathf.Clamp(profile.TrailTime, 0.035f, 0.18f);
            _trail.startWidth = _width * 0.85f;
            _trail.endWidth = 0f;
            _trail.startColor = _color;
            _trail.endColor = new Color(_color.r, _color.g, _color.b, 0f);
            _trail.Clear();
            _trail.emitting = profile.UseRuntimeTrail;
            ConfigureAccents();
            ConfigureParticles();
            TickVisual(0f);
        }

        public void TickVisual(float deltaTime)
        {
            if (!gameObject.activeSelf)
                return;

            _elapsed += Mathf.Max(0f, deltaTime);
            _sheath.widthMultiplier = _width * 2.1f * (1f + Mathf.Sin(_elapsed * 24f) * 0.08f);
            UpdateAccents();
        }

        public void Clear()
        {
            _elapsed = 0f;
            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
            }
            if (_particles != null)
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }

        private void EnsureRenderers()
        {
            if (_core != null)
                return;

            _material = Resources.Load<Material>("VFX/ElementalBlaster");
            _properties = new MaterialPropertyBlock();
            _core = CreateRibbon("BlasterCore");
            _sheath = CreateRibbon("ElementSheath");
            _trail = gameObject.AddComponent<TrailRenderer>();
            ConfigureRenderer(_trail, true);
            _trail.minVertexDistance = 0.045f;
            _trail.numCapVertices = 3;
            _trail.numCornerVertices = 2;
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.alignment = LineAlignment.View;
            _trail.emitting = false;
            for (int i = 0; i < _accents.Length; i++)
                _accents[i] = CreateRibbon("ElementMotion" + i);

            var particleRoot = new GameObject("ElementWake");
            particleRoot.layer = gameObject.layer;
            particleRoot.transform.SetParent(transform, false);
            _particles = particleRoot.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ConfigureAccents()
        {
            Color accent = _element == BrawlerElementType.Lightning
                ? new Color(0.3f, 0.85f, 1f, 1f)
                : Color.Lerp(_color, Color.white, 0.28f);
            for (int i = 0; i < _accents.Length; i++)
            {
                LineRenderer line = _accents[i];
                line.positionCount = 20;
                line.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
                line.widthMultiplier = _width * (_element == BrawlerElementType.Fire ? 0.6f : 0.24f);
                line.startColor = accent;
                line.endColor = new Color(_color.r, _color.g, _color.b, 0.1f);
            }
        }

        private void UpdateAccents()
        {
            for (int strand = 0; strand < _accents.Length; strand++)
            {
                float side = strand == 0 ? -1f : 1f;
                for (int point = 0; point < 20; point++)
                {
                    float t = point / 19f;
                    float angle = t * Mathf.PI * 4f - _elapsed * 16f + strand * Mathf.PI;
                    float radius = _width * Mathf.Sin(t * Mathf.PI);
                    Vector3 offset;
                    switch (_element)
                    {
                        case BrawlerElementType.Lightning:
                            // Stepped time gives electrical jumps without using the gameplay random stream.
                            float noise = Mathf.Sin(point * 17.13f + Mathf.Floor(_elapsed * 24f) * 7.37f + strand * 9f);
                            offset = new Vector3(noise * radius * 1.8f, side * radius * 0.45f, 0f);
                            break;
                        case BrawlerElementType.Air:
                            offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * 1.9f;
                            break;
                        case BrawlerElementType.Water:
                            offset = new Vector3(side * radius * 0.8f, Mathf.Sin(angle * 0.6f) * radius * 0.6f, 0f);
                            break;
                        case BrawlerElementType.Fire:
                            offset = new Vector3(side * radius * (0.6f + 0.4f * Mathf.Sin(angle * 2f)),
                                Mathf.Sin(angle * 1.7f) * radius * 0.5f, 0f);
                            break;
                        case BrawlerElementType.Ice:
                            offset = new Vector3(side * radius * 0.8f, side * radius * 0.4f, 0f);
                            break;
                        case BrawlerElementType.Nature:
                            offset = new Vector3(Mathf.Cos(angle * 0.5f), Mathf.Sin(angle * 0.5f), 0f) * radius;
                            break;
                        case BrawlerElementType.Shadow:
                            offset = new Vector3(side * radius * 1.3f,
                                Mathf.Sin(angle * 0.65f) * radius * 1.3f, 0f);
                            break;
                        default:
                            offset = new Vector3(side * radius, 0f, 0f);
                            break;
                    }
                    _accents[strand].SetPosition(point, Vector3.back * (_length * t * 1.65f) + offset);
                }
            }
        }

        private void ConfigureParticles()
        {
            bool fire = _element == BrawlerElementType.Fire;
            bool water = _element == BrawlerElementType.Water;
            bool ice = _element == BrawlerElementType.Ice;
            bool shadow = _element == BrawlerElementType.Shadow;
            bool nature = _element == BrawlerElementType.Nature;
            bool air = _element == BrawlerElementType.Air;
            string texture = water ? "circle_03" :
                (fire || shadow || air ? "smoke_03" : "spark_03");
            _particles.transform.localPosition = Vector3.back * _length * 0.4f;
            _particles.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = shadow ? 0.28f : (water || fire ? 0.22f : 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(_width * 0.35f, _width * (fire || shadow ? 1.6f : 0.85f));
            main.startColor = Color.white;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = fire ? -0.12f : (water || ice ? 0.28f : 0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = _particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = air ? 4f : 8f;
            var shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = water ? 24f : 12f;
            shape.radius = _width * 0.3f;

            Color hot = fire ? new Color(1f, 0.88f, 0.3f, 0.9f) : Color.Lerp(_color, Color.white, 0.45f);
            Color cool = fire ? new Color(1f, 0.12f, 0.01f, 0f) : _color;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(hot, 0f), new GradientColorKey(_color, 0.35f), new GradientColorKey(cool, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 0.45f), new GradientAlphaKey(0f, 1f) });
            var color = _particles.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
            var size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, fire || shadow
                ? AnimationCurve.EaseInOut(0f, 0.65f, 1f, 1.7f)
                : AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var noise = _particles.noise;
            noise.enabled = fire || shadow || nature;
            noise.strength = _width * 0.8f;
            noise.frequency = 3f;
            noise.scrollSpeed = 1f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ConfigureRenderer(renderer, false, Resources.Load<Texture2D>("VFX/Particles/" + texture));
            _particles.Play(true);
        }

        private LineRenderer CreateRibbon(string label)
        {
            var ribbon = new GameObject(label);
            ribbon.layer = gameObject.layer;
            ribbon.transform.SetParent(transform, false);
            var line = ribbon.AddComponent<LineRenderer>();
            ConfigureRenderer(line, true);
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            return line;
        }

        private void ConfigureRenderer(Renderer renderer, bool ribbon, Texture texture = null)
        {
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _properties.Clear();
            _properties.SetFloat(RibbonId, ribbon ? 1f : 0f);
            _properties.SetTexture(BaseMapId, texture != null ? texture : Texture2D.whiteTexture);
            renderer.SetPropertyBlock(_properties);
        }

        private static void SetBolt(LineRenderer line, float width, Color color, float length)
        {
            line.positionCount = 3;
            line.SetPosition(0, Vector3.back * length * 0.85f);
            line.SetPosition(1, Vector3.zero);
            line.SetPosition(2, Vector3.forward * length * 0.15f);
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
        }
    }
}
