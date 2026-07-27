using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class GemGrabStageDressingPresentation : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private const string StageRootName = "RuntimeGemGrabStageDressing";

        [Header("Mine")]
        [SerializeField, Min(0.5f)] private float _mineRadius = 2.45f;
        [SerializeField, Min(0)] private int _mountainRockClusterCount = 8;

        private Transform _stageRoot;
        private Material _darkSandMaterial;
        private Material _rockMaterial;
        private Material _metalMaterial;
        private Material _lampMaterial;

        public static void InstallUnder(GameObject root)
        {
            if (root == null || SceneSelection.SelectedMode != GameModeId.GemGrab)
                return;

            GemGrabStageDressingPresentation presentation =
                root.GetComponent<GemGrabStageDressingPresentation>();
            bool created = false;
            if (presentation == null)
            {
                presentation = root.AddComponent<GemGrabStageDressingPresentation>();
                created = true;
            }

            if (!created)
                presentation.RefreshStage();
        }

        private void Awake()
        {
            if (SceneSelection.SelectedMode == GameModeId.GemGrab)
                RefreshStage();
        }

        private void Start()
        {
            if (SceneSelection.SelectedMode == GameModeId.GemGrab)
                RefreshStage();
        }

        private void OnDestroy()
        {
            DestroyMaterial(_darkSandMaterial);
            DestroyMaterial(_rockMaterial);
            DestroyMaterial(_metalMaterial);
            DestroyMaterial(_lampMaterial);
        }

        public void RefreshStage()
        {
            if (SceneSelection.SelectedMode != GameModeId.GemGrab)
                return;

            if (!TryResolveGroundBounds(out Bounds bounds))
                return;

            EnsureStageRoot();
            ClearStageRoot();
            EnsureMaterials();

            float groundY = bounds.max.y;
            Vector3 mineCenter = ResolveGemMineCenter(bounds);
            mineCenter.y = groundY;

            BuildGemMine(mineCenter, groundY);
            BuildAmbientProps(bounds, mineCenter, groundY);
        }

        private void BuildGemMine(Vector3 center, float groundY)
        {
            float baseY = groundY + 0.04f;
            float rimY = groundY + 0.12f;

            CreatePrimitive(
                "GemMine_SewerConcreteBase",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.055f, 0f),
                new Vector3(_mineRadius * 0.86f, 0.045f, _mineRadius * 0.86f),
                Quaternion.identity,
                _rockMaterial,
                false);

            CreatePrimitive(
                "GemMine_SewerHole",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.112f, 0f),
                new Vector3(_mineRadius * 0.48f, 0.02f, _mineRadius * 0.48f),
                Quaternion.identity,
                _darkSandMaterial,
                false);

            CreatePrimitive(
                "GemMine_SewerInnerGlow",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.126f, 0f),
                new Vector3(_mineRadius * 0.42f, 0.01f, _mineRadius * 0.42f),
                Quaternion.identity,
                _lampMaterial,
                false);

            CreatePrimitive(
                "GemMine_MetalSpawnRim",
                PrimitiveType.Cylinder,
                new Vector3(center.x, rimY, center.z),
                new Vector3(_mineRadius * 0.62f, 0.035f, _mineRadius * 0.62f),
                Quaternion.identity,
                _metalMaterial,
                false);

            CreatePrimitive(
                "GemMine_MovedSewerLid",
                PrimitiveType.Cylinder,
                center + new Vector3(1.68f, 0.16f, -0.82f),
                new Vector3(_mineRadius * 0.56f, 0.055f, _mineRadius * 0.56f),
                Quaternion.Euler(0f, 0f, -11f),
                _metalMaterial,
                false);

            CreatePrimitive(
                "GemMine_LidHandle",
                PrimitiveType.Cube,
                center + new Vector3(1.68f, 0.25f, -0.82f),
                new Vector3(0.58f, 0.07f, 0.12f),
                Quaternion.Euler(0f, -22f, -11f),
                _darkSandMaterial,
                false);

            CreatePrimitive(
                "GemMine_DrainGrate_H",
                PrimitiveType.Cube,
                center + new Vector3(-1.62f, 0.14f, 0.82f),
                new Vector3(0.95f, 0.045f, 0.16f),
                Quaternion.Euler(0f, 18f, 0f),
                _metalMaterial,
                false);

            CreatePrimitive(
                "GemMine_DrainGrate_V",
                PrimitiveType.Cube,
                center + new Vector3(-1.62f, 0.145f, 0.82f),
                new Vector3(0.16f, 0.045f, 0.72f),
                Quaternion.Euler(0f, 18f, 0f),
                _metalMaterial,
                false);

            CreatePrimitive(
                "GemMine_Runoff_A",
                PrimitiveType.Cylinder,
                new Vector3(center.x - 0.82f, baseY + 0.016f, center.z - 1.42f),
                new Vector3(0.44f, 0.008f, 1.08f),
                Quaternion.Euler(0f, -32f, 0f),
                _lampMaterial,
                false);

            CreatePrimitive(
                "GemMine_Runoff_B",
                PrimitiveType.Cylinder,
                new Vector3(center.x + 1.08f, baseY + 0.016f, center.z + 1.22f),
                new Vector3(0.34f, 0.008f, 0.86f),
                Quaternion.Euler(0f, 24f, 0f),
                _lampMaterial,
                false);

            CreatePrimitive(
                "GemMine_GemSpawnClearMarker",
                PrimitiveType.Cylinder,
                new Vector3(center.x, groundY + 0.17f, center.z),
                new Vector3(0.26f, 0.008f, 0.26f),
                Quaternion.identity,
                _metalMaterial,
                false);
        }

        private void BuildAmbientProps(Bounds bounds, Vector3 mineCenter, float groundY)
        {
            CreateRockPile("GemRockPile_A", new Vector3(bounds.min.x + 2.3f, groundY, mineCenter.z - 4.6f));
            CreateRockPile("GemRockPile_B", new Vector3(bounds.max.x - 2.5f, groundY, mineCenter.z + 3.8f));
            CreateRockPile("GemRockPile_C", new Vector3(mineCenter.x + 4.3f, groundY, bounds.min.z + 2.5f));
            CreateMountainRockClusters(bounds, groundY);
        }

        private void CreateRockPile(string prefix, Vector3 basePosition)
        {
            CreatePrimitive(prefix + "_0", PrimitiveType.Sphere, basePosition + new Vector3(0f, 0.18f, 0f), new Vector3(0.62f, 0.36f, 0.5f), Quaternion.identity, _rockMaterial, false);
            CreatePrimitive(prefix + "_1", PrimitiveType.Sphere, basePosition + new Vector3(0.42f, 0.14f, -0.16f), new Vector3(0.42f, 0.28f, 0.38f), Quaternion.identity, _darkSandMaterial, false);
            CreatePrimitive(prefix + "_2", PrimitiveType.Sphere, basePosition + new Vector3(-0.38f, 0.13f, 0.18f), new Vector3(0.38f, 0.26f, 0.34f), Quaternion.identity, _rockMaterial, false);
        }

        private void CreateMountainRockClusters(Bounds bounds, float groundY)
        {
            int count = Mathf.Clamp(_mountainRockClusterCount, 0, 10);
            for (int i = 0; i < count; i++)
            {
                Vector3 basePosition = ResolveMountainRockPosition(bounds, groundY, i, count);
                CreatePrimitive(
                    "MountainRock_" + i + "_Core",
                    PrimitiveType.Sphere,
                    basePosition + new Vector3(0f, 1.0f, 0f),
                    new Vector3(2.45f, 1.95f, 1.85f),
                    Quaternion.Euler(0f, i * 37f, 0f),
                    _rockMaterial,
                    false);

                CreatePrimitive(
                    "MountainRock_" + i + "_ShoulderA",
                    PrimitiveType.Sphere,
                    basePosition + new Vector3(1.32f, 0.62f, -0.44f),
                    new Vector3(1.55f, 1.12f, 1.28f),
                    Quaternion.Euler(0f, i * 23f, 0f),
                    _darkSandMaterial,
                    false);

                CreatePrimitive(
                    "MountainRock_" + i + "_ShoulderB",
                    PrimitiveType.Sphere,
                    basePosition + new Vector3(-1.08f, 0.52f, 0.58f),
                    new Vector3(1.38f, 1.0f, 1.14f),
                    Quaternion.Euler(0f, i * -29f, 0f),
                    _rockMaterial,
                    false);
            }
        }

        private static Vector3 ResolveMountainRockPosition(Bounds bounds, float groundY, int index, int count)
        {
            float t = (index + 0.37f) / Mathf.Max(1, count);
            bool sideEdge = index % 2 == 0;
            float x = sideEdge
                ? (index % 4 == 0 ? bounds.min.x + 1.75f : bounds.max.x - 1.75f)
                : Mathf.Lerp(bounds.min.x + 3.2f, bounds.max.x - 3.2f, Mathf.Repeat(t * 1.71f, 1f));
            float z = sideEdge
                ? Mathf.Lerp(bounds.min.z + 2.8f, bounds.max.z - 2.8f, Mathf.Repeat(t * 2.11f, 1f))
                : (index % 4 == 1 ? bounds.min.z + 1.65f : bounds.max.z - 1.65f);

            return new Vector3(x, groundY, z);
        }

        private GameObject CreatePrimitive(
            string objectName,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material,
            bool keepCollider)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(_stageRoot, true);
            primitive.transform.position = position;
            primitive.transform.rotation = rotation;
            primitive.transform.localScale = scale;
            ApplyRendererMaterial(primitive, material);

            if (!keepCollider)
                RemoveCollider(primitive);

            return primitive;
        }

        private static void ApplyRendererMaterial(GameObject primitive, Material material)
        {
            Renderer renderer = primitive != null ? primitive.GetComponent<Renderer>() : null;
            if (renderer == null)
                return;

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void RemoveCollider(GameObject primitive)
        {
            Collider collider = primitive != null ? primitive.GetComponent<Collider>() : null;
            if (collider != null)
                Destroy(collider);
        }

        private Vector3 ResolveGemMineCenter(Bounds bounds)
        {
            GemSpawner spawner = FindObjectOfType<GemSpawner>();
            if (spawner != null)
                return spawner.transform.position;

            return bounds.center;
        }

        private void EnsureStageRoot()
        {
            if (_stageRoot != null)
                return;

            Transform existing = transform.Find(StageRootName);
            if (existing != null)
            {
                _stageRoot = existing;
                return;
            }

            GameObject root = new GameObject(StageRootName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            _stageRoot = root.transform;
        }

        private void ClearStageRoot()
        {
            if (_stageRoot == null)
                return;

            for (int i = _stageRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _stageRoot.GetChild(i);
                if (child == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private bool TryResolveGroundBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            int excludedMask = ResolveObstacleMask() | ResolveLayerMask("Bushes") | ResolveLayerMask("Bush");

            Collider[] colliders = GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.isTrigger ||
                    !IsGroundCandidate(collider.gameObject, excludedMask))
                {
                    continue;
                }

                Encapsulate(collider.bounds, ref bounds, ref found);
            }

            if (found)
                return true;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !IsGroundCandidate(renderer.gameObject, excludedMask))
                {
                    continue;
                }

                Encapsulate(renderer.bounds, ref bounds, ref found);
            }

            return found;
        }

        private bool IsGroundCandidate(GameObject candidate, int excludedMask)
        {
            if (candidate == null)
                return false;

            if (_stageRoot != null && candidate.transform.IsChildOf(_stageRoot))
                return false;

            string objectName = candidate.name;
            if (objectName.IndexOf(StageRootName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("RuntimeArenaBoundary", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("ArenaWall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Poison", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            int layerMask = 1 << candidate.layer;
            return (excludedMask & layerMask) == 0;
        }

        private static void Encapsulate(Bounds candidate, ref Bounds bounds, ref bool found)
        {
            if (!found)
            {
                bounds = candidate;
                found = true;
                return;
            }

            bounds.Encapsulate(candidate);
        }

        private void EnsureMaterials()
        {
            _darkSandMaterial = EnsureMaterial(_darkSandMaterial, "Runtime_GemGrab_DarkSand", new Color(0.31f, 0.22f, 0.15f, 1f), false);
            _rockMaterial = EnsureMaterial(_rockMaterial, "Runtime_GemGrab_Rock", new Color(0.66f, 0.48f, 0.30f, 1f), false);
            _metalMaterial = EnsureMaterial(_metalMaterial, "Runtime_GemGrab_Metal", new Color(0.16f, 0.14f, 0.13f, 1f), false);
            _lampMaterial = EnsureMaterial(_lampMaterial, "Runtime_GemGrab_WetAmber", new Color(1f, 0.66f, 0.22f, 0.22f), true);
        }

        private static Material EnsureMaterial(Material current, string materialName, Color color, bool transparent)
        {
            if (current != null)
                return current;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Sprites/Default") ??
                            Shader.Find("Standard");
            if (shader == null)
                return null;

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);

            if (transparent)
                ConfigureTransparentMaterial(material);

            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetMaterialFloatIfPresent(material, SurfaceId, 1f);
            SetMaterialFloatIfPresent(material, ModeId, 3f);
            SetMaterialFloatIfPresent(material, BlendId, 0f);
            SetMaterialFloatIfPresent(material, AlphaClipId, 0f);
            SetMaterialFloatIfPresent(material, CullId, (float)CullMode.Off);
            SetMaterialIntIfPresent(material, SrcBlendId, (int)BlendMode.SrcAlpha);
            SetMaterialIntIfPresent(material, DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            SetMaterialIntIfPresent(material, ZWriteId, 0);
            SetMaterialIntIfPresent(material, CullId, (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetMaterialFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetFloat(propertyId, value);
        }

        private static void SetMaterialIntIfPresent(Material material, int propertyId, int value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetInt(propertyId, value);
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

        private static int ResolveObstacleMask()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
        }

        private static int ResolveLayerMask(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
