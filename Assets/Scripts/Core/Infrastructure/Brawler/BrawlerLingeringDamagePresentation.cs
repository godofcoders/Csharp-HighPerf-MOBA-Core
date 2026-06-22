using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerLingeringDamagePresentation : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private BrawlerController _brawler;
        [SerializeField] private float _visibleSeconds = 0.72f;
        [SerializeField] private float _pulseSpeed = 8.5f;
        [SerializeField] private Color _burnColor = new Color(1f, 0.36f, 0.05f, 0.48f);
        [SerializeField] private Color _poisonEdgeColor = new Color(0.30f, 1f, 0.16f, 0.44f);

        private Material _overlayMaterial;
        private MaterialPropertyBlock _propertyBlock;
        private GameObject _root;
        private Transform _edgeOverlay;
        private Transform[] _wisps;
        private Renderer _edgeRenderer;
        private Renderer[] _wispRenderers;
        private float _visibleUntilTime;
        private Color _activeEdgeColor;

        private void Awake()
        {
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            EnsureVisuals();
            SetVisible(false);
        }

        private void OnEnable()
        {
            CombatPresentationEventBus.OnEvent += HandleCombatPresentationEvent;
        }

        private void OnDisable()
        {
            CombatPresentationEventBus.OnEvent -= HandleCombatPresentationEvent;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_overlayMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_overlayMaterial);
            else
                DestroyImmediate(_overlayMaterial);
        }

        public void Bind(BrawlerController brawler)
        {
            _brawler = brawler;
            EnsureVisuals();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_root == null || !_root.activeSelf)
                return;

            if (_brawler == null || _brawler.State == null || _brawler.State.IsDead)
            {
                SetVisible(false);
                return;
            }

            if (Time.time >= _visibleUntilTime)
            {
                SetVisible(false);
                return;
            }

            float remaining = Mathf.Clamp01((_visibleUntilTime - Time.time) / Mathf.Max(0.01f, _visibleSeconds));
            float pulse = 0.82f + Mathf.Sin(Time.time * _pulseSpeed) * 0.18f;
            float alphaScale = Mathf.Clamp01(remaining * 1.4f) * pulse;

            if (_edgeOverlay != null)
            {
                float edgeScale = 0.95f + pulse * 0.16f;
                _edgeOverlay.localScale = new Vector3(edgeScale, 0.018f, edgeScale);
            }

            ApplyColor(_edgeRenderer, _activeEdgeColor, alphaScale);

            if (_wisps == null || _wispRenderers == null)
                return;

            for (int i = 0; i < _wisps.Length; i++)
            {
                Transform wisp = _wisps[i];
                if (wisp == null)
                    continue;

                float phase = Time.time * (2.8f + i * 0.35f) + i * 1.7f;
                float angle = phase * 52f + i * 120f;
                float radius = 0.42f + Mathf.Sin(phase) * 0.08f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                offset.y = 0.28f + Mathf.PingPong(phase * 0.16f, 0.34f);
                wisp.localPosition = offset;
                wisp.localRotation = Quaternion.Euler(0f, angle, 18f + Mathf.Sin(phase) * 10f);
                wisp.localScale = new Vector3(0.08f, 0.20f + pulse * 0.06f, 0.08f);

                if (i < _wispRenderers.Length)
                    ApplyColor(_wispRenderers[i], _activeEdgeColor, alphaScale);
            }
        }

        private void HandleCombatPresentationEvent(CombatPresentationEvent evt)
        {
            if (!evt.IsLingeringAreaEffect ||
                evt.EventType != CombatPresentationEventType.DamageHit ||
                evt.Target == null ||
                evt.Target != _brawler)
            {
                return;
            }

            if (IsBarleyLingering(evt))
                return;

            bool poison = IsPoisonLike(evt);
            _activeEdgeColor = poison ? _poisonEdgeColor : _burnColor;
            _visibleUntilTime = Time.time + Mathf.Max(0.05f, _visibleSeconds);

            EnsureVisuals();
            SetVisible(true);
        }

        private bool IsBarleyLingering(CombatPresentationEvent evt)
        {
            string key = evt.AbilityDefinition != null
                ? evt.AbilityDefinition.name.ToLowerInvariant()
                : string.Empty;

            return key.Contains("barley") || key.Contains("puddle");
        }

        private bool IsPoisonLike(CombatPresentationEvent evt)
        {
            string key = evt.AbilityDefinition != null
                ? evt.AbilityDefinition.name.ToLowerInvariant()
                : string.Empty;

            return key.Contains("poison") || key.Contains("toxic") || key.Contains("venom");
        }

        private void EnsureVisuals()
        {
            if (_root != null)
                return;

            _root = new GameObject("LingeringDamageOverlay");
            _root.transform.SetParent(transform, false);
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;

            _edgeOverlay = CreatePrimitive(
                _root.transform,
                "GroundBurn",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.035f, 0f),
                new Vector3(0.95f, 0.018f, 0.95f),
                Quaternion.identity,
                out _edgeRenderer);

            _wisps = new Transform[3];
            _wispRenderers = new Renderer[3];
            for (int i = 0; i < _wisps.Length; i++)
            {
                _wisps[i] = CreatePrimitive(
                    _root.transform,
                    "DamageWisp",
                    PrimitiveType.Cube,
                    Vector3.up * 0.55f,
                    new Vector3(0.1f, 0.32f, 0.1f),
                    Quaternion.identity,
                    out _wispRenderers[i]);
            }
        }

        private Transform CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            out Renderer renderer)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRotation;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ResolveOverlayMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return go.transform;
        }

        private void ApplyColor(Renderer renderer, Color color, float alphaScale)
        {
            if (renderer == null)
                return;

            Color applied = color;
            applied.a *= Mathf.Clamp01(alphaScale);

            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, applied);
            _propertyBlock.SetColor(BaseColorId, applied);
            _propertyBlock.SetColor(EmissionColorId, applied);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        private Material ResolveOverlayMaterial()
        {
            if (_overlayMaterial != null)
                return _overlayMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            _overlayMaterial = new Material(shader);
            _overlayMaterial.color = Color.white;
            _overlayMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _overlayMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _overlayMaterial.SetInt("_ZWrite", 0);
            _overlayMaterial.DisableKeyword("_ALPHATEST_ON");
            _overlayMaterial.EnableKeyword("_ALPHABLEND_ON");
            _overlayMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _overlayMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _overlayMaterial;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }
    }
}
