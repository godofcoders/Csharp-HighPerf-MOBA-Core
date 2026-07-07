using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class BushPatchPresentation : MonoBehaviour
    {
        private const string VisualRootName = "GrassPatchVisual";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private float _tuftCellSize = 0.16f;
        [SerializeField] private Color _darkGrass = new Color(0.03f, 0.33f, 0.08f, 0.68f);
        [SerializeField] private Color _midGrass = new Color(0.08f, 0.56f, 0.11f, 0.74f);
        [SerializeField] private Color _lightGrass = new Color(0.24f, 0.78f, 0.15f, 0.78f);

        private readonly List<Vector3> _vertices = new List<Vector3>(512);
        private readonly List<Vector2> _uvs = new List<Vector2>(512);
        private readonly List<int>[] _triangles =
        {
            new List<int>(768),
            new List<int>(768),
            new List<int>(768)
        };

        private Renderer[] _sourceRenderers;
        private bool[] _sourceRendererStates;
        private GameObject _visualRoot;
        private Mesh _mesh;
        private Material[] _materials;

        public static void InstallUnder(GameObject root)
        {
            if (root == null)
                return;

            int bushLayer = LayerMask.NameToLayer("Bushes");
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !IsBushCandidate(collider.gameObject, bushLayer))
                    continue;

                if (collider.GetComponent<BushPatchPresentation>() == null)
                    collider.gameObject.AddComponent<BushPatchPresentation>();
            }
        }

        private static bool IsBushCandidate(GameObject candidate, int bushLayer)
        {
            if (candidate == null)
                return false;

            if (bushLayer >= 0 && candidate.layer == bushLayer)
                return true;

            return candidate.name.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Awake()
        {
            BuildPresentation();
        }

        private void OnDestroy()
        {
            RestoreSourceRenderers();
            DestroyGeneratedObject(_mesh);

            if (_materials == null)
                return;

            for (int i = 0; i < _materials.Length; i++)
                DestroyGeneratedObject(_materials[i]);
        }

        private void BuildPresentation()
        {
            if (_visualRoot != null)
                return;

            HideSourceRenderers();

            _visualRoot = new GameObject(VisualRootName);
            _visualRoot.transform.SetParent(transform, false);
            _visualRoot.transform.localPosition = Vector3.zero;
            _visualRoot.transform.localRotation = Quaternion.identity;
            _visualRoot.transform.localScale = Vector3.one;
            _visualRoot.layer = gameObject.layer;

            MeshFilter meshFilter = _visualRoot.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = _visualRoot.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;

            Bounds bounds = ResolveLocalBounds();
            _mesh = BuildGrassMesh(bounds);
            _mesh.name = $"{name}_GrassPatchMesh";
            meshFilter.sharedMesh = _mesh;

            _materials = CreateGrassMaterials();
            meshRenderer.sharedMaterials = _materials;
        }

        private void HideSourceRenderers()
        {
            _sourceRenderers = GetComponents<Renderer>();
            _sourceRendererStates = new bool[_sourceRenderers.Length];

            for (int i = 0; i < _sourceRenderers.Length; i++)
            {
                Renderer renderer = _sourceRenderers[i];
                if (renderer == null)
                    continue;

                _sourceRendererStates[i] = renderer.enabled;
                renderer.enabled = false;
            }
        }

        private void RestoreSourceRenderers()
        {
            if (_sourceRenderers == null || _sourceRendererStates == null)
                return;

            for (int i = 0; i < _sourceRenderers.Length; i++)
            {
                Renderer renderer = _sourceRenderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = i < _sourceRendererStates.Length && _sourceRendererStates[i];
            }
        }

        private Bounds ResolveLocalBounds()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
                return new Bounds(box.center, box.size);

            Collider collider = GetComponent<Collider>();
            if (collider != null)
                return ConvertWorldBoundsToLocal(collider.bounds);

            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
                return ConvertWorldBoundsToLocal(renderer.bounds);

            return new Bounds(Vector3.zero, Vector3.one);
        }

        private Bounds ConvertWorldBoundsToLocal(Bounds worldBounds)
        {
            Vector3 lossyScale = transform.lossyScale;
            Vector3 size = new Vector3(
                SafeInverseScale(worldBounds.size.x, lossyScale.x),
                SafeInverseScale(worldBounds.size.y, lossyScale.y),
                SafeInverseScale(worldBounds.size.z, lossyScale.z));

            return new Bounds(transform.InverseTransformPoint(worldBounds.center), size);
        }

        private Mesh BuildGrassMesh(Bounds bounds)
        {
            _vertices.Clear();
            _uvs.Clear();
            for (int i = 0; i < _triangles.Length; i++)
                _triangles[i].Clear();

            int seed = CalculateSeed();
            float surfaceY = ResolveGroundVisualY(bounds);
            float halfX = Mathf.Max(0.05f, bounds.extents.x * 0.98f);
            float halfZ = Mathf.Max(0.05f, bounds.extents.z * 0.98f);
            AddBaseFill(bounds, surfaceY, halfX, halfZ);
            AddTriangularTuftField(bounds, surfaceY, halfX, halfZ, ref seed);

            Mesh mesh = new Mesh();
            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = _triangles.Length;
            for (int i = 0; i < _triangles.Length; i++)
                mesh.SetTriangles(_triangles[i], i);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static float ResolveGroundVisualY(Bounds bounds)
        {
            float lowerEdge = bounds.center.y - bounds.extents.y;
            float lift = Mathf.Clamp(bounds.size.y * 0.12f, 0.06f, 0.16f);
            return lowerEdge + lift;
        }

        private void AddBaseFill(Bounds bounds, float surfaceY, float halfX, float halfZ)
        {
            int start = _vertices.Count;
            float y = surfaceY;
            _vertices.Add(new Vector3(bounds.center.x - halfX, y, bounds.center.z - halfZ));
            _vertices.Add(new Vector3(bounds.center.x + halfX, y, bounds.center.z - halfZ));
            _vertices.Add(new Vector3(bounds.center.x - halfX, y, bounds.center.z + halfZ));
            _vertices.Add(new Vector3(bounds.center.x + halfX, y, bounds.center.z + halfZ));

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(1f, 0f));
            _uvs.Add(new Vector2(0f, 1f));
            _uvs.Add(new Vector2(1f, 1f));

            List<int> triangles = _triangles[0];
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void AddTriangularTuftField(
            Bounds bounds,
            float surfaceY,
            float halfX,
            float halfZ,
            ref int seed)
        {
            float cell = Mathf.Max(0.08f, _tuftCellSize);
            int columns = Mathf.Clamp(Mathf.CeilToInt((halfX * 2f) / cell), 4, 28);
            int rows = Mathf.Clamp(Mathf.CeilToInt((halfZ * 2f) / cell), 4, 28);
            float stepX = (halfX * 2f) / columns;
            float stepZ = (halfZ * 2f) / rows;
            float visualMinX = bounds.center.x - halfX;
            float visualMaxX = bounds.center.x + halfX;

            for (int z = 0; z < rows; z++)
            {
                float rowZ = bounds.center.z - halfZ + stepZ * (z + 0.5f);
                float stagger = (z & 1) == 0 ? 0f : stepX * 0.5f;

                for (int x = 0; x < columns; x++)
                {
                    float rowX = bounds.center.x - halfX + stepX * (x + 0.5f) + stagger;
                    if (rowX > bounds.center.x + halfX)
                        rowX -= halfX * 2f;

                    float jitterX = Mathf.Lerp(-stepX * 0.07f, stepX * 0.07f, Next01(ref seed));
                    float jitterZ = Mathf.Lerp(-stepZ * 0.05f, stepZ * 0.05f, Next01(ref seed));
                    Vector3 center = new Vector3(
                        Mathf.Clamp(rowX + jitterX, visualMinX + stepX * 0.22f, visualMaxX - stepX * 0.22f),
                        surfaceY + 0.008f + z * 0.0007f,
                        rowZ + jitterZ);
                    float width = stepX * Mathf.Lerp(0.92f, 1.18f, Next01(ref seed));
                    float height = stepZ * Mathf.Lerp(1.08f, 1.36f, Next01(ref seed));
                    float direction = (z & 1) == 0 ? 1f : -1f;

                    AddTopDownTuft(center, width, height, direction, 1);
                    AddTopDownTuft(
                        center + new Vector3(0f, 0.001f, -height * 0.18f * direction),
                        width * 0.62f,
                        height * 0.72f,
                        direction,
                        2);
                }
            }
        }

        private void AddTopDownTuft(
            Vector3 center,
            float width,
            float height,
            float direction,
            int materialIndex)
        {
            int start = _vertices.Count;
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            _vertices.Add(new Vector3(center.x - halfWidth, center.y, center.z - halfHeight * direction));
            _vertices.Add(new Vector3(center.x + halfWidth, center.y, center.z - halfHeight * direction));
            _vertices.Add(new Vector3(center.x, center.y, center.z + halfHeight * direction));

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(1f, 0f));
            _uvs.Add(new Vector2(0.5f, 1f));

            List<int> triangles = _triangles[Mathf.Clamp(materialIndex, 0, _triangles.Length - 1)];
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
        }

        private Material[] CreateGrassMaterials()
        {
            return new[]
            {
                CreateGrassMaterial("Grass_Dark", _darkGrass),
                CreateGrassMaterial("Grass_Mid", _midGrass),
                CreateGrassMaterial("Grass_Light", _lightGrass)
            };
        }

        private static Material CreateGrassMaterial(string materialName, Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.name = materialName;
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

            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)CullMode.Off);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            WriteMaterialColor(material, color);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.18f);

            return material;
        }

        private int CalculateSeed()
        {
            unchecked
            {
                int hash = 17;
                string objectName = name;
                for (int i = 0; i < objectName.Length; i++)
                    hash = hash * 31 + objectName[i];

                Vector3 position = transform.position;
                hash = hash * 31 + Mathf.RoundToInt(position.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(position.z * 100f);
                return hash;
            }
        }

        private static float Next01(ref int seed)
        {
            unchecked
            {
                seed = seed * 1103515245 + 12345;
                return ((seed >> 8) & 0x00FFFFFF) / 16777215f;
            }
        }

        private static float SafeInverseScale(float value, float scale)
        {
            float absScale = Mathf.Abs(scale);
            return absScale > 0.0001f ? value / absScale : value;
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

        private static void DestroyGeneratedObject(UnityEngine.Object target)
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
