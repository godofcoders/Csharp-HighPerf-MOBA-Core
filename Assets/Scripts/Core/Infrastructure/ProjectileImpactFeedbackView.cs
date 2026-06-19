using System.Collections.Generic;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class ProjectileImpactFeedbackView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Pool")]
        [Min(4)]
        [SerializeField] private int _initialPoolSize = 18;

        [Min(8)]
        [SerializeField] private int _maxPoolSize = 48;

        [Header("Timing")]
        [Min(0.05f)]
        [SerializeField] private float _impactDurationSeconds = 0.16f;

        [Min(0.05f)]
        [SerializeField] private float _expiredDurationSeconds = 0.12f;

        [Header("Scale")]
        [SerializeField] private float _verticalScale = 0.025f;
        [SerializeField] private float _startScaleMultiplier = 0.16f;
        [SerializeField] private float _endScaleMultiplier = 0.42f;

        [Header("Spark")]
        [SerializeField] private bool _spawnImpactSpark = true;
        [Min(0.05f)]
        [SerializeField] private float _sparkDurationSeconds = 0.12f;
        [SerializeField] private float _sparkLength = 0.34f;
        [SerializeField] private float _sparkWidth = 0.045f;
        [SerializeField] private float _sparkHeightOffset = 0.14f;

        [Header("Colors")]
        [SerializeField] private Color _impactColor = new Color(1f, 0.56f, 0.10f, 0.90f);
        [SerializeField] private Color _superImpactColor = new Color(1f, 0.30f, 0.95f, 0.90f);
        [SerializeField] private Color _hyperImpactColor = new Color(0.72f, 0.20f, 1f, 0.96f);
        [SerializeField] private Color _expiredColor = new Color(0.42f, 0.46f, 0.50f, 0.20f);

        private readonly List<PulseInstance> _pool = new List<PulseInstance>(48);
        private readonly List<SparkInstance> _sparkPool = new List<SparkInstance>(48);
        private MaterialPropertyBlock _propertyBlock;
        private Material _pulseMaterial;

        private void Awake()
        {
            EnsurePropertyBlock();
            _pulseMaterial = CreatePulseMaterial();
            PrewarmPool();
        }

        private void OnEnable()
        {
            CombatPresentationEventBus.OnEvent += HandleCombatPresentationEvent;
        }

        private void OnDisable()
        {
            CombatPresentationEventBus.OnEvent -= HandleCombatPresentationEvent;
        }

        private void OnDestroy()
        {
            if (_pulseMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_pulseMaterial);
            else
                DestroyImmediate(_pulseMaterial);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < _pool.Count; i++)
            {
                PulseInstance pulse = _pool[i];
                if (pulse == null || !pulse.IsActive)
                    continue;

                pulse.ElapsedSeconds += deltaTime;
                float duration = Mathf.Max(0.001f, pulse.DurationSeconds);
                float t = Mathf.Clamp01(pulse.ElapsedSeconds / duration);

                if (t >= 1f)
                {
                    pulse.SetActive(false);
                    continue;
                }

                ApplyPulseVisual(pulse, t);
            }

            for (int i = 0; i < _sparkPool.Count; i++)
            {
                SparkInstance spark = _sparkPool[i];
                if (spark == null || !spark.IsActive)
                    continue;

                spark.ElapsedSeconds += deltaTime;
                float duration = Mathf.Max(0.001f, spark.DurationSeconds);
                float t = Mathf.Clamp01(spark.ElapsedSeconds / duration);

                if (t >= 1f)
                {
                    spark.SetActive(false);
                    continue;
                }

                ApplySparkVisual(spark, t);
            }
        }

        private void HandleCombatPresentationEvent(CombatPresentationEvent evt)
        {
            switch (evt.EventType)
            {
                case CombatPresentationEventType.ProjectileImpacted:
                    float impactScale = ResolveImpactScale(evt.IsSuper, evt.IsHypercharged);
                    Color impactColor = ResolveImpactColor(evt.IsSuper, evt.IsHypercharged);
                    float durationScale = Mathf.Lerp(1f, impactScale, 0.24f);
                    SpawnPulse(
                        evt.Position,
                        ResolveRadius(evt.Value) * impactScale,
                        impactColor,
                        _impactDurationSeconds * durationScale);
                    SpawnSpark(
                        evt.Position,
                        evt.Direction,
                        impactColor,
                        _sparkDurationSeconds * durationScale,
                        Mathf.Lerp(1f, impactScale, 0.72f));
                    break;

                case CombatPresentationEventType.ProjectileExpired:
                    SpawnPulse(evt.Position, ResolveRadius(evt.Value) * 0.72f, _expiredColor, _expiredDurationSeconds);
                    break;
            }
        }

        private void PrewarmPool()
        {
            int count = Mathf.Clamp(_initialPoolSize, 0, Mathf.Max(0, _maxPoolSize));
            for (int i = 0; i < count; i++)
            {
                _pool.Add(CreatePulseInstance());
                _sparkPool.Add(CreateSparkInstance());
            }
        }

        private void SpawnPulse(Vector3 position, float radius, Color color, float durationSeconds)
        {
            PulseInstance pulse = GetPulseInstance();
            if (pulse == null)
                return;

            position.y += 0.08f;

            pulse.Transform.position = position;
            pulse.Transform.rotation = Quaternion.identity;
            pulse.Radius = Mathf.Max(0.05f, radius);
            pulse.Color = color;
            pulse.DurationSeconds = Mathf.Max(0.05f, durationSeconds);
            pulse.ElapsedSeconds = 0f;
            pulse.SetActive(true);
            ApplyPulseVisual(pulse, 0f);
        }

        private void SpawnSpark(
            Vector3 position,
            Vector3 direction,
            Color color,
            float durationSeconds,
            float scaleMultiplier = 1f)
        {
            if (!_spawnImpactSpark)
                return;

            SparkInstance spark = GetSparkInstance();
            if (spark == null)
                return;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector3.forward;
            else
                direction.Normalize();

            position.y += Mathf.Max(0f, _sparkHeightOffset);

            spark.Transform.position = position;
            spark.Transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            spark.Length = Mathf.Max(0.04f, _sparkLength * scaleMultiplier);
            spark.Width = Mathf.Max(0.01f, _sparkWidth * Mathf.Lerp(1f, scaleMultiplier, 0.75f));
            spark.Color = color;
            spark.DurationSeconds = Mathf.Max(0.05f, durationSeconds);
            spark.ElapsedSeconds = 0f;
            spark.SetActive(true);
            ApplySparkVisual(spark, 0f);
        }

        private PulseInstance GetPulseInstance()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                PulseInstance pulse = _pool[i];
                if (pulse != null && !pulse.IsActive)
                    return pulse;
            }

            if (_pool.Count >= Mathf.Max(1, _maxPoolSize))
                return null;

            PulseInstance created = CreatePulseInstance();
            _pool.Add(created);
            return created;
        }

        private SparkInstance GetSparkInstance()
        {
            for (int i = 0; i < _sparkPool.Count; i++)
            {
                SparkInstance spark = _sparkPool[i];
                if (spark != null && !spark.IsActive)
                    return spark;
            }

            if (_sparkPool.Count >= Mathf.Max(1, _maxPoolSize))
                return null;

            SparkInstance created = CreateSparkInstance();
            _sparkPool.Add(created);
            return created;
        }

        private PulseInstance CreatePulseInstance()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "ProjectileImpactPulse";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.zero;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_pulseMaterial != null)
                    renderer.sharedMaterial = _pulseMaterial;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            PulseInstance pulse = new PulseInstance(go, renderer);
            pulse.SetActive(false);
            return pulse;
        }

        private SparkInstance CreateSparkInstance()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ProjectileImpactSpark";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.zero;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_pulseMaterial != null)
                    renderer.sharedMaterial = _pulseMaterial;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            SparkInstance spark = new SparkInstance(go, renderer);
            spark.SetActive(false);
            return spark;
        }

        private Color ResolveImpactColor(bool isSuper, bool isHypercharged)
        {
            if (isHypercharged)
                return _hyperImpactColor;

            return isSuper ? _superImpactColor : _impactColor;
        }

        private static float ResolveImpactScale(bool isSuper, bool isHypercharged)
        {
            if (isHypercharged)
                return 1.55f;

            return isSuper ? 1.18f : 1f;
        }

        private static float ResolveRadius(float eventValue)
        {
            return Mathf.Clamp(eventValue > 0f ? eventValue : 0.45f, 0.15f, 1.6f);
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static Material CreatePulseMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.color = new Color(1f, 0.42f, 0.06f, 0.86f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void ApplyPulseVisual(PulseInstance pulse, float normalizedTime)
        {
            if (pulse == null)
                return;

            float t = Mathf.Clamp01(normalizedTime);
            float scale = Mathf.Lerp(
                pulse.Radius * _startScaleMultiplier,
                pulse.Radius * _endScaleMultiplier,
                EaseOutCubic(t));

            pulse.Transform.localScale = new Vector3(scale, _verticalScale, scale);

            if (pulse.Renderer == null)
                return;

            Color color = pulse.Color;
            color.a *= 1f - t;
            ApplyRendererColor(pulse.Renderer, color);
        }

        private void ApplySparkVisual(SparkInstance spark, float normalizedTime)
        {
            if (spark == null)
                return;

            float t = Mathf.Clamp01(normalizedTime);
            float ease = EaseOutCubic(t);
            float length = Mathf.Lerp(spark.Length, spark.Length * 0.38f, ease);
            float width = Mathf.Lerp(spark.Width, spark.Width * 0.18f, ease);

            spark.Transform.localScale = new Vector3(width, width, length);

            if (spark.Renderer == null)
                return;

            Color color = spark.Color;
            color.a *= 1f - t;
            ApplyRendererColor(spark.Renderer, color);
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

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private sealed class PulseInstance
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly Renderer Renderer;

            public bool IsActive;
            public float ElapsedSeconds;
            public float DurationSeconds;
            public float Radius;
            public Color Color;

            public PulseInstance(GameObject gameObject, Renderer renderer)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Renderer = renderer;
            }

            public void SetActive(bool active)
            {
                IsActive = active;

                if (GameObject != null && GameObject.activeSelf != active)
                    GameObject.SetActive(active);
            }
        }

        private sealed class SparkInstance
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly Renderer Renderer;

            public bool IsActive;
            public float ElapsedSeconds;
            public float DurationSeconds;
            public float Length;
            public float Width;
            public Color Color;

            public SparkInstance(GameObject gameObject, Renderer renderer)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Renderer = renderer;
            }

            public void SetActive(bool active)
            {
                IsActive = active;

                if (GameObject != null && GameObject.activeSelf != active)
                    GameObject.SetActive(active);
            }
        }
    }
}
