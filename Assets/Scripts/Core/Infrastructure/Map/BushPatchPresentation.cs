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

        [SerializeField] private int _minTuftCount = 96;
        [SerializeField] private int _maxTuftCount = 220;
        [SerializeField] private float _tuftDensity = 145f;
        [SerializeField] private Color _darkGrass = new Color(0.05f, 0.36f, 0.10f, 1f);
        [SerializeField] private Color _midGrass = new Color(0.08f, 0.58f, 0.14f, 1f);
        [SerializeField] private Color _lightGrass = new Color(0.20f, 0.78f, 0.18f, 1f);

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
            float footprintArea = Mathf.Max(0.1f, bounds.size.x * bounds.size.z);
            int tuftCount = Mathf.Clamp(
                Mathf.RoundToInt(footprintArea * Mathf.Max(1f, _tuftDensity)),
                Mathf.Max(16, _minTuftCount),
                Mathf.Max(_minTuftCount, _maxTuftCount));

            float baseY = bounds.center.y - bounds.extents.y + 0.018f;
            float halfX = Mathf.Max(0.05f, bounds.extents.x * 0.98f);
            float halfZ = Mathf.Max(0.05f, bounds.extents.z * 0.98f);
            float minHeight = Mathf.Max(0.28f, bounds.size.y * 0.34f);
            float maxHeight = Mathf.Max(minHeight + 0.02f, bounds.size.y * 0.62f);
            AddBaseFill(bounds, baseY, halfX, halfZ);
            AddTriangularTuftField(bounds, baseY, halfX, halfZ, ref seed);
            AddEdgeBand(bounds, baseY, halfX, halfZ, ref seed);

            for (int i = 0; i < tuftCount; i++)
            {
                float x = bounds.center.x + Mathf.Lerp(-halfX, halfX, Next01(ref seed));
                float z = bounds.center.z + Mathf.Lerp(-halfZ, halfZ, Next01(ref seed));
                float angle = Next01(ref seed) * Mathf.PI;
                float height = Mathf.Lerp(minHeight, maxHeight, Next01(ref seed));
                float width = Mathf.Lerp(0.06f, 0.14f, Next01(ref seed));
                float spread = Mathf.Lerp(0.08f, 0.18f, Next01(ref seed));
                int materialIndex = Mathf.Clamp(Mathf.FloorToInt(Next01(ref seed) * _triangles.Length), 0, _triangles.Length - 1);

                AddTuft(
                    new Vector3(x, baseY, z),
                    angle,
                    width,
                    height,
                    spread,
                    materialIndex);
            }

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

        private void AddBaseFill(Bounds bounds, float baseY, float halfX, float halfZ)
        {
            int start = _vertices.Count;
            float y = baseY + 0.006f;
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
            float baseY,
            float halfX,
            float halfZ,
            ref int seed)
        {
            float cell = 0.18f;
            int columns = Mathf.Clamp(Mathf.CeilToInt((halfX * 2f) / cell), 4, 28);
            int rows = Mathf.Clamp(Mathf.CeilToInt((halfZ * 2f) / cell), 4, 28);
            float stepX = (halfX * 2f) / columns;
            float stepZ = (halfZ * 2f) / rows;

            for (int z = 0; z < rows; z++)
            {
                float rowZ = bounds.center.z - halfZ + stepZ * (z + 0.5f);
                float stagger = (z & 1) == 0 ? 0f : stepX * 0.5f;

                for (int x = 0; x < columns; x++)
                {
                    float rowX = bounds.center.x - halfX + stepX * (x + 0.5f) + stagger;
                    if (rowX > bounds.center.x + halfX)
                        rowX -= halfX * 2f;

                    float jitterX = Mathf.Lerp(-stepX * 0.16f, stepX * 0.16f, Next01(ref seed));
                    float jitterZ = Mathf.Lerp(-stepZ * 0.12f, stepZ * 0.12f, Next01(ref seed));
                    int materialIndex = Next01(ref seed) > 0.58f ? 2 : 1;
                    AddTopDownTuft(
                        new Vector3(rowX + jitterX, baseY + 0.014f + z * 0.0006f, rowZ + jitterZ),
                        stepX * Mathf.Lerp(0.82f, 1.12f, Next01(ref seed)),
                        stepZ * Mathf.Lerp(0.92f, 1.22f, Next01(ref seed)),
                        (z & 1) == 0 ? 1f : -1f,
                        materialIndex);
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

        private void AddEdgeBand(
            Bounds bounds,
            float baseY,
            float halfX,
            float halfZ,
            ref int seed)
        {
            int edgeTuftsPerSide = Mathf.Clamp(
                Mathf.RoundToInt((halfX + halfZ) * 10f),
                10,
                34);

            for (int i = 0; i < edgeTuftsPerSide; i++)
            {
                float t = edgeTuftsPerSide <= 1 ? 0.5f : i / (float)(edgeTuftsPerSide - 1);
                AddEdgeTuft(new Vector3(bounds.center.x + Mathf.Lerp(-halfX, halfX, t), baseY, bounds.center.z - halfZ), 0f, ref seed);
                AddEdgeTuft(new Vector3(bounds.center.x + Mathf.Lerp(-halfX, halfX, t), baseY, bounds.center.z + halfZ), Mathf.PI, ref seed);
                AddEdgeTuft(new Vector3(bounds.center.x - halfX, baseY, bounds.center.z + Mathf.Lerp(-halfZ, halfZ, t)), Mathf.PI * 0.5f, ref seed);
                AddEdgeTuft(new Vector3(bounds.center.x + halfX, baseY, bounds.center.z + Mathf.Lerp(-halfZ, halfZ, t)), -Mathf.PI * 0.5f, ref seed);
            }
        }

        private void AddEdgeTuft(Vector3 basePosition, float inwardAngle, ref int seed)
        {
            float angle = inwardAngle + Mathf.Lerp(-0.45f, 0.45f, Next01(ref seed));
            int materialIndex = Mathf.Clamp(Mathf.FloorToInt(Next01(ref seed) * _triangles.Length), 0, _triangles.Length - 1);
            AddTuft(
                basePosition,
                angle,
                Mathf.Lerp(0.08f, 0.15f, Next01(ref seed)),
                Mathf.Lerp(0.34f, 0.58f, Next01(ref seed)),
                Mathf.Lerp(0.12f, 0.22f, Next01(ref seed)),
                materialIndex);
        }

        private void AddTuft(
            Vector3 basePosition,
            float angle,
            float width,
            float height,
            float spread,
            int materialIndex)
        {
            AddGrassCard(basePosition, angle, width, height, spread, materialIndex);
            AddGrassCard(basePosition, angle + Mathf.PI * 0.52f, width * 0.86f, height * 0.92f, spread * 0.84f, materialIndex);
            AddGrassCard(basePosition, angle - Mathf.PI * 0.48f, width * 0.78f, height * 0.82f, spread * 0.74f, materialIndex);
        }

        private void AddGrassCard(
            Vector3 basePosition,
            float angle,
            float width,
            float height,
            float spread,
            int materialIndex)
        {
            int start = _vertices.Count;
            Vector3 forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 right = new Vector3(-forward.z, 0f, forward.x) * (width * 0.5f);
            Vector3 top = basePosition + Vector3.up * height + forward * spread;
            Vector3 mid = basePosition + Vector3.up * (height * 0.58f) + forward * (spread * 0.52f);

            _vertices.Add(basePosition - right);
            _vertices.Add(basePosition + right);
            _vertices.Add(mid - right * 0.82f);
            _vertices.Add(mid + right * 0.82f);
            _vertices.Add(top);

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(1f, 0f));
            _uvs.Add(new Vector2(0f, 0.58f));
            _uvs.Add(new Vector2(1f, 0.58f));
            _uvs.Add(new Vector2(0.5f, 1f));

            List<int> triangles = _triangles[Mathf.Clamp(materialIndex, 0, _triangles.Length - 1)];
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
            triangles.Add(start + 4);
            triangles.Add(start + 3);

            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 3);
            triangles.Add(start + 4);
            triangles.Add(start + 2);
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
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)CullMode.Off);

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
