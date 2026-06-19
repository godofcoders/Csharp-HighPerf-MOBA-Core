using UnityEngine;
using MOBA.Core.Definitions;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public class ProjectileVisualController : MonoBehaviour
    {
        private const float FallbackMinimumVisualDiameter = 0.16f;
        private const float FallbackTrailTime = 0.10f;
        private const float FallbackTrailStartWidth = 0.14f;
        private const float FallbackTrailEndWidth = 0.025f;

        private static readonly Color FallbackTrailColor = new Color(1f, 0.78f, 0.26f, 0.78f);
        private static readonly Color FallbackSuperTrailColor = new Color(1f, 0.35f, 0.92f, 0.82f);
        private static readonly Color FallbackVisualColor = new Color(1f, 0.72f, 0.18f, 1f);
        private static readonly Color FallbackSuperVisualColor = new Color(1f, 0.30f, 0.92f, 1f);

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

        private Vector3 _spinEulerPerSecond;
        private bool _useSpin;

        private void Awake()
        {
            if (_visualRoot == null)
                _visualRoot = transform;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_fallbackVisualMaterial);
            DestroyMaterial(_trailMaterial);
        }

        public void ApplyProfile(ProjectilePresentationProfile profile, bool isSuper = false)
        {
            _currentProfile = profile;
            _spinEulerPerSecond = Vector3.zero;
            _useSpin = false;

            ClearVisual();
            ConfigureTrail(_currentProfile, isSuper);

            if (_currentProfile == null || _currentProfile.VisualPrefab == null)
            {
                CreateFallbackVisual(isSuper);
                return;
            }

            _currentVisualInstance = Instantiate(_currentProfile.VisualPrefab, _visualRoot);
            _currentVisualInstance.transform.localPosition = _currentProfile.LocalPosition;
            _currentVisualInstance.transform.localRotation = Quaternion.Euler(_currentProfile.LocalRotationEuler);
            _currentVisualInstance.transform.localScale = ResolveReadableScale(
                _currentProfile.LocalScale,
                _currentProfile.MinimumVisualDiameter);

            _useSpin = _currentProfile.UseSpin;
            _spinEulerPerSecond = _currentProfile.SpinEulerPerSecond;
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

        private void ConfigureTrail(ProjectilePresentationProfile profile, bool isSuper)
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
                ? Mathf.Max(profile.TrailStartWidth, minimumDiameter * 0.55f)
                : FallbackTrailStartWidth;

            float endWidth = profile != null
                ? Mathf.Max(0f, profile.TrailEndWidth)
                : FallbackTrailEndWidth;

            Color startColor = profile != null
                ? isSuper ? profile.SuperTrailColor : profile.TrailColor
                : isSuper ? FallbackSuperTrailColor : FallbackTrailColor;

            Color endColor = startColor;
            endColor.a = 0f;

            trail.time = profile != null ? Mathf.Max(0.01f, profile.TrailTime) : FallbackTrailTime;
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

        private void CreateFallbackVisual(bool isSuper)
        {
            if (!_createFallbackVisualWhenMissing)
                return;

            _currentVisualInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _currentVisualInstance.name = "RuntimeProjectileVisual";
            _currentVisualInstance.transform.SetParent(_visualRoot, false);
            _currentVisualInstance.transform.localPosition = Vector3.zero;
            _currentVisualInstance.transform.localRotation = Quaternion.identity;

            float diameter = Mathf.Max(0.05f, _fallbackVisualDiameter);
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
            Color color = isSuper ? FallbackSuperVisualColor : FallbackVisualColor;
            _propertyBlock.SetColor("_Color", color);
            _propertyBlock.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(_propertyBlock);
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
    }
}
