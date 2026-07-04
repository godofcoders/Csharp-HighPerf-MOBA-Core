using UnityEngine;
using MOBA.Core.Definitions;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public class ProjectileVisualController : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private const float FallbackMinimumVisualDiameter = 0.16f;
        private const float FallbackTrailTime = 0.07f;
        private const float FallbackTrailStartWidth = 0.14f;
        private const float FallbackTrailEndWidth = 0.025f;

        private static readonly Color FallbackTrailColor = new Color(1f, 0.78f, 0.26f, 0.78f);
        private static readonly Color FallbackSuperTrailColor = new Color(1f, 0.36f, 0.08f, 0.82f);
        private static readonly Color FallbackVisualColor = new Color(1f, 0.72f, 0.18f, 1f);
        private static readonly Color FallbackSuperVisualColor = new Color(1f, 0.34f, 0.08f, 1f);

        [SerializeField] private Transform _visualRoot;

        [Header("Fallback Visual")]
        [SerializeField] private bool _createFallbackVisualWhenMissing = true;
        [SerializeField] private float _fallbackVisualDiameter = 0.18f;

        private GameObject _currentVisualInstance;
        private ProjectilePresentationProfile _currentProfile;
        private TrailRenderer _trailRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Material _fallbackVisualMaterial;
        private Material _trailMaterial;
        private Material _runtimeFxMaterial;
        private ParticleSystem _sparkParticles;
        private ParticleSystemRenderer _sparkParticleRenderer;
        private ProjectileVisualStyle _currentStyle;

        private Vector3 _spinEulerPerSecond;
        private bool _useSpin;
        private float _powerVisualScale = 1f;

        private void Awake()
        {
            if (_visualRoot == null)
                _visualRoot = transform;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_fallbackVisualMaterial);
            DestroyMaterial(_trailMaterial);
            DestroyMaterial(_runtimeFxMaterial);
        }

        public void ApplyProfile(
            ProjectilePresentationProfile profile,
            bool isSuper = false,
            bool isHypercharged = false)
        {
            _currentProfile = profile;
            _currentStyle = ResolveStyle(profile, isSuper, isHypercharged);
            _powerVisualScale = ResolvePowerVisualScale(profile, isSuper, isHypercharged);
            _spinEulerPerSecond = Vector3.zero;
            _useSpin = false;

            ClearVisual();
            ConfigureTrail(_currentProfile, _currentStyle);

            if (_currentProfile == null || _currentProfile.VisualPrefab == null)
            {
                CreateFallbackVisual(_currentStyle);
                ConfigureProjectileParticles(_currentStyle);
                return;
            }

            _currentVisualInstance = Instantiate(_currentProfile.VisualPrefab, _visualRoot);
            _currentVisualInstance.transform.localPosition = _currentProfile.LocalPosition;
            _currentVisualInstance.transform.localRotation = Quaternion.Euler(_currentProfile.LocalRotationEuler);
            _currentVisualInstance.transform.localScale = ResolveReadableScale(
                _currentProfile.LocalScale,
                _currentProfile.MinimumVisualDiameter) * _powerVisualScale;

            _useSpin = _currentProfile.UseSpin;
            _spinEulerPerSecond = _currentProfile.SpinEulerPerSecond;

            ApplyStyleToRenderers(_currentVisualInstance, _currentStyle);
            CreateRuntimeGlow(_currentStyle);
            ConfigureProjectileParticles(_currentStyle);
        }

        public void TickVisual(float deltaTime)
        {
            if (_useSpin && _currentVisualInstance != null)
            {
                _currentVisualInstance.transform.Rotate(_spinEulerPerSecond * deltaTime, Space.Self);
            }
        }

        public bool ShouldFaceMovementDirection()
        {
            return _currentProfile == null || _currentProfile.FaceMovementDirection;
        }

        private void ConfigureTrail(ProjectilePresentationProfile profile, ProjectileVisualStyle style)
        {
            bool useTrail = profile == null || profile.UseRuntimeTrail;
            TrailRenderer trail = EnsureTrailRenderer();
            if (trail == null)
                return;

            if (!useTrail)
            {
                trail.emitting = false;
                trail.Clear();
                return;
            }

            float minimumDiameter = profile != null
                ? Mathf.Max(0.01f, profile.MinimumVisualDiameter)
                : FallbackMinimumVisualDiameter;

            float startWidth = profile != null
                ? Mathf.Max(profile.TrailStartWidth, minimumDiameter * 0.55f) * style.TrailWidthMultiplier
                : FallbackTrailStartWidth;
            startWidth *= Mathf.Lerp(1f, _powerVisualScale, 0.65f);

            float endWidth = profile != null
                ? Mathf.Max(0f, profile.TrailEndWidth) * style.TrailWidthMultiplier
                : FallbackTrailEndWidth;
            endWidth *= Mathf.Lerp(1f, _powerVisualScale, 0.45f);

            Color startColor = style.TrailColor;

            Color endColor = startColor;
            endColor.a = 0f;

            float baseTrailTime = profile != null
                ? Mathf.Max(0.01f, profile.TrailTime)
                : FallbackTrailTime;
            trail.time = baseTrailTime * style.TrailTimeMultiplier * Mathf.Lerp(1f, _powerVisualScale, 0.24f);
            trail.startWidth = startWidth;
            trail.endWidth = endWidth;
            trail.startColor = startColor;
            trail.endColor = endColor;
            trail.material = ResolveTrailMaterial();
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.autodestruct = false;
            trail.Clear();
            trail.emitting = true;
        }

        private TrailRenderer EnsureTrailRenderer()
        {
            if (_trailRenderer != null)
                return _trailRenderer;

            _trailRenderer = GetComponent<TrailRenderer>();
            if (_trailRenderer == null)
                _trailRenderer = gameObject.AddComponent<TrailRenderer>();

            return _trailRenderer;
        }

        private void CreateFallbackVisual(ProjectileVisualStyle style)
        {
            if (!_createFallbackVisualWhenMissing)
                return;

            _currentVisualInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _currentVisualInstance.name = "RuntimeProjectileVisual";
            _currentVisualInstance.transform.SetParent(_visualRoot, false);
            _currentVisualInstance.transform.localPosition = Vector3.zero;
            _currentVisualInstance.transform.localRotation = Quaternion.identity;

            float diameter = Mathf.Max(0.05f, _fallbackVisualDiameter) * _powerVisualScale;
            _currentVisualInstance.transform.localScale = Vector3.one * diameter;

            Collider collider = _currentVisualInstance.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = _currentVisualInstance.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ResolveFallbackVisualMaterial();

            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, style.CoreColor);
            _propertyBlock.SetColor(BaseColorId, style.CoreColor);
            _propertyBlock.SetColor(EmissionColorId, style.CoreColor);
            renderer.SetPropertyBlock(_propertyBlock);

            CreateRuntimeGlow(style);
        }

        private static Vector3 ResolveReadableScale(Vector3 requestedScale, float minimumDiameter)
        {
            float minimum = Mathf.Max(0.01f, minimumDiameter);
            float largestAxis = Mathf.Max(
                Mathf.Abs(requestedScale.x),
                Mathf.Abs(requestedScale.y),
                Mathf.Abs(requestedScale.z));

            if (largestAxis >= minimum || largestAxis <= 0.0001f)
                return requestedScale;

            return requestedScale * (minimum / largestAxis);
        }

        public void ClearVisual()
        {
            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _visualRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            _currentVisualInstance = null;
        }

        public void ResetForPool()
        {
            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = false;
                _trailRenderer.Clear();
            }

            ClearVisual();
            _currentProfile = null;
            _spinEulerPerSecond = Vector3.zero;
            _useSpin = false;
            _powerVisualScale = 1f;

            if (_sparkParticles != null)
            {
                _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _sparkParticles.Clear(true);
            }
        }

        private Material ResolveFallbackVisualMaterial()
        {
            if (_fallbackVisualMaterial != null)
                return _fallbackVisualMaterial;

            _fallbackVisualMaterial = CreateUnlitMaterial(FallbackVisualColor);
            return _fallbackVisualMaterial;
        }

        private Material ResolveTrailMaterial()
        {
            if (_trailMaterial != null)
                return _trailMaterial;

            _trailMaterial = CreateUnlitMaterial(FallbackTrailColor);
            return _trailMaterial;
        }

        private Material ResolveRuntimeFxMaterial()
        {
            if (_runtimeFxMaterial != null)
                return _runtimeFxMaterial;

            _runtimeFxMaterial = CreateUnlitMaterial(Color.white);
            return _runtimeFxMaterial;
        }

        private void ApplyStyleToRenderers(GameObject visualInstance, ProjectileVisualStyle style)
        {
            if (visualInstance == null)
                return;

            Renderer[] renderers = visualInstance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                EnsurePropertyBlock();
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ColorId, style.CoreColor);
                _propertyBlock.SetColor(BaseColorId, style.CoreColor);
                _propertyBlock.SetColor(EmissionColorId, style.CoreColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void CreateRuntimeGlow(ProjectileVisualStyle style)
        {
            if (!style.UseGlow)
                return;

            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "RuntimeProjectileGlow";
            glow.transform.SetParent(_visualRoot, false);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localRotation = Quaternion.identity;
            glow.transform.localScale =
                Vector3.one * Mathf.Max(0.01f, style.GlowDiameter * _powerVisualScale);

            Collider collider = glow.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = glow.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ResolveRuntimeFxMaterial();

            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, style.GlowColor);
            _propertyBlock.SetColor(BaseColorId, style.GlowColor);
            _propertyBlock.SetColor(EmissionColorId, style.GlowColor);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void ConfigureProjectileParticles(ProjectileVisualStyle style)
        {
            ParticleSystem particles = EnsureSparkParticles();
            if (particles == null)
                return;

            if (!style.UseParticles)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Clear(true);
                return;
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Clear(true);

            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = style.ParticleLifetime * Mathf.Lerp(1f, _powerVisualScale, 0.22f);
            main.startSpeed = style.ParticleSpeed * Mathf.Lerp(1f, _powerVisualScale, 0.34f);
            main.startSize = style.ParticleSize * Mathf.Lerp(1f, _powerVisualScale, 0.58f);
            main.startColor = style.ParticleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.playOnAwake = false;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = style.ParticleRate * Mathf.Lerp(1f, _powerVisualScale, 0.55f);

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = style.ParticleRadius * Mathf.Lerp(1f, _powerVisualScale, 0.42f);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            Color fadeColor = style.ParticleColor;
            fadeColor.a = 0f;
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(style.ParticleColor, 0f),
                    new GradientColorKey(style.ParticleColor, 0.35f),
                    new GradientColorKey(fadeColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(style.ParticleColor.a, 0f),
                    new GradientAlphaKey(style.ParticleColor.a * 0.55f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.65f, 0.62f),
                    new Keyframe(1f, 0f)));

            if (_sparkParticleRenderer == null)
                _sparkParticleRenderer = particles.GetComponent<ParticleSystemRenderer>();

            if (_sparkParticleRenderer != null)
            {
                _sparkParticleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                _sparkParticleRenderer.material = ResolveRuntimeFxMaterial();
                _sparkParticleRenderer.sortingOrder = 8;
            }

            particles.Clear(true);
            particles.Play(true);
        }

        private ParticleSystem EnsureSparkParticles()
        {
            if (_sparkParticles != null)
                return _sparkParticles;

            _sparkParticles = GetComponent<ParticleSystem>();
            if (_sparkParticles == null)
                _sparkParticles = gameObject.AddComponent<ParticleSystem>();

            _sparkParticleRenderer = _sparkParticles.GetComponent<ParticleSystemRenderer>();
            return _sparkParticles;
        }

        private static ProjectileVisualStyle ResolveStyle(
            ProjectilePresentationProfile profile,
            bool isSuper,
            bool isHypercharged)
        {
            string profileName = profile != null && !string.IsNullOrEmpty(profile.name)
                ? profile.name.ToLowerInvariant()
                : string.Empty;

            if (profileName.Contains("barley"))
            {
                return new ProjectileVisualStyle(
                    new Color(0.66f, 0.22f, 1f, 1f),
                    new Color(0.90f, 0.58f, 1f, 0.58f),
                    new Color(0.58f, 0.20f, 1f, 0.82f),
                    new Color(0.90f, 0.45f, 1f, 0.62f),
                    0.24f,
                    1.18f,
                    1.22f,
                    34f,
                    0.12f,
                    0.16f,
                    0.36f,
                    0.045f);
            }

            if (profileName.Contains("byron"))
            {
                if (isHypercharged)
                {
                    Color hyperCore = new Color(0.18f, 1f, 0.70f, 1f);
                    Color hyperGlow = new Color(0.36f, 0.08f, 0.82f, 0.72f);
                    return new ProjectileVisualStyle(
                        hyperCore,
                        hyperGlow,
                        hyperGlow,
                        hyperCore,
                        0.42f,
                        0.92f,
                        0.78f,
                        56f,
                        0.090f,
                        0.13f,
                        0.42f,
                        0.042f);
                }

                Color core = isSuper
                    ? new Color(0.34f, 0.12f, 0.86f, 1f)
                    : new Color(0.12f, 1f, 0.72f, 1f);
                Color glow = isSuper
                    ? new Color(0.12f, 1f, 0.72f, 0.54f)
                    : new Color(0.18f, 1f, 0.82f, 0.48f);
                return new ProjectileVisualStyle(
                    core,
                    glow,
                    glow,
                    glow,
                    isSuper ? 0.34f : 0.20f,
                    isSuper ? 0.92f : 0.78f,
                    isSuper ? 0.82f : 0.64f,
                    isSuper ? 42f : 24f,
                    isSuper ? 0.090f : 0.055f,
                    isSuper ? 0.12f : 0.08f,
                    isSuper ? 0.34f : 0.20f,
                    isSuper ? 0.040f : 0.026f);
            }

            if (profileName.Contains("jesse") || profileName.Contains("jessie"))
            {
                return new ProjectileVisualStyle(
                    new Color(0.10f, 0.94f, 1f, 1f),
                    new Color(1f, 0.92f, 0.12f, 0.58f),
                    new Color(0.16f, 0.90f, 1f, 0.78f),
                    new Color(1f, 0.96f, 0.18f, 0.64f),
                    0.28f,
                    0.92f,
                    0.76f,
                    34f,
                    0.080f,
                    0.11f,
                    0.28f,
                    0.034f);
            }

            if (profileName.Contains("scrappy"))
            {
                return new ProjectileVisualStyle(
                    new Color(1f, 0.64f, 0.10f, 1f),
                    new Color(0.18f, 0.88f, 1f, 0.54f),
                    new Color(0.22f, 0.84f, 1f, 0.78f),
                    new Color(1f, 0.70f, 0.16f, 0.62f),
                    0.24f,
                    0.86f,
                    0.70f,
                    30f,
                    0.070f,
                    0.10f,
                    0.26f,
                    0.030f);
            }

            if (profileName.Contains("colt"))
            {
                if (isHypercharged)
                {
                    Color hyperCore = new Color(1f, 0.70f, 0.16f, 1f);
                    Color hyperWrap = new Color(0.24f, 0.05f, 0.48f, 0.82f);
                    return new ProjectileVisualStyle(
                        hyperCore,
                        hyperWrap,
                        hyperWrap,
                        hyperWrap,
                        0.36f,
                        0.86f,
                        0.72f,
                        46f,
                        0.075f,
                        0.12f,
                        0.34f,
                        0.036f);
                }

                Color core = isSuper
                    ? new Color(1f, 0.46f, 0.08f, 1f)
                    : new Color(1f, 0.82f, 0.22f, 1f);
                Color glow = isSuper
                    ? new Color(1f, 0.18f, 0.02f, 0.68f)
                    : new Color(1f, 0.46f, 0.08f, 0.62f);
                return new ProjectileVisualStyle(
                    core,
                    glow,
                    glow,
                    glow,
                    isSuper ? 0.30f : 0.24f,
                    isSuper ? 0.88f : 0.82f,
                    isSuper ? 0.68f : 0.58f,
                    isSuper ? 34f : 24f,
                    isSuper ? 0.070f : 0.055f,
                    isSuper ? 0.10f : 0.08f,
                    isSuper ? 0.30f : 0.20f,
                    isSuper ? 0.034f : 0.026f);
            }

            return new ProjectileVisualStyle(
                FallbackVisualColor,
                FallbackSuperVisualColor,
                FallbackTrailColor,
                FallbackTrailColor,
                0.20f,
                1f,
                1f,
                24f,
                0.09f,
                0.14f,
                0.30f,
                0.04f);
        }

        private static float ResolvePowerVisualScale(
            ProjectilePresentationProfile profile,
            bool isSuper,
            bool isHypercharged)
        {
            string profileName = profile != null && !string.IsNullOrEmpty(profile.name)
                ? profile.name.ToLowerInvariant()
                : string.Empty;

            if (profileName.Contains("colt"))
            {
                if (isHypercharged)
                    return 1.16f;

                return isSuper ? 1.08f : 1f;
            }

            if (isHypercharged)
                return 1.85f;

            return isSuper ? 1.38f : 1f;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.color = color;
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        private readonly struct ProjectileVisualStyle
        {
            public readonly Color CoreColor;
            public readonly Color GlowColor;
            public readonly Color TrailColor;
            public readonly Color ParticleColor;
            public readonly float GlowDiameter;
            public readonly float TrailWidthMultiplier;
            public readonly float TrailTimeMultiplier;
            public readonly float ParticleRate;
            public readonly float ParticleSize;
            public readonly float ParticleLifetime;
            public readonly float ParticleSpeed;
            public readonly float ParticleRadius;
            public readonly bool UseGlow;
            public readonly bool UseParticles;

            public ProjectileVisualStyle(
                Color coreColor,
                Color glowColor,
                Color trailColor,
                Color particleColor,
                float glowDiameter,
                float trailWidthMultiplier,
                float trailTimeMultiplier,
                float particleRate,
                float particleSize,
                float particleLifetime,
                float particleSpeed,
                float particleRadius)
            {
                CoreColor = coreColor;
                GlowColor = glowColor;
                TrailColor = trailColor;
                ParticleColor = particleColor;
                GlowDiameter = glowDiameter;
                TrailWidthMultiplier = trailWidthMultiplier;
                TrailTimeMultiplier = trailTimeMultiplier;
                ParticleRate = particleRate;
                ParticleSize = particleSize;
                ParticleLifetime = particleLifetime;
                ParticleSpeed = particleSpeed;
                ParticleRadius = particleRadius;
                UseGlow = glowDiameter > 0f;
                UseParticles = particleRate > 0f;
            }
        }
    }
}
