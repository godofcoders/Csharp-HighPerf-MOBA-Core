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

            if (ShouldUseRuntimeShape(_currentProfile))
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

            ProjectileRuntimeShape shape = _currentProfile != null
                ? _currentProfile.RuntimeShape
                : ProjectileRuntimeShape.Sphere;

            _currentVisualInstance = CreateRuntimeShapeVisual(shape);
            if (_currentVisualInstance == null)
                return;

            _currentVisualInstance.transform.localPosition = _currentProfile != null
                ? _currentProfile.LocalPosition
                : Vector3.zero;
            _currentVisualInstance.transform.localRotation = _currentProfile != null
                ? Quaternion.Euler(_currentProfile.LocalRotationEuler)
                : Quaternion.identity;

            _currentVisualInstance.transform.localScale = ResolveRuntimeShapeScale(_currentProfile);

            if (_currentProfile != null)
            {
                _useSpin = _currentProfile.UseSpin;
                _spinEulerPerSecond = _currentProfile.SpinEulerPerSecond;
            }

            Renderer[] renderers = _currentVisualInstance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ResolveFallbackVisualMaterial();
            }

            ApplyStyleToRenderers(_currentVisualInstance, style);
            CreateRuntimeGlow(style);
        }

        private static bool ShouldUseRuntimeShape(ProjectilePresentationProfile profile)
        {
            return profile == null ||
                   profile.PreferRuntimeShape ||
                   profile.VisualPrefab == null;
        }

        private Vector3 ResolveRuntimeShapeScale(ProjectilePresentationProfile profile)
        {
            if (profile != null)
            {
                return ResolveReadableScale(
                    profile.LocalScale,
                    profile.MinimumVisualDiameter) * _powerVisualScale;
            }

            float diameter = Mathf.Max(0.05f, _fallbackVisualDiameter) * _powerVisualScale;
            return Vector3.one * diameter;
        }

        private GameObject CreateRuntimeShapeVisual(ProjectileRuntimeShape shape)
        {
            GameObject root = new GameObject("RuntimeProjectileVisual");
            root.transform.SetParent(_visualRoot, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            switch (shape)
            {
                case ProjectileRuntimeShape.Bottle:
                    CreatePrimitivePart(root.transform, "BottleBody", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, -0.04f),
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.70f, 0.38f, 0.70f));
                    CreatePrimitivePart(root.transform, "BottleNeck", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, 0.44f),
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.36f, 0.18f, 0.36f));
                    CreatePrimitivePart(root.transform, "BottleCap", PrimitiveType.Sphere,
                        new Vector3(0f, 0f, 0.64f),
                        Quaternion.identity,
                        new Vector3(0.42f, 0.42f, 0.24f));
                    break;

                case ProjectileRuntimeShape.EnergyOrb:
                    CreatePrimitivePart(root.transform, "EnergyOrbCore", PrimitiveType.Sphere,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one);
                    CreatePrimitivePart(root.transform, "EnergyOrbPulse", PrimitiveType.Sphere,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one * 1.22f);
                    break;

                case ProjectileRuntimeShape.MiniOrb:
                    CreatePrimitivePart(root.transform, "MiniOrbCore", PrimitiveType.Sphere,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one * 0.72f);
                    break;

                case ProjectileRuntimeShape.Vial:
                    CreatePrimitivePart(root.transform, "VialBody", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, 0f),
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.44f, 0.48f, 0.44f));
                    CreatePrimitivePart(root.transform, "VialTip", PrimitiveType.Sphere,
                        new Vector3(0f, 0f, 0.58f),
                        Quaternion.identity,
                        new Vector3(0.42f, 0.42f, 0.24f));
                    CreatePrimitivePart(root.transform, "VialTail", PrimitiveType.Sphere,
                        new Vector3(0f, 0f, -0.58f),
                        Quaternion.identity,
                        new Vector3(0.34f, 0.34f, 0.20f));
                    break;

                case ProjectileRuntimeShape.Bowl:
                    CreatePrimitivePart(root.transform, "PoisonBowl", PrimitiveType.Sphere,
                        new Vector3(0f, -0.05f, 0f),
                        Quaternion.identity,
                        new Vector3(1.08f, 0.42f, 1.08f));
                    CreatePrimitivePart(root.transform, "PoisonBowlRim", PrimitiveType.Cylinder,
                        new Vector3(0f, 0.16f, 0f),
                        Quaternion.identity,
                        new Vector3(1.12f, 0.06f, 1.12f));
                    break;

                case ProjectileRuntimeShape.Disc:
                    CreatePrimitivePart(root.transform, "DiscCore", PrimitiveType.Cylinder,
                        Vector3.zero,
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.78f, 0.10f, 0.78f));
                    CreatePrimitivePart(root.transform, "DiscEdge", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, -0.012f),
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.88f, 0.035f, 0.88f));
                    break;

                case ProjectileRuntimeShape.Bullet:
                    CreatePrimitivePart(root.transform, "BulletBody", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, -0.10f),
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.58f, 0.56f, 0.58f));
                    CreatePrimitivePart(root.transform, "BulletNose", PrimitiveType.Sphere,
                        new Vector3(0f, 0f, 0.32f),
                        Quaternion.identity,
                        new Vector3(0.58f, 0.58f, 0.48f));
                    CreatePrimitivePart(root.transform, "BulletBase", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, -0.52f),
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.66f, 0.14f, 0.66f));
                    break;

                case ProjectileRuntimeShape.NinjaStar:
                    CreatePrimitivePart(root.transform, "NinjaStarCore", PrimitiveType.Cylinder,
                        Vector3.zero,
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.42f, 0.06f, 0.42f));
                    CreatePrimitivePart(root.transform, "NinjaStarBladeTop", PrimitiveType.Cube,
                        new Vector3(0f, 0.34f, 0f),
                        Quaternion.Euler(0f, 0f, 45f),
                        new Vector3(0.18f, 0.54f, 0.055f));
                    CreatePrimitivePart(root.transform, "NinjaStarBladeBottom", PrimitiveType.Cube,
                        new Vector3(0f, -0.34f, 0f),
                        Quaternion.Euler(0f, 0f, 45f),
                        new Vector3(0.18f, 0.54f, 0.055f));
                    CreatePrimitivePart(root.transform, "NinjaStarBladeRight", PrimitiveType.Cube,
                        new Vector3(0.34f, 0f, 0f),
                        Quaternion.Euler(0f, 0f, -45f),
                        new Vector3(0.54f, 0.18f, 0.055f));
                    CreatePrimitivePart(root.transform, "NinjaStarBladeLeft", PrimitiveType.Cube,
                        new Vector3(-0.34f, 0f, 0f),
                        Quaternion.Euler(0f, 0f, -45f),
                        new Vector3(0.54f, 0.18f, 0.055f));
                    break;

                default:
                    CreatePrimitivePart(root.transform, "SphereCore", PrimitiveType.Sphere,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one);
                    break;
            }

            return root;
        }

        private static void CreatePrimitivePart(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
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
                if (isHypercharged)
                {
                    Color hyperCore = new Color(0.76f, 0.22f, 1f, 1f);
                    Color fizz = new Color(0.72f, 1f, 0.16f, 0.64f);
                    return new ProjectileVisualStyle(
                        hyperCore,
                        fizz,
                        new Color(0.62f, 0.20f, 1f, 0.78f),
                        fizz,
                        0.32f,
                        0.90f,
                        0.76f,
                        44f,
                        0.085f,
                        0.13f,
                        0.34f,
                        0.040f);
                }

                return new ProjectileVisualStyle(
                    isSuper ? new Color(0.62f, 0.20f, 1f, 1f) : new Color(1f, 0.56f, 0.08f, 1f),
                    isSuper ? new Color(0.72f, 1f, 0.16f, 0.54f) : new Color(1f, 0.78f, 0.12f, 0.48f),
                    isSuper ? new Color(0.62f, 0.20f, 1f, 0.74f) : new Color(1f, 0.54f, 0.08f, 0.72f),
                    isSuper ? new Color(0.72f, 1f, 0.16f, 0.58f) : new Color(1f, 0.76f, 0.14f, 0.54f),
                    isSuper ? 0.28f : 0.22f,
                    isSuper ? 0.86f : 0.78f,
                    isSuper ? 0.72f : 0.64f,
                    isSuper ? 36f : 24f,
                    isSuper ? 0.075f : 0.060f,
                    isSuper ? 0.12f : 0.09f,
                    isSuper ? 0.30f : 0.20f,
                    isSuper ? 0.036f : 0.026f);
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

            if (profileName.Contains("bo") || profileName.Contains("arrow"))
            {
                return new ProjectileVisualStyle(
                    new Color(1f, 0.78f, 0.28f, 1f),
                    new Color(1f, 0.48f, 0.10f, 0.54f),
                    new Color(1f, 0.66f, 0.20f, 0.70f),
                    new Color(1f, 0.54f, 0.10f, 0.54f),
                    0.18f,
                    0.70f,
                    0.62f,
                    16f,
                    0.045f,
                    0.070f,
                    0.16f,
                    0.020f);
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

            if (profileName.Contains("barley"))
            {
                if (isHypercharged)
                    return 1.28f;

                return isSuper ? 1.14f : 1f;
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
