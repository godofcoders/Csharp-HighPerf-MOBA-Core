using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public enum BrawlerChargeRingKind
    {
        None,
        Super,
        Hypercharge
    }

    public static class BrawlerChargeRingUtility
    {
        public static BrawlerChargeRingKind Resolve(
            bool superReady,
            bool hyperchargeAvailable,
            bool hyperchargeReady)
        {
            if (hyperchargeAvailable && hyperchargeReady)
                return BrawlerChargeRingKind.Hypercharge;

            return superReady
                ? BrawlerChargeRingKind.Super
                : BrawlerChargeRingKind.None;
        }
    }

    public sealed class BrawlerHyperchargePresentation : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private BrawlerController _brawler;
        [SerializeField] private Color _activeColor = new Color(0.72f, 0.22f, 1f, 1f);
        [SerializeField] private float _emissionIntensity = 2.45f;
        [SerializeField] private float _pulseSpeed = 8.6f;
        [SerializeField] private float _auraRadius = 1.85f;
        [SerializeField] private float _auraAlpha = 0.52f;

        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _renderers;
        private GameObject _auraObject;
        private Renderer _auraRenderer;
        private Material _auraMaterial;
        private bool _isActive;

        private void Awake()
        {
            EnsurePropertyBlock();
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            RefreshRenderers();
            EnsureAura();
            SetActive(false);
        }

        private void OnEnable()
        {
            BrawlerPresentationEventBus.OnEvent += HandlePresentationEvent;
        }

        private void OnDisable()
        {
            BrawlerPresentationEventBus.OnEvent -= HandlePresentationEvent;
            SetActive(false);
        }

        private void OnDestroy()
        {
            if (_auraMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_auraMaterial);
            else
                DestroyImmediate(_auraMaterial);
        }

        public void Bind(BrawlerController brawler)
        {
            _brawler = brawler;
            RefreshRenderers();
            EnsureAura();
            SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_isActive)
                return;

            float pulse = 0.82f + Mathf.Sin(Time.time * _pulseSpeed) * 0.18f;
            ApplyRendererHighlight(pulse);

            if (_auraObject != null)
            {
                float radius = _auraRadius * (0.92f + pulse * 0.08f);
                _auraObject.transform.localScale = new Vector3(radius, 0.025f, radius);
            }
        }

        private void HandlePresentationEvent(BrawlerPresentationEvent evt)
        {
            if (_brawler == null || evt.Source != _brawler)
                return;

            switch (evt.EventType)
            {
                case BrawlerPresentationEventType.HyperchargeStarted:
                    SetActive(true);
                    break;

                case BrawlerPresentationEventType.HyperchargeEnded:
                case BrawlerPresentationEventType.Died:
                    SetActive(false);
                    break;
            }
        }

        private void SetActive(bool active)
        {
            _isActive = active;

            if (active)
            {
                RefreshRenderers();
                EnsureAura();
                ApplyRendererHighlight(1f);
            }
            else
            {
                ClearRendererHighlight();
            }

            if (_auraObject != null && _auraObject.activeSelf != active)
                _auraObject.SetActive(active);
        }

        private void RefreshRenderers()
        {
            BrawlerController root = _brawler != null ? _brawler : GetComponent<BrawlerController>();
            _renderers = root != null
                ? root.GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>(true);
        }

        private void ApplyRendererHighlight(float pulse)
        {
            if (_renderers == null)
                return;

            Color emission = _activeColor * Mathf.Max(0f, _emissionIntensity * pulse);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || renderer == _auraRenderer)
                    continue;

                EnsurePropertyBlock();
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId, emission);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private void ClearRendererHighlight()
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || renderer == _auraRenderer)
                    continue;

                renderer.SetPropertyBlock(null);
            }
        }

        private void EnsureAura()
        {
            if (_auraObject != null)
                return;

            _auraObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _auraObject.name = "HyperchargeAura";
            _auraObject.transform.SetParent(transform, false);
            _auraObject.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            _auraObject.transform.localRotation = Quaternion.identity;
            _auraObject.transform.localScale = new Vector3(_auraRadius, 0.025f, _auraRadius);

            Collider collider = _auraObject.GetComponent<Collider>();
            if (collider != null)
                DestroyGeneratedObject(collider);

            _auraRenderer = _auraObject.GetComponent<Renderer>();
            _auraMaterial = CreateAuraMaterial();

            if (_auraRenderer != null)
            {
                if (_auraMaterial != null)
                    _auraRenderer.sharedMaterial = _auraMaterial;

                _auraRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _auraRenderer.receiveShadows = false;
            }

            _auraObject.SetActive(false);
        }

        private Material CreateAuraMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);

            Color color = _activeColor;
            color.a = Mathf.Clamp01(_auraAlpha);
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

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }

    public sealed class BrawlerChargeReadinessPresentation : MonoBehaviour
    {
        private const string RingObjectName = "ChargeReadinessRing";

        [SerializeField] private BrawlerController _brawler;
        [SerializeField] private Color _superColor = new Color(1f, 0.78f, 0.08f, 0.92f);
        [SerializeField] private Color _hyperchargeColor = new Color(0.68f, 0.24f, 1f, 0.95f);
        [SerializeField, Min(0.2f)] private float _innerRadius = 0.92f;
        [SerializeField, Min(0.02f)] private float _ringWidth = 0.10f;
        [SerializeField, Range(180f, 355f)] private float _arcDegrees = 330f;
        [SerializeField, Range(8, 96)] private int _segments = 48;

        private GameObject _ringObject;
        private Mesh _ringMesh;
        private MeshRenderer _ringRenderer;
        private Material _ringMaterial;
        private BrawlerChargeRingKind _displayedKind;

        private void Awake()
        {
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            EnsureRing();
            SetDisplayedKind(BrawlerChargeRingKind.None);
        }

        private void OnDisable()
        {
            SetDisplayedKind(BrawlerChargeRingKind.None);
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(_ringMaterial);
            DestroyGeneratedObject(_ringMesh);
        }

        public void Bind(BrawlerController brawler)
        {
            _brawler = brawler;
            EnsureRing();
            RefreshState();
        }

        private void LateUpdate()
        {
            RefreshState();
        }

        private void RefreshState()
        {
            BrawlerState state = _brawler != null ? _brawler.State : null;
            if (state == null || state.IsDead)
            {
                SetDisplayedKind(BrawlerChargeRingKind.None);
                return;
            }

            bool hyperchargeAvailable = state.GetCurrentHyperchargeDefinition() != null;
            BrawlerChargeRingKind kind = BrawlerChargeRingUtility.Resolve(
                state.SuperCharge != null && state.SuperCharge.IsReady,
                hyperchargeAvailable,
                state.Hypercharge != null &&
                    !state.Hypercharge.IsActive &&
                    state.Hypercharge.ChargePercent >= 1f);
            SetDisplayedKind(kind);
        }

        private void SetDisplayedKind(BrawlerChargeRingKind kind)
        {
            _displayedKind = kind;
            if (_ringObject == null)
                return;

            bool visible = kind != BrawlerChargeRingKind.None;
            if (_ringObject.activeSelf != visible)
                _ringObject.SetActive(visible);

            if (!visible || _ringMaterial == null)
                return;

            _ringMaterial.color = kind == BrawlerChargeRingKind.Hypercharge
                ? _hyperchargeColor
                : _superColor;
        }

        private void EnsureRing()
        {
            if (_ringObject != null)
                return;

            Transform existing = transform.Find(RingObjectName);
            _ringObject = existing != null
                ? existing.gameObject
                : new GameObject(RingObjectName);
            _ringObject.layer = gameObject.layer;
            _ringObject.transform.SetParent(transform, false);
            _ringObject.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            _ringObject.transform.localRotation = Quaternion.identity;
            _ringObject.transform.localScale = Vector3.one;

            MeshFilter filter = _ringObject.GetComponent<MeshFilter>();
            if (filter == null)
                filter = _ringObject.AddComponent<MeshFilter>();

            _ringRenderer = _ringObject.GetComponent<MeshRenderer>();
            if (_ringRenderer == null)
                _ringRenderer = _ringObject.AddComponent<MeshRenderer>();

            _ringMesh = BuildRingMesh();
            filter.sharedMesh = _ringMesh;
            _ringMaterial = CreateRingMaterial();
            if (_ringMaterial != null)
                _ringRenderer.sharedMaterial = _ringMaterial;

            _ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;
        }

        private Mesh BuildRingMesh()
        {
            int safeSegments = Mathf.Clamp(_segments, 8, 96);
            float inner = Mathf.Max(0.2f, _innerRadius);
            float outer = inner + Mathf.Max(0.02f, _ringWidth);
            float radians = Mathf.Clamp(_arcDegrees, 180f, 355f) * Mathf.Deg2Rad;
            var vertices = new Vector3[(safeSegments + 1) * 2];
            var triangles = new int[safeSegments * 6];

            for (int i = 0; i <= safeSegments; i++)
            {
                float angle = radians * i / safeSegments;
                float x = Mathf.Sin(angle);
                float z = Mathf.Cos(angle);
                int vertex = i * 2;
                vertices[vertex] = new Vector3(x * inner, 0f, z * inner);
                vertices[vertex + 1] = new Vector3(x * outer, 0f, z * outer);

                if (i == safeSegments)
                    continue;

                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            var mesh = new Mesh { name = "RuntimeChargeReadinessRing" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateRingMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "Runtime_ChargeReadinessRing",
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
