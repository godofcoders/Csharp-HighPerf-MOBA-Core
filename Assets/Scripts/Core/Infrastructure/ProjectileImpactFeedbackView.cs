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
        [SerializeField] private float _impactDurationSeconds = 0.22f;

        [Min(0.05f)]
        [SerializeField] private float _expiredDurationSeconds = 0.14f;

        [Header("Scale")]
        [SerializeField] private float _verticalScale = 0.035f;
        [SerializeField] private float _startScaleMultiplier = 0.22f;
        [SerializeField] private float _endScaleMultiplier = 0.72f;

        [Header("Colors")]
        [SerializeField] private Color _impactColor = new Color(1f, 0.42f, 0.06f, 0.86f);
        [SerializeField] private Color _superImpactColor = new Color(1f, 0.30f, 0.95f, 0.88f);
        [SerializeField] private Color _expiredColor = new Color(0.42f, 0.46f, 0.50f, 0.24f);

        private readonly List<PulseInstance> _pool = new List<PulseInstance>(48);
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
        }

        private void HandleCombatPresentationEvent(CombatPresentationEvent evt)
        {
            switch (evt.EventType)
            {
                case CombatPresentationEventType.ProjectileImpacted:
                    SpawnPulse(evt.Position, ResolveRadius(evt.Value), ResolveImpactColor(evt.IsSuper), _impactDurationSeconds);
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

        private Color ResolveImpactColor(bool isSuper)
        {
            return isSuper ? _superImpactColor : _impactColor;
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
            EnsurePropertyBlock();
            pulse.Renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            pulse.Renderer.SetPropertyBlock(_propertyBlock);
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
    }
}
