using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class DeployablePresentationView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static Material _opaqueMaterial;
        private static Material _transparentMaterial;

        private readonly Color _metalColor = new Color(0.12f, 0.13f, 0.16f, 1f);
        private readonly Color _darkMetalColor = new Color(0.06f, 0.07f, 0.09f, 1f);

        private Transform _runtimeRoot;
        private Transform _turretHead;
        private Renderer _muzzleRenderer;
        private Renderer _teamRingRenderer;
        private Renderer _trapPlateRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Color _teamColor;
        private Color _muzzleColor;
        private bool _isTrapPresentation;
        private bool _trapIndicatorVisible = true;
        private float _trapIndicatorIntensity;

        public void Build(DeployableController controller)
        {
            if (controller == null || controller.Definition == null)
                return;

            ClearRuntimeRoot();
            HideDefaultBlockoutRenderer();

            _teamColor = ResolveTeamColor(controller.Team);
            _muzzleColor = new Color(1f, 0.68f, 0.18f, 0.92f);
            _isTrapPresentation = false;
            _trapIndicatorVisible = true;
            _trapIndicatorIntensity = 0f;
            _trapPlateRenderer = null;

            if (controller.Definition.DeployableType == DeployableType.Turret)
                BuildTurret();
            else if (controller.Definition.DeployableType == DeployableType.Trap)
                BuildTrapMine();
            else
                BuildFallbackBeacon();
        }

        public void SetAimDirection(Vector3 direction)
        {
            if (_turretHead == null)
                return;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            _turretHead.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public void TickPresentation(float deltaTime)
        {
            if (_runtimeRoot == null)
                return;

            if (_isTrapPresentation)
            {
                ApplyTrapIndicatorPresentation();
                return;
            }

            float pulse = 0.78f + Mathf.PingPong(Time.time * 4.6f, 0.22f);

            if (_muzzleRenderer != null)
            {
                Color muzzle = _muzzleColor;
                muzzle.a *= pulse;
                ApplyRendererColor(_muzzleRenderer, muzzle);
            }

            if (_teamRingRenderer != null)
            {
                Color ring = _teamColor;
                ring.a = 0.34f + Mathf.PingPong(Time.time * 1.8f, 0.16f);
                ApplyRendererColor(_teamRingRenderer, ring);
            }
        }

        public void SetTrapIndicator(float intensity, bool visible)
        {
            _trapIndicatorIntensity = Mathf.Clamp01(intensity);
            _trapIndicatorVisible = visible;
        }

        public static void SpawnTrapExplosionEffect(Vector3 position, TeamType team, float radius)
        {
            GameObject effect = new GameObject("TrapMineExplosionEffect");
            effect.transform.position = position;
            TrapExplosionEffect view = effect.AddComponent<TrapExplosionEffect>();
            view.Initialize(team, radius);
        }

        private void BuildTurret()
        {
            _runtimeRoot = CreateRoot("RuntimeDeployablePresentation");

            CreatePrimitive(
                _runtimeRoot,
                "TeamRing",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.035f, 0f),
                new Vector3(1.16f, 0.012f, 1.16f),
                Quaternion.identity,
                _teamColor.WithAlpha(0.38f),
                true,
                out _teamRingRenderer);

            CreatePrimitive(
                _runtimeRoot,
                "CannonBase",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.13f, 0f),
                new Vector3(0.86f, 0.12f, 0.86f),
                Quaternion.identity,
                _darkMetalColor,
                false,
                out _);

            CreatePrimitive(
                _runtimeRoot,
                "CannonPedestal",
                PrimitiveType.Cube,
                new Vector3(0f, 0.36f, 0f),
                new Vector3(0.52f, 0.36f, 0.52f),
                Quaternion.identity,
                _metalColor,
                false,
                out _);

            GameObject head = new GameObject("CannonHead");
            head.transform.SetParent(_runtimeRoot, false);
            head.transform.localPosition = new Vector3(0f, 0.66f, 0f);
            _turretHead = head.transform;

            CreatePrimitive(
                _turretHead,
                "CannonHousing",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(0.68f, 0.38f, 0.56f),
                Quaternion.identity,
                _teamColor,
                false,
                out _);

            CreatePrimitive(
                _turretHead,
                "CannonBarrel",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0f, 0.52f),
                new Vector3(0.16f, 0.46f, 0.16f),
                Quaternion.Euler(90f, 0f, 0f),
                _darkMetalColor,
                false,
                out _);

            CreatePrimitive(
                _turretHead,
                "MuzzleGlow",
                PrimitiveType.Sphere,
                new Vector3(0f, 0f, 0.94f),
                new Vector3(0.24f, 0.24f, 0.24f),
                Quaternion.identity,
                _muzzleColor,
                true,
                out _muzzleRenderer);
        }

        private void BuildFallbackBeacon()
        {
            _runtimeRoot = CreateRoot("RuntimeDeployablePresentation");

            CreatePrimitive(
                _runtimeRoot,
                "DeployableBeacon",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.24f, 0f),
                new Vector3(0.74f, 0.24f, 0.74f),
                Quaternion.identity,
                _teamColor,
                false,
                out _);
        }

        private void BuildTrapMine()
        {
            _isTrapPresentation = true;
            _runtimeRoot = CreateRoot("RuntimeDeployablePresentation");

            CreatePrimitive(
                _runtimeRoot,
                "MineTeamRing",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.018f, 0f),
                new Vector3(0.92f, 0.010f, 0.92f),
                Quaternion.identity,
                _teamColor.WithAlpha(0.32f),
                true,
                out _teamRingRenderer);

            CreatePrimitive(
                _runtimeRoot,
                "MineOuterDisc",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.055f, 0f),
                new Vector3(0.62f, 0.050f, 0.62f),
                Quaternion.identity,
                _darkMetalColor,
                false,
                out _);

            CreatePrimitive(
                _runtimeRoot,
                "MinePressurePlate",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.115f, 0f),
                new Vector3(0.42f, 0.030f, 0.42f),
                Quaternion.identity,
                _teamColor,
                false,
                out _trapPlateRenderer);

            CreatePrimitive(
                _runtimeRoot,
                "TripWireFront",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.155f, 0.18f),
                new Vector3(0.020f, 0.34f, 0.020f),
                Quaternion.Euler(0f, 0f, 90f),
                _metalColor,
                false,
                out _);

            CreatePrimitive(
                _runtimeRoot,
                "TripWireBack",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.155f, -0.18f),
                new Vector3(0.020f, 0.34f, 0.020f),
                Quaternion.Euler(0f, 0f, 90f),
                _metalColor,
                false,
                out _);

            CreatePrimitive(
                _runtimeRoot,
                "WarningLamp",
                PrimitiveType.Sphere,
                new Vector3(0f, 0.245f, 0f),
                new Vector3(0.16f, 0.16f, 0.16f),
                Quaternion.identity,
                _muzzleColor,
                true,
                out _muzzleRenderer);
        }

        private void ApplyTrapIndicatorPresentation()
        {
            if (_runtimeRoot == null)
                return;

            _runtimeRoot.gameObject.SetActive(_trapIndicatorVisible);
            if (!_trapIndicatorVisible)
                return;

            float eased = Mathf.SmoothStep(0f, 1f, _trapIndicatorIntensity);
            float scale = 1f + eased * 0.16f;
            _runtimeRoot.localScale = new Vector3(scale, 1f, scale);

            if (_teamRingRenderer != null)
            {
                Color ring = Color.Lerp(_teamColor.WithAlpha(0.20f), new Color(1f, 0.78f, 0.14f, 0.74f), eased);
                ApplyRendererColor(_teamRingRenderer, ring);
            }

            if (_trapPlateRenderer != null)
            {
                Color plate = Color.Lerp(_teamColor, new Color(1f, 0.34f, 0.08f, 1f), eased);
                ApplyRendererColor(_trapPlateRenderer, plate);
            }

            if (_muzzleRenderer != null)
            {
                Color lamp = Color.Lerp(new Color(1f, 0.68f, 0.18f, 0.28f), new Color(1f, 0.12f, 0.02f, 1f), eased);
                ApplyRendererColor(_muzzleRenderer, lamp);
            }
        }

        private Transform CreateRoot(string rootName)
        {
            GameObject root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root.transform;
        }

        private Transform CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Color color,
            bool transparent,
            out Renderer renderer)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            renderer = go.GetComponent<Renderer>();
            ConfigureRenderer(renderer, color, transparent);
            return go.transform;
        }

        private void ConfigureRenderer(Renderer renderer, Color color, bool transparent)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = transparent
                ? ShadowCastingMode.Off
                : ShadowCastingMode.On;
            renderer.receiveShadows = !transparent;
            renderer.sharedMaterial = transparent
                ? ResolveTransparentMaterial()
                : ResolveOpaqueMaterial();
            ApplyRendererColor(renderer, color);
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private void HideDefaultBlockoutRenderer()
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        private void ClearRuntimeRoot()
        {
            Transform existing = transform.Find("RuntimeDeployablePresentation");
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        private Color ResolveTeamColor(TeamType team)
        {
            return ResolveTeamColorValue(team);
        }

        private static Color ResolveTeamColorValue(TeamType team)
        {
            if (team == TeamType.Red)
                return new Color(1f, 0.24f, 0.18f, 1f);

            if (team == TeamType.Blue)
                return new Color(0.18f, 0.58f, 1f, 1f);

            return new Color(1f, 0.76f, 0.18f, 1f);
        }

        private static Material ResolveOpaqueMaterial()
        {
            if (_opaqueMaterial != null)
                return _opaqueMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            _opaqueMaterial = new Material(shader);
            return _opaqueMaterial;
        }

        private static Material ResolveTransparentMaterial()
        {
            if (_transparentMaterial != null)
                return _transparentMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            _transparentMaterial = new Material(shader);
            _transparentMaterial.color = Color.white;
            _transparentMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _transparentMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _transparentMaterial.SetInt("_ZWrite", 0);
            _transparentMaterial.DisableKeyword("_ALPHATEST_ON");
            _transparentMaterial.EnableKeyword("_ALPHABLEND_ON");
            _transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _transparentMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _transparentMaterial;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private sealed class TrapExplosionEffect : MonoBehaviour
        {
            private const float Duration = 0.55f;

            private readonly Renderer[] _renderers = new Renderer[5];
            private readonly Vector3[] _baseScales = new Vector3[5];
            private readonly Color[] _baseColors = new Color[5];

            private MaterialPropertyBlock _propertyBlock;
            private float _startTime;
            private float _radius;
            private Color _teamColor;

            public void Initialize(TeamType team, float radius)
            {
                _startTime = Time.time;
                _radius = Mathf.Max(0.4f, radius);
                _teamColor = ResolveTeamColorValue(team);

                CreateEffectPrimitive(
                    0,
                    "Shockwave",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.045f, 0f),
                    new Vector3(_radius * 0.35f, 0.012f, _radius * 0.35f),
                    Quaternion.identity,
                    new Color(1f, 0.78f, 0.15f, 0.62f));

                CreateEffectPrimitive(
                    1,
                    "BlastCore",
                    PrimitiveType.Sphere,
                    new Vector3(0f, 0.26f, 0f),
                    new Vector3(_radius * 0.28f, _radius * 0.18f, _radius * 0.28f),
                    Quaternion.identity,
                    new Color(1f, 0.42f, 0.08f, 0.82f));

                CreateEffectPrimitive(
                    2,
                    "TeamFlash",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.075f, 0f),
                    new Vector3(_radius * 0.18f, 0.010f, _radius * 0.18f),
                    Quaternion.identity,
                    _teamColor.WithAlpha(0.52f));

                CreateEffectPrimitive(
                    3,
                    "SparkA",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.22f, 0f),
                    new Vector3(0.045f, _radius * 0.36f, 0.045f),
                    Quaternion.Euler(0f, 0f, 64f),
                    new Color(1f, 0.92f, 0.32f, 0.86f));

                CreateEffectPrimitive(
                    4,
                    "SparkB",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.20f, 0f),
                    new Vector3(0.040f, _radius * 0.30f, 0.040f),
                    Quaternion.Euler(0f, 0f, -48f),
                    new Color(1f, 0.58f, 0.12f, 0.82f));
            }

            private void Update()
            {
                float t = Mathf.Clamp01((Time.time - _startTime) / Duration);
                float expand = Mathf.SmoothStep(0f, 1f, t);
                float alpha = 1f - expand;

                SetScaleAndAlpha(0, new Vector3(_radius * 2.08f, 0.012f, _radius * 2.08f), alpha * 0.54f, expand);
                SetScaleAndAlpha(1, new Vector3(_radius * 0.52f, _radius * 0.28f, _radius * 0.52f), alpha * 0.72f, expand);
                SetScaleAndAlpha(2, new Vector3(_radius * 1.28f, 0.010f, _radius * 1.28f), alpha * 0.36f, expand);
                SetScaleAndAlpha(3, new Vector3(0.045f, _radius * 0.58f, 0.045f), alpha * 0.70f, expand);
                SetScaleAndAlpha(4, new Vector3(0.040f, _radius * 0.48f, 0.040f), alpha * 0.64f, expand);

                if (t >= 1f)
                    Destroy(gameObject);
            }

            private void CreateEffectPrimitive(
                int index,
                string objectName,
                PrimitiveType primitiveType,
                Vector3 localPosition,
                Vector3 localScale,
                Quaternion localRotation,
                Color color)
            {
                GameObject primitive = GameObject.CreatePrimitive(primitiveType);
                primitive.name = objectName;
                primitive.transform.SetParent(transform, false);
                primitive.transform.localPosition = localPosition;
                primitive.transform.localRotation = localRotation;
                primitive.transform.localScale = localScale;

                Collider collider = primitive.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                Renderer renderer = primitive.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sharedMaterial = ResolveTransparentMaterial();
                    ApplyColor(renderer, color);
                }

                _renderers[index] = renderer;
                _baseScales[index] = localScale;
                _baseColors[index] = color;
            }

            private void SetScaleAndAlpha(int index, Vector3 targetScale, float alpha, float t)
            {
                Renderer renderer = _renderers[index];
                if (renderer == null)
                    return;

                renderer.transform.localScale = Vector3.Lerp(_baseScales[index], targetScale, t);

                Color color = _baseColors[index];
                color.a = Mathf.Clamp01(alpha);
                ApplyColor(renderer, color);
            }

            private void ApplyColor(Renderer renderer, Color color)
            {
                if (renderer == null)
                    return;

                EnsurePropertyBlock();
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ColorId, color);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(EmissionColorId, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }

            private void EnsurePropertyBlock()
            {
                if (_propertyBlock == null)
                    _propertyBlock = new MaterialPropertyBlock();
            }
        }
    }

    internal static class DeployablePresentationColorExtensions
    {
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
