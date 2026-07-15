using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class MapArenaBoundaryPresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private const string BoundaryRootName = "RuntimeArenaBoundary";

        [SerializeField, Min(0f)] private float _boundsPadding = 0.35f;
        [SerializeField, Min(0.05f)] private float _wallThickness = 0.48f;
        [SerializeField, Min(0.1f)] private float _wallHeight = 1.1f;
        [SerializeField, Min(1f)] private float _brawlBallGoalOpeningWidth = 6.6f;
        [SerializeField] private Color _wallColor = new Color(0.71f, 0.52f, 0.32f, 0.48f);

        private Transform _boundaryRoot;
        private Material _boundaryMaterial;
        private bool _hasBuiltBoundary;

        public static void InstallUnder(GameObject root)
        {
            if (root == null)
                return;

            MapArenaBoundaryPresentation presentation =
                root.GetComponent<MapArenaBoundaryPresentation>();
            bool created = false;
            if (presentation == null)
            {
                presentation = root.AddComponent<MapArenaBoundaryPresentation>();
                created = true;
            }

            if (!created || !presentation._hasBuiltBoundary)
                presentation.RefreshBoundary();
        }

        private void Awake()
        {
            RefreshBoundary();
        }

        private void OnDestroy()
        {
            if (_boundaryMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_boundaryMaterial);
            else
                DestroyImmediate(_boundaryMaterial);
        }

        public void RefreshBoundary()
        {
            if (!TryResolveGroundBounds(out Bounds bounds))
                return;

            EnsureBoundaryRoot();
            ClearBoundaryRoot();

            float padding = Mathf.Max(0f, _boundsPadding);
            float thickness = Mathf.Max(0.05f, _wallThickness);
            float height = Mathf.Max(0.1f, _wallHeight);

            float minX = bounds.min.x - padding;
            float maxX = bounds.max.x + padding;
            float minZ = bounds.min.z - padding;
            float maxZ = bounds.max.z + padding;
            float centerX = bounds.center.x;
            float centerZ = (minZ + maxZ) * 0.5f;
            float centerY = bounds.max.y + height * 0.5f;

            float zLength = Mathf.Max(thickness, maxZ - minZ + thickness * 2f);
            float xLength = Mathf.Max(thickness, maxX - minX);

            CreateBoundaryWall(
                "ArenaWall_Left",
                new Vector3(minX - thickness * 0.5f, centerY, centerZ),
                new Vector3(thickness, height, zLength));
            CreateBoundaryWall(
                "ArenaWall_Right",
                new Vector3(maxX + thickness * 0.5f, centerY, centerZ),
                new Vector3(thickness, height, zLength));

            bool useGoalOpenings = SceneSelection.SelectedMode == GameModeId.BrawlBall;
            CreateEndBoundary("ArenaWall_Bottom", minZ - thickness * 0.5f, minX, maxX, centerX, centerY, xLength, thickness, height, useGoalOpenings);
            CreateEndBoundary("ArenaWall_Top", maxZ + thickness * 0.5f, minX, maxX, centerX, centerY, xLength, thickness, height, useGoalOpenings);
            _hasBuiltBoundary = true;
        }

        private void CreateEndBoundary(
            string namePrefix,
            float z,
            float minX,
            float maxX,
            float centerX,
            float centerY,
            float xLength,
            float thickness,
            float height,
            bool useGoalOpening)
        {
            if (!useGoalOpening)
            {
                CreateBoundaryWall(
                    namePrefix,
                    new Vector3(centerX, centerY, z),
                    new Vector3(xLength, height, thickness));
                return;
            }

            float openingWidth = Mathf.Clamp(
                Mathf.Max(1f, _brawlBallGoalOpeningWidth),
                1f,
                Mathf.Max(1f, maxX - minX - thickness));
            float openingMinX = centerX - openingWidth * 0.5f;
            float openingMaxX = centerX + openingWidth * 0.5f;

            CreateEndSegment(namePrefix + "_Left", minX, openingMinX, z, centerY, thickness, height);
            CreateEndSegment(namePrefix + "_Right", openingMaxX, maxX, z, centerY, thickness, height);
        }

        private void CreateEndSegment(
            string name,
            float startX,
            float endX,
            float z,
            float centerY,
            float thickness,
            float height)
        {
            float width = endX - startX;
            if (width <= 0.05f)
                return;

            CreateBoundaryWall(
                name,
                new Vector3((startX + endX) * 0.5f, centerY, z),
                new Vector3(width, height, thickness));
        }

        private void CreateBoundaryWall(string objectName, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = objectName;
            wall.layer = ResolveObstacleLayer();
            wall.transform.SetParent(_boundaryRoot, true);
            wall.transform.position = position;
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = scale;

            MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = EnsureBoundaryMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Collider collider = wall.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = false;
        }

        private void EnsureBoundaryRoot()
        {
            if (_boundaryRoot != null)
                return;

            Transform existing = transform.Find(BoundaryRootName);
            if (existing != null)
            {
                _boundaryRoot = existing;
                return;
            }

            GameObject root = new GameObject(BoundaryRootName);
            root.layer = ResolveObstacleLayer();
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            _boundaryRoot = root.transform;
        }

        private void ClearBoundaryRoot()
        {
            if (_boundaryRoot == null)
                return;

            for (int i = _boundaryRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _boundaryRoot.GetChild(i);
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

            if (_boundaryRoot != null && candidate.transform.IsChildOf(_boundaryRoot))
                return false;

            if (candidate.name.IndexOf(BoundaryRootName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

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

        private Material EnsureBoundaryMaterial()
        {
            if (_boundaryMaterial != null)
                return _boundaryMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            _boundaryMaterial = new Material(shader)
            {
                name = "RuntimeArenaBoundary_Mat",
                color = _wallColor,
                enableInstancing = true
            };

            if (_boundaryMaterial.HasProperty(BaseColorId))
                _boundaryMaterial.SetColor(BaseColorId, _wallColor);
            if (_boundaryMaterial.HasProperty(ColorId))
                _boundaryMaterial.SetColor(ColorId, _wallColor);

            _boundaryMaterial.SetOverrideTag("RenderType", "Transparent");
            SetMaterialFloatIfPresent(_boundaryMaterial, SurfaceId, 1f);
            SetMaterialFloatIfPresent(_boundaryMaterial, BlendId, 0f);
            SetMaterialFloatIfPresent(_boundaryMaterial, AlphaClipId, 0f);
            _boundaryMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _boundaryMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _boundaryMaterial.SetInt("_ZWrite", 0);
            _boundaryMaterial.DisableKeyword("_ALPHATEST_ON");
            _boundaryMaterial.EnableKeyword("_ALPHABLEND_ON");
            _boundaryMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _boundaryMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _boundaryMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _boundaryMaterial;
        }

        private static void SetMaterialFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetFloat(propertyId, value);
        }

        private static int ResolveObstacleLayer()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? obstacleLayer : 0;
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
