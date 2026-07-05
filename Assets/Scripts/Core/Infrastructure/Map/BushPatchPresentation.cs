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

        [SerializeField] private int _minBladeCount = 24;
        [SerializeField] private int _maxBladeCount = 56;
        [SerializeField] private float _bladeDensity = 46f;
        [SerializeField] private Color _darkGrass = new Color(0.08f, 0.46f, 0.12f, 1f);
        [SerializeField] private Color _midGrass = new Color(0.15f, 0.68f, 0.16f, 1f);
        [SerializeField] private Color _lightGrass = new Color(0.36f, 0.88f, 0.18f, 1f);

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
            int bladeCount = Mathf.Clamp(
                Mathf.RoundToInt(footprintArea * Mathf.Max(1f, _bladeDensity)),
                Mathf.Max(8, _minBladeCount),
                Mathf.Max(_minBladeCount, _maxBladeCount));

            float baseY = bounds.center.y - bounds.extents.y + 0.018f;
            float halfX = Mathf.Max(0.05f, bounds.extents.x * 0.92f);
            float halfZ = Mathf.Max(0.05f, bounds.extents.z * 0.92f);
            float minHeight = Mathf.Max(0.22f, bounds.size.y * 0.32f);
            float maxHeight = Mathf.Max(minHeight + 0.02f, bounds.size.y * 0.54f);

            for (int i = 0; i < bladeCount; i++)
            {
                float x = bounds.center.x + Mathf.Lerp(-halfX, halfX, Next01(ref seed));
                float z = bounds.center.z + Mathf.Lerp(-halfZ, halfZ, Next01(ref seed));
                float angle = Next01(ref seed) * Mathf.PI;
                float height = Mathf.Lerp(minHeight, maxHeight, Next01(ref seed));
                float width = Mathf.Lerp(0.04f, 0.08f, Next01(ref seed));
                int materialIndex = Mathf.Clamp(Mathf.FloorToInt(Next01(ref seed) * _triangles.Length), 0, _triangles.Length - 1);

                AddBlade(
                    new Vector3(x, baseY, z),
                    angle,
                    width,
                    height,
                    Mathf.Lerp(-0.035f, 0.035f, Next01(ref seed)),
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

        private void AddBlade(
            Vector3 basePosition,
            float angle,
            float width,
            float height,
            float lean,
            int materialIndex)
        {
            int start = _vertices.Count;
            Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (width * 0.5f);
            Vector3 leanOffset = new Vector3(Mathf.Cos(angle + Mathf.PI * 0.5f), 0f, Mathf.Sin(angle + Mathf.PI * 0.5f)) * lean;
            Vector3 top = basePosition + Vector3.up * height + leanOffset;

            _vertices.Add(basePosition - right);
            _vertices.Add(basePosition + right);
            _vertices.Add(top - right * 0.32f);
            _vertices.Add(top + right * 0.32f);

            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(1f, 0f));
            _uvs.Add(new Vector2(0f, 1f));
            _uvs.Add(new Vector2(1f, 1f));

            List<int> triangles = _triangles[Mathf.Clamp(materialIndex, 0, _triangles.Length - 1)];
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 3);
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
