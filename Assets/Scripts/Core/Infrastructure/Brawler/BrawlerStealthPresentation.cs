using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerStealthPresentation : MonoBehaviour
    {
        [SerializeField] private BrawlerController _brawler;
        [SerializeField] private Color _hiddenAllyColor = new Color(0.2f, 0.95f, 0.72f, 0.24f);
        [SerializeField] private float _indicatorRadius = 1.05f;
        [SerializeField] private float _pulseSpeed = 4.5f;

        private GameObject _indicatorObject;
        private Renderer _indicatorRenderer;
        private Material _indicatorMaterial;
        private bool _visible;

        private void Awake()
        {
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            EnsureIndicator();
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_indicatorMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_indicatorMaterial);
            else
                DestroyImmediate(_indicatorMaterial);
        }

        public void Bind(BrawlerController brawler)
        {
            _brawler = brawler;
            EnsureIndicator();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            bool shouldShow = ShouldShowHiddenAllyIndicator();
            SetVisible(shouldShow);

            if (!_visible || _indicatorObject == null)
                return;

            float pulse = 0.92f + Mathf.Sin(Time.time * _pulseSpeed) * 0.08f;
            float radius = _indicatorRadius * pulse;
            _indicatorObject.transform.localScale = new Vector3(radius, 0.018f, radius);
        }

        private bool ShouldShowHiddenAllyIndicator()
        {
            if (_brawler == null || _brawler.State == null || _brawler.State.IsDead)
                return false;

            if (!BrawlerController.TryGetLocalObserverTeam(out TeamType observerTeam))
                return false;

            if (observerTeam != _brawler.Team)
                return false;

            uint currentTick = ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock)
                ? clock.CurrentTick
                : 0u;

            return _brawler.State.Stealth.IsHidden(currentTick);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;

            if (_indicatorObject != null && _indicatorObject.activeSelf != visible)
                _indicatorObject.SetActive(visible);
        }

        private void EnsureIndicator()
        {
            if (_indicatorObject != null)
                return;

            _indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _indicatorObject.name = "StealthIndicator";
            _indicatorObject.transform.SetParent(transform, false);
            _indicatorObject.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            _indicatorObject.transform.localRotation = Quaternion.identity;
            _indicatorObject.transform.localScale = new Vector3(_indicatorRadius, 0.018f, _indicatorRadius);

            Collider collider = _indicatorObject.GetComponent<Collider>();
            if (collider != null)
                DestroyGeneratedObject(collider);

            _indicatorRenderer = _indicatorObject.GetComponent<Renderer>();
            _indicatorMaterial = CreateIndicatorMaterial();

            if (_indicatorRenderer != null)
            {
                if (_indicatorMaterial != null)
                    _indicatorRenderer.sharedMaterial = _indicatorMaterial;

                _indicatorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _indicatorRenderer.receiveShadows = false;
            }

            _indicatorObject.SetActive(false);
        }

        private Material CreateIndicatorMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.color = _hiddenAllyColor;

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
