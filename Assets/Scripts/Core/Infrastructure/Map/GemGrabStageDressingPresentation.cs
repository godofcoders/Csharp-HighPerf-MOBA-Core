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
        [SerializeField, Min(0.1f)] private float _mineCrystalHeight = 0.95f;

        [Header("Train")]
        [SerializeField, Min(0.1f)] private float _trackInset = 1.8f;
        [SerializeField, Min(0.1f)] private float _trackLaneOffsetRatio = 0.38f;
        [SerializeField, Min(0.1f)] private float _trainSpeed = 6.4f;
        [SerializeField, Min(0f)] private float _trainDamage = 850f;
        [SerializeField, Min(0.1f)] private float _trainHitHalfLength = 1.55f;
        [SerializeField, Min(0.1f)] private float _trainHitHalfWidth = 0.72f;
        [SerializeField, Min(0.1f)] private float _trainDamageCooldownSeconds = 1.1f;

        private Transform _stageRoot;
        private Material _sandMaterial;
        private Material _darkSandMaterial;
        private Material _rockMaterial;
        private Material _woodMaterial;
        private Material _railMaterial;
        private Material _trainMaterial;
        private Material _trainTrimMaterial;
        private Material _crystalMaterial;
        private Material _gemGlowMaterial;

        public static void InstallUnder(GameObject root)
        {
            if (root == null || SceneSelection.SelectedMode != GameModeId.GemGrab)
                return;

            GemGrabStageDressingPresentation presentation =
                root.GetComponent<GemGrabStageDressingPresentation>();
            if (presentation == null)
                presentation = root.AddComponent<GemGrabStageDressingPresentation>();

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
            DestroyMaterial(_sandMaterial);
            DestroyMaterial(_darkSandMaterial);
            DestroyMaterial(_rockMaterial);
            DestroyMaterial(_woodMaterial);
            DestroyMaterial(_railMaterial);
            DestroyMaterial(_trainMaterial);
            DestroyMaterial(_trainTrimMaterial);
            DestroyMaterial(_crystalMaterial);
            DestroyMaterial(_gemGlowMaterial);
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
            BuildTrainLane(bounds, mineCenter, groundY);
            BuildAmbientProps(bounds, mineCenter, groundY);
        }

        private void BuildGemMine(Vector3 center, float groundY)
        {
            CreatePrimitive(
                "GemMine_SandstoneBase",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.055f, 0f),
                new Vector3(_mineRadius, 0.055f, _mineRadius),
                Quaternion.identity,
                _rockMaterial,
                false);

            CreatePrimitive(
                "GemMine_DarkShaft",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.12f, 0f),
                new Vector3(_mineRadius * 0.46f, 0.035f, _mineRadius * 0.46f),
                Quaternion.identity,
                _darkSandMaterial,
                false);

            CreatePrimitive(
                "GemMine_GlowDisc",
                PrimitiveType.Cylinder,
                center + new Vector3(0f, 0.145f, 0f),
                new Vector3(_mineRadius * 0.72f, 0.012f, _mineRadius * 0.72f),
                Quaternion.identity,
                _gemGlowMaterial,
                false);

            CreateCrystal("GemMine_CrystalCore", center + new Vector3(0f, _mineCrystalHeight * 0.55f, 0f), new Vector3(0.56f, _mineCrystalHeight, 0.56f), 45f);
            CreateCrystal("GemMine_CrystalLeft", center + new Vector3(-0.58f, _mineCrystalHeight * 0.42f, 0.18f), new Vector3(0.34f, _mineCrystalHeight * 0.72f, 0.34f), 18f);
            CreateCrystal("GemMine_CrystalRight", center + new Vector3(0.52f, _mineCrystalHeight * 0.38f, -0.24f), new Vector3(0.30f, _mineCrystalHeight * 0.62f, 0.30f), -28f);

            CreateBeam("GemMine_TopBeam_A", center + new Vector3(0f, 0.38f, -1.15f), new Vector3(2.25f, 0.16f, 0.18f));
            CreateBeam("GemMine_TopBeam_B", center + new Vector3(0f, 0.38f, 1.15f), new Vector3(2.25f, 0.16f, 0.18f));
            CreateBeam("GemMine_SideBeam_L", center + new Vector3(-1.15f, 0.34f, 0f), new Vector3(0.18f, 0.16f, 2.05f));
            CreateBeam("GemMine_SideBeam_R", center + new Vector3(1.15f, 0.34f, 0f), new Vector3(0.18f, 0.16f, 2.05f));

            CreatePrimitive(
                "GemMine_MiniCart",
                PrimitiveType.Cube,
                center + new Vector3(-2.1f, 0.22f, -1.8f),
                new Vector3(0.82f, 0.34f, 0.55f),
                Quaternion.Euler(0f, 18f, 0f),
                _woodMaterial,
                false);

            CreatePrimitive(
                "GemMine_CartGem",
                PrimitiveType.Cube,
                center + new Vector3(-2.1f, 0.58f, -1.8f),
                new Vector3(0.32f, 0.32f, 0.32f),
                Quaternion.Euler(20f, 45f, 12f),
                _crystalMaterial,
                false);

            CreatePrimitive(
                "GemMine_SpawnRing",
                PrimitiveType.Cylinder,
                new Vector3(center.x, groundY + 0.17f, center.z),
                new Vector3(0.42f, 0.012f, 0.42f),
                Quaternion.identity,
                _crystalMaterial,
                false);
        }

        private void BuildTrainLane(Bounds bounds, Vector3 mineCenter, float groundY)
        {
            float minX = bounds.min.x + _trackInset;
            float maxX = bounds.max.x - _trackInset;
            if (maxX - minX < 6f)
                return;

            float targetZ = mineCenter.z + bounds.extents.z * _trackLaneOffsetRatio;
            float lanePadding = Mathf.Max(3.5f, _mineRadius + 0.9f);
            float minZ = bounds.min.z + lanePadding;
            float maxZ = bounds.max.z - lanePadding;
            float trackZ = Mathf.Clamp(targetZ, minZ, maxZ);
            if (Mathf.Abs(trackZ - mineCenter.z) < _mineRadius + 1.4f)
                trackZ = Mathf.Clamp(mineCenter.z - bounds.extents.z * 0.42f, minZ, maxZ);

            float trackLength = maxX - minX;
            float centerX = (minX + maxX) * 0.5f;
            float railY = groundY + 0.065f;
            float railGap = 0.62f;

            CreatePrimitive(
                "GemTrain_Rail_North",
                PrimitiveType.Cube,
                new Vector3(centerX, railY, trackZ + railGap),
                new Vector3(trackLength, 0.08f, 0.08f),
                Quaternion.identity,
                _railMaterial,
                false);

            CreatePrimitive(
                "GemTrain_Rail_South",
                PrimitiveType.Cube,
                new Vector3(centerX, railY, trackZ - railGap),
                new Vector3(trackLength, 0.08f, 0.08f),
                Quaternion.identity,
                _railMaterial,
                false);

            int sleeperCount = Mathf.Clamp(Mathf.RoundToInt(trackLength / 1.15f), 8, 28);
            float step = sleeperCount > 1 ? trackLength / (sleeperCount - 1) : trackLength;
            for (int i = 0; i < sleeperCount; i++)
            {
                float x = minX + step * i;
                CreatePrimitive(
                    "GemTrain_Sleeper_" + i,
                    PrimitiveType.Cube,
                    new Vector3(x, groundY + 0.04f, trackZ),
                    new Vector3(0.18f, 0.06f, 1.72f),
                    Quaternion.identity,
                    _woodMaterial,
                    false);
            }

            GameObject trainRoot = new GameObject("GemGrab_TrainHazard");
            trainRoot.transform.SetParent(_stageRoot, true);
            trainRoot.transform.position = new Vector3(minX, groundY + 0.46f, trackZ);
            trainRoot.transform.rotation = Quaternion.identity;
            trainRoot.transform.localScale = Vector3.one;

            BuildTrainVisual(trainRoot.transform);

            GemGrabTrainHazard hazard = trainRoot.AddComponent<GemGrabTrainHazard>();
            hazard.Configure(
                new Vector3(minX, groundY + 0.46f, trackZ),
                new Vector3(maxX, groundY + 0.46f, trackZ),
                _trainSpeed,
                _trainDamage,
                _trainHitHalfLength,
                _trainHitHalfWidth,
                _trainDamageCooldownSeconds);
        }

        private void BuildTrainVisual(Transform trainRoot)
        {
            CreatePrimitiveLocal(
                "Train_Body",
                PrimitiveType.Cube,
                trainRoot,
                new Vector3(0f, 0f, 0f),
                new Vector3(1.04f, 0.64f, 2.25f),
                Quaternion.identity,
                _trainMaterial);

            CreatePrimitiveLocal(
                "Train_Cabin",
                PrimitiveType.Cube,
                trainRoot,
                new Vector3(0f, 0.52f, -0.52f),
                new Vector3(0.9f, 0.72f, 0.82f),
                Quaternion.identity,
                _trainTrimMaterial);

            CreatePrimitiveLocal(
                "Train_Nose",
                PrimitiveType.Cube,
                trainRoot,
                new Vector3(0f, -0.02f, 1.28f),
                new Vector3(0.82f, 0.46f, 0.42f),
                Quaternion.identity,
                _trainTrimMaterial);

            CreatePrimitiveLocal(
                "Train_SmokeStack",
                PrimitiveType.Cylinder,
                trainRoot,
                new Vector3(0f, 0.52f, 0.58f),
                new Vector3(0.22f, 0.26f, 0.22f),
                Quaternion.identity,
                _railMaterial);

            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.62f : 0.62f;
                float z = i < 2 ? -0.65f : 0.72f;
                CreatePrimitiveLocal(
                    "Train_Wheel_" + i,
                    PrimitiveType.Cylinder,
                    trainRoot,
                    new Vector3(x, -0.31f, z),
                    new Vector3(0.28f, 0.08f, 0.28f),
                    Quaternion.Euler(0f, 0f, 90f),
                    _railMaterial);
            }
        }

        private void BuildAmbientProps(Bounds bounds, Vector3 mineCenter, float groundY)
        {
            CreateRockPile("GemRockPile_A", new Vector3(bounds.min.x + 2.3f, groundY, mineCenter.z - 4.6f));
            CreateRockPile("GemRockPile_B", new Vector3(bounds.max.x - 2.5f, groundY, mineCenter.z + 3.8f));
            CreateRockPile("GemRockPile_C", new Vector3(mineCenter.x + 4.3f, groundY, bounds.min.z + 2.5f));

            CreatePrimitive(
                "GemMine_BrokenCart_A",
                PrimitiveType.Cube,
                new Vector3(bounds.min.x + 4.4f, groundY + 0.22f, bounds.max.z - 3.0f),
                new Vector3(1.0f, 0.36f, 0.58f),
                Quaternion.Euler(0f, -28f, 0f),
                _woodMaterial,
                false);

            CreatePrimitive(
                "GemMine_BrokenCart_B",
                PrimitiveType.Cube,
                new Vector3(bounds.max.x - 4.4f, groundY + 0.22f, bounds.min.z + 3.0f),
                new Vector3(1.0f, 0.36f, 0.58f),
                Quaternion.Euler(0f, 28f, 0f),
                _woodMaterial,
                false);
        }

        private void CreateRockPile(string prefix, Vector3 basePosition)
        {
            CreatePrimitive(prefix + "_0", PrimitiveType.Sphere, basePosition + new Vector3(0f, 0.18f, 0f), new Vector3(0.62f, 0.36f, 0.5f), Quaternion.identity, _rockMaterial, false);
            CreatePrimitive(prefix + "_1", PrimitiveType.Sphere, basePosition + new Vector3(0.42f, 0.14f, -0.16f), new Vector3(0.42f, 0.28f, 0.38f), Quaternion.identity, _darkSandMaterial, false);
            CreatePrimitive(prefix + "_2", PrimitiveType.Sphere, basePosition + new Vector3(-0.38f, 0.13f, 0.18f), new Vector3(0.38f, 0.26f, 0.34f), Quaternion.identity, _rockMaterial, false);
        }

        private void CreateCrystal(string objectName, Vector3 position, Vector3 scale, float yaw)
        {
            CreatePrimitive(
                objectName,
                PrimitiveType.Cube,
                position,
                scale,
                Quaternion.Euler(12f, yaw, 8f),
                _crystalMaterial,
                false);
        }

        private void CreateBeam(string objectName, Vector3 position, Vector3 scale)
        {
            CreatePrimitive(
                objectName,
                PrimitiveType.Cube,
                position,
                scale,
                Quaternion.identity,
                _woodMaterial,
                false);
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

        private GameObject CreatePrimitiveLocal(
            string objectName,
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;
            ApplyRendererMaterial(primitive, material);
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
            _sandMaterial = EnsureMaterial(_sandMaterial, "Runtime_GemGrab_Sand", new Color(0.76f, 0.59f, 0.36f, 1f), false);
            _darkSandMaterial = EnsureMaterial(_darkSandMaterial, "Runtime_GemGrab_DarkSand", new Color(0.31f, 0.22f, 0.15f, 1f), false);
            _rockMaterial = EnsureMaterial(_rockMaterial, "Runtime_GemGrab_Rock", new Color(0.66f, 0.48f, 0.30f, 1f), false);
            _woodMaterial = EnsureMaterial(_woodMaterial, "Runtime_GemGrab_Wood", new Color(0.46f, 0.26f, 0.12f, 1f), false);
            _railMaterial = EnsureMaterial(_railMaterial, "Runtime_GemGrab_Rail", new Color(0.16f, 0.14f, 0.13f, 1f), false);
            _trainMaterial = EnsureMaterial(_trainMaterial, "Runtime_GemGrab_TrainBody", new Color(0.72f, 0.18f, 0.13f, 1f), false);
            _trainTrimMaterial = EnsureMaterial(_trainTrimMaterial, "Runtime_GemGrab_TrainTrim", new Color(0.95f, 0.64f, 0.18f, 1f), false);
            _crystalMaterial = EnsureMaterial(_crystalMaterial, "Runtime_GemGrab_Crystal", new Color(0.95f, 0.08f, 0.92f, 1f), false);
            _gemGlowMaterial = EnsureMaterial(_gemGlowMaterial, "Runtime_GemGrab_Glow", new Color(0.95f, 0.08f, 0.92f, 0.34f), true);
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
