using System.Collections.Generic;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class GemPickupFeedbackView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh _sparkMesh;

        [Header("Pool")]
        [Min(4)]
        [SerializeField] private int _initialPoolSize = 12;
        [Min(8)]
        [SerializeField] private int _maxPoolSize = 36;

        [Header("Timing")]
        [Min(0.05f)]
        [SerializeField] private float _durationSeconds = 0.42f;

        [Header("Ring")]
        [SerializeField] private float _ringStartRadius = 0.20f;
        [SerializeField] private float _ringEndRadius = 0.76f;
        [SerializeField] private float _ringHeight = 0.018f;
        [SerializeField] private Color _ringColor = new Color(1f, 0.66f, 0.08f, 0.92f);

        [Header("Spark")]
        [SerializeField] private float _sparkStartHeight = 0.22f;
        [SerializeField] private float _sparkEndHeight = 1.05f;
        [SerializeField] private float _sparkStartScale = 0.34f;
        [SerializeField] private float _sparkEndScale = 0.10f;
        [SerializeField] private Color _sparkColor = new Color(1f, 0.88f, 0.30f, 0.98f);

        private readonly List<BurstInstance> _pool = new List<BurstInstance>(36);
        private MaterialPropertyBlock _propertyBlock;
        private Material _material;

        private void Awake()
        {
            EnsurePropertyBlock();
            _material = CreateMaterial();
            PrewarmPool();
        }

        private void OnEnable()
        {
            GemEventBus.OnGemPickedUpAt += HandleGemPickedUpAt;
        }

        private void OnDisable()
        {
            GemEventBus.OnGemPickedUpAt -= HandleGemPickedUpAt;
        }

        private void OnDestroy()
        {
            if (_material == null)
                return;

            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _pool.Count; i++)
            {
                BurstInstance burst = _pool[i];
                if (burst == null || !burst.IsActive)
                    continue;

                burst.ElapsedSeconds += deltaTime;
                float duration = Mathf.Max(0.001f, burst.DurationSeconds);
                float t = Mathf.Clamp01(burst.ElapsedSeconds / duration);

                if (t >= 1f)
                {
                    burst.SetActive(false);
                    continue;
                }

                ApplyBurstVisual(burst, t);
            }
        }

        private void HandleGemPickedUpAt(Vector3 position, int amount)
        {
            SpawnBurst(position, Mathf.Max(1, amount));
        }

        private void PrewarmPool()
        {
            int count = Mathf.Clamp(_initialPoolSize, 0, Mathf.Max(0, _maxPoolSize));
            for (int i = 0; i < count; i++)
            {
                _pool.Add(CreateBurstInstance());
            }
        }

        private void SpawnBurst(Vector3 position, int amount)
        {
            BurstInstance burst = GetBurstInstance();
            if (burst == null)
                return;

            burst.Root.position = position;
            burst.Root.rotation = Quaternion.identity;
            burst.AmountScale = 1f + Mathf.Clamp(amount - 1, 0, 5) * 0.08f;
            burst.ElapsedSeconds = 0f;
            burst.DurationSeconds = Mathf.Max(0.05f, _durationSeconds);
            burst.SetActive(true);
            ApplyBurstVisual(burst, 0f);
        }

        private BurstInstance GetBurstInstance()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                BurstInstance burst = _pool[i];
                if (burst != null && !burst.IsActive)
                    return burst;
            }

            if (_pool.Count >= Mathf.Max(1, _maxPoolSize))
                return null;

            BurstInstance created = CreateBurstInstance();
            _pool.Add(created);
            return created;
        }

        private BurstInstance CreateBurstInstance()
        {
            GameObject root = new GameObject("GemPickupBurst");
            root.transform.SetParent(transform, false);

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(root.transform, false);

            Collider ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null)
                Destroy(ringCollider);

            Renderer ringRenderer = ring.GetComponent<Renderer>();
            ConfigureRenderer(ringRenderer);

            GameObject spark = new GameObject(
                "Spark",
                typeof(MeshFilter),
                typeof(MeshRenderer));
            spark.transform.SetParent(root.transform, false);
            spark.GetComponent<MeshFilter>().sharedMesh = GetSparkMesh();

            Renderer sparkRenderer = spark.GetComponent<Renderer>();
            ConfigureRenderer(sparkRenderer);

            BurstInstance burst = new BurstInstance(
                root,
                ring.transform,
                ringRenderer,
                spark.transform,
                sparkRenderer);

            burst.SetActive(false);
            return burst;
        }

        private void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            if (_material != null)
                renderer.sharedMaterial = _material;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void ApplyBurstVisual(BurstInstance burst, float normalizedTime)
        {
            if (burst == null)
                return;

            float t = Mathf.Clamp01(normalizedTime);
            float ease = EaseOutCubic(t);
            float fade = 1f - t;

            float ringRadius = Mathf.Lerp(
                _ringStartRadius,
                _ringEndRadius,
                ease) * burst.AmountScale;

            burst.Ring.localPosition = Vector3.up * 0.04f;
            burst.Ring.localScale = new Vector3(
                ringRadius,
                Mathf.Max(0.001f, _ringHeight),
                ringRadius);

            burst.Spark.localPosition = Vector3.up * Mathf.Lerp(
                _sparkStartHeight,
                _sparkEndHeight,
                ease);
            burst.Spark.localRotation = Quaternion.Euler(
                0f,
                45f + ease * 220f,
                0f);

            float sparkScale = Mathf.Lerp(
                _sparkStartScale,
                _sparkEndScale,
                ease) * burst.AmountScale;
            burst.Spark.localScale = Vector3.one * Mathf.Max(0.01f, sparkScale);

            Color ringColor = _ringColor;
            ringColor.a *= fade;
            Color sparkColor = _sparkColor;
            sparkColor.a *= fade;

            ApplyRendererColor(burst.RingRenderer, ringColor);
            ApplyRendererColor(burst.SparkRenderer, sparkColor);
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            EnsurePropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static Mesh GetSparkMesh()
        {
            if (_sparkMesh != null)
                return _sparkMesh;

            _sparkMesh = new Mesh
            {
                name = "GemPickupSpark"
            };

            Vector3 top = new Vector3(0f, 0.58f, 0f);
            Vector3 east = new Vector3(0.42f, 0f, 0f);
            Vector3 north = new Vector3(0f, 0f, 0.42f);
            Vector3 west = new Vector3(-0.42f, 0f, 0f);
            Vector3 south = new Vector3(0f, 0f, -0.42f);
            Vector3 bottom = new Vector3(0f, -0.58f, 0f);

            _sparkMesh.vertices = new[]
            {
                top, north, east,
                top, west, north,
                top, south, west,
                top, east, south,
                bottom, east, north,
                bottom, north, west,
                bottom, west, south,
                bottom, south, east
            };

            _sparkMesh.triangles = new[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8,
                9, 10, 11,
                12, 13, 14,
                15, 16, 17,
                18, 19, 20,
                21, 22, 23
            };

            _sparkMesh.RecalculateNormals();
            _sparkMesh.RecalculateBounds();
            return _sparkMesh;
        }

        private static Material CreateMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.color = Color.white;
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private sealed class BurstInstance
        {
            public readonly Transform Root;
            public readonly Transform Ring;
            public readonly Renderer RingRenderer;
            public readonly Transform Spark;
            public readonly Renderer SparkRenderer;

            public bool IsActive;
            public float ElapsedSeconds;
            public float DurationSeconds;
            public float AmountScale;

            public BurstInstance(
                GameObject root,
                Transform ring,
                Renderer ringRenderer,
                Transform spark,
                Renderer sparkRenderer)
            {
                Root = root.transform;
                Ring = ring;
                RingRenderer = ringRenderer;
                Spark = spark;
                SparkRenderer = sparkRenderer;
            }

            public void SetActive(bool active)
            {
                IsActive = active;

                if (Root != null && Root.gameObject.activeSelf != active)
                    Root.gameObject.SetActive(active);
            }
        }
    }
}
