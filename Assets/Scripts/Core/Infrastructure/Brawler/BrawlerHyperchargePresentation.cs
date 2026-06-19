using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
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
}
