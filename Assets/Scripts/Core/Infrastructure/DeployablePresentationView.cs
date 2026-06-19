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
        private MaterialPropertyBlock _propertyBlock;
        private Color _teamColor;
        private Color _muzzleColor;

        public void Build(DeployableController controller)
        {
            if (controller == null || controller.Definition == null)
                return;

            ClearRuntimeRoot();
            HideDefaultBlockoutRenderer();

            _teamColor = ResolveTeamColor(controller.Team);
            _muzzleColor = new Color(1f, 0.68f, 0.18f, 0.92f);

            if (controller.Definition.DeployableType == DeployableType.Turret)
                BuildTurret();
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
