using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerStealthPresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private BrawlerController _brawler;
        [SerializeField] private Color _hiddenAllyTint = new Color(0.72f, 1f, 0.82f, 1f);
        [SerializeField] private float _hiddenAllyAlpha = 0.56f;
        [SerializeField] private float _hiddenAllyTintStrength = 0.38f;
        [SerializeField] private float _hiddenEnemyAlpha = 0f;
        [SerializeField] private float _fadeSpeed = 8.5f;

        private Renderer[] _visualRenderers;
        private Material[][] _originalSharedMaterials;
        private Material[][] _runtimeMaterials;
        private Color[][] _baseColors;
        private float _currentAlpha = 1f;
        private float _targetAlpha = 1f;
        private bool _materialsPrepared;
        private Color _targetTint = Color.white;
        private float _targetTintStrength;

        private void Awake()
        {
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            RefreshVisualRenderers();
        }

        private void OnDisable()
        {
            SetRenderersEnabled(true);
            ApplyAlpha(1f, Color.white, 0f);
        }

        private void OnDestroy()
        {
            RestoreOriginalMaterials();
        }

        public void Bind(BrawlerController brawler)
        {
            _brawler = brawler;
            RefreshVisualRenderers();
            _currentAlpha = 1f;
            _targetAlpha = 1f;
            ApplyAlpha(1f, Color.white, 0f);
        }

        private void LateUpdate()
        {
            RefreshStealthPresentation();

            float nextAlpha = Mathf.MoveTowards(
                _currentAlpha,
                _targetAlpha,
                Mathf.Max(0f, _fadeSpeed) * Time.deltaTime);

            ApplyAlpha(nextAlpha, _targetTint, _targetTintStrength);
        }

        public void RefreshStealthPresentation()
        {
            if (_brawler == null || _brawler.State == null || _brawler.State.IsDead)
            {
                SetTargetVisibility(1f, Color.white, 0f);
                return;
            }

            if (!BrawlerController.TryGetLocalObserverTeam(out TeamType observerTeam))
            {
                SetTargetVisibility(1f, Color.white, 0f);
                return;
            }

            bool sameTeam = TeamRelationshipUtility.AreAllies(observerTeam, _brawler.Team);

            uint currentTick = ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock)
                ? clock.CurrentTick
                : 0u;

            bool hiddenInGrass = _brawler.State.Stealth.IsHidden(currentTick);
            if (!hiddenInGrass)
            {
                SetTargetVisibility(1f, Color.white, 0f);
                return;
            }

            if (sameTeam)
            {
                SetTargetVisibility(
                    Mathf.Clamp01(_hiddenAllyAlpha),
                    _hiddenAllyTint,
                    Mathf.Clamp01(_hiddenAllyTintStrength));
                return;
            }

            SetTargetVisibility(Mathf.Clamp01(_hiddenEnemyAlpha), Color.white, 0f);
        }

        private void SetTargetVisibility(float alpha, Color tint, float tintStrength)
        {
            _targetAlpha = Mathf.Clamp01(alpha);
            _targetTint = tint;
            _targetTintStrength = Mathf.Clamp01(tintStrength);

            if (_targetAlpha > 0.01f)
                SetRenderersEnabled(true);
        }

        private void RefreshVisualRenderers()
        {
            RestoreOriginalMaterials();

            BrawlerController root = _brawler != null ? _brawler : GetComponent<BrawlerController>();
            GameObject visualModel = root != null ? root.VisualModel : null;
            _visualRenderers = visualModel != null
                ? visualModel.GetComponentsInChildren<Renderer>(true)
                : root != null
                    ? root.GetComponentsInChildren<Renderer>(true)
                    : GetComponentsInChildren<Renderer>(true);

            PrepareRuntimeMaterials();
        }

        private void PrepareRuntimeMaterials()
        {
            if (_visualRenderers == null || _visualRenderers.Length == 0)
                return;

            _originalSharedMaterials = new Material[_visualRenderers.Length][];
            _runtimeMaterials = new Material[_visualRenderers.Length][];
            _baseColors = new Color[_visualRenderers.Length][];

            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                Renderer renderer = _visualRenderers[i];
                if (renderer == null)
                    continue;

                Material[] sourceMaterials = renderer.sharedMaterials;
                _originalSharedMaterials[i] = sourceMaterials;
                _runtimeMaterials[i] = new Material[sourceMaterials.Length];
                _baseColors[i] = new Color[sourceMaterials.Length];

                for (int m = 0; m < sourceMaterials.Length; m++)
                {
                    Material source = sourceMaterials[m];
                    if (source == null)
                        continue;

                    Material runtime = new Material(source);
                    ConfigureTransparentMaterial(runtime);
                    _runtimeMaterials[i][m] = runtime;
                    _baseColors[i][m] = ReadMaterialColor(source);
                }

                renderer.sharedMaterials = _runtimeMaterials[i];
            }

            _materialsPrepared = true;
        }

        private void ApplyAlpha(float alpha, Color tint, float tintStrength)
        {
            _currentAlpha = Mathf.Clamp01(alpha);

            if (!_materialsPrepared)
                PrepareRuntimeMaterials();

            if (_runtimeMaterials == null || _baseColors == null)
                return;

            for (int i = 0; i < _runtimeMaterials.Length; i++)
            {
                if (_runtimeMaterials[i] == null || _baseColors[i] == null)
                    continue;

                for (int m = 0; m < _runtimeMaterials[i].Length; m++)
                {
                    Material material = _runtimeMaterials[i][m];
                    if (material == null)
                        continue;

                    Color color = Color.Lerp(_baseColors[i][m], tint, tintStrength);
                    color.a = _currentAlpha;
                    WriteMaterialColor(material, color);
                }
            }

            if (_currentAlpha <= 0.01f && _targetAlpha <= 0.01f)
                SetRenderersEnabled(false);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_visualRenderers == null)
                return;

            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                Renderer renderer = _visualRenderers[i];
                if (renderer == null)
                    continue;

                if (renderer.enabled != enabled)
                    renderer.enabled = enabled;
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (_visualRenderers != null && _originalSharedMaterials != null)
            {
                for (int i = 0; i < _visualRenderers.Length; i++)
                {
                    if (_visualRenderers[i] != null &&
                        _originalSharedMaterials.Length > i &&
                        _originalSharedMaterials[i] != null)
                    {
                        _visualRenderers[i].sharedMaterials = _originalSharedMaterials[i];
                    }
                }
            }

            if (_runtimeMaterials != null)
            {
                for (int i = 0; i < _runtimeMaterials.Length; i++)
                {
                    if (_runtimeMaterials[i] == null)
                        continue;

                    for (int m = 0; m < _runtimeMaterials[i].Length; m++)
                        DestroyGeneratedObject(_runtimeMaterials[i][m]);
                }
            }

            _originalSharedMaterials = null;
            _runtimeMaterials = null;
            _baseColors = null;
            _materialsPrepared = false;
        }

        private static Color ReadMaterialColor(Material material)
        {
            if (material == null)
                return Color.white;

            if (material.HasProperty(BaseColorId))
                return material.GetColor(BaseColorId);

            if (material.HasProperty(ColorId))
                return material.GetColor(ColorId);

            return Color.white;
        }

        private static void WriteMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            material.SetOverrideTag("RenderType", "Transparent");

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
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
