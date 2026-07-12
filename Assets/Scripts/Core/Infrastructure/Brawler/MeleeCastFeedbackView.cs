using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class MeleeCastFeedbackView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Pool")]
        [Min(4)]
        [SerializeField] private int _initialPoolSize = 16;
        [Min(8)]
        [SerializeField] private int _maxPoolSize = 48;
        [Min(4)]
        [SerializeField] private int _maxScheduledCasts = 24;

        [Header("Punch Combo")]
        [Range(1, 6)]
        [SerializeField] private int _punchesPerCast = 4;
        [Min(0.01f)]
        [SerializeField] private float _punchIntervalSeconds = 0.040f;
        [Min(0.04f)]
        [SerializeField] private float _punchDurationSeconds = 0.130f;

        [Header("Placement")]
        [SerializeField] private float _heightOffset = 0.42f;
        [SerializeField] private float _startForwardOffset = 0.22f;
        [SerializeField] private float _endRangeRatio = 0.92f;
        [SerializeField] private float _sideOffset = 0.34f;

        [Header("Scale")]
        [SerializeField] private Vector3 _startScale = new Vector3(0.48f, 0.36f, 0.58f);
        [SerializeField] private Vector3 _endScale = new Vector3(0.72f, 0.46f, 0.86f);

        [Header("Color")]
        [SerializeField] private Color _fistColor = new Color(1f, 1f, 1f, 0.96f);
        [SerializeField] private Color _trailColor = new Color(1f, 1f, 1f, 0.88f);

        private readonly List<PunchInstance> _pool = new List<PunchInstance>(48);
        private readonly List<ScheduledMeleeCast> _scheduledCasts = new List<ScheduledMeleeCast>(24);
        private MaterialPropertyBlock _propertyBlock;
        private Material _fistMaterial;

        private void Awake()
        {
            EnsurePropertyBlock();
            _fistMaterial = CreateTransparentMaterial(_fistColor);
            PrewarmPool();
        }

        private void OnEnable()
        {
            BrawlerPresentationEventBus.OnEvent += HandleBrawlerPresentationEvent;
        }

        private void OnDisable()
        {
            BrawlerPresentationEventBus.OnEvent -= HandleBrawlerPresentationEvent;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_fistMaterial);
        }

        private void Update()
        {
            float now = Time.time;
            float deltaTime = Time.deltaTime;

            for (int i = _scheduledCasts.Count - 1; i >= 0; i--)
            {
                ScheduledMeleeCast cast = _scheduledCasts[i];
                if (cast.Source == null || cast.NextPunchIndex >= cast.PunchCount)
                {
                    _scheduledCasts.RemoveAt(i);
                    continue;
                }

                int safety = 0;
                while (cast.NextPunchIndex < cast.PunchCount &&
                       now + 0.0001f >= cast.NextPunchTime &&
                       safety < 4)
                {
                    SpawnPunch(cast, cast.NextPunchIndex);
                    cast.NextPunchIndex++;
                    cast.NextPunchTime += Mathf.Max(0.001f, cast.PunchIntervalSeconds);
                    safety++;
                }

                if (cast.NextPunchIndex >= cast.PunchCount)
                    _scheduledCasts.RemoveAt(i);
                else
                    _scheduledCasts[i] = cast;
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                PunchInstance punch = _pool[i];
                if (punch == null || !punch.IsActive)
                    continue;

                punch.ElapsedSeconds += deltaTime;
                float t = Mathf.Clamp01(punch.ElapsedSeconds / Mathf.Max(0.001f, punch.DurationSeconds));
                if (t >= 1f)
                {
                    punch.SetActive(false);
                    continue;
                }

                ApplyPunchVisual(punch, t);
            }
        }

        private void HandleBrawlerPresentationEvent(BrawlerPresentationEvent evt)
        {
            if (evt.EventType != BrawlerPresentationEventType.MainAttackStarted ||
                evt.Source == null ||
                evt.AbilityDefinition is not MeleeConeAbilityDefinition melee)
            {
                return;
            }

            Vector3 direction = evt.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = evt.Source.transform.forward;

            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;

            if (_scheduledCasts.Count >= Mathf.Max(1, _maxScheduledCasts))
                _scheduledCasts.RemoveAt(0);

            _scheduledCasts.Add(new ScheduledMeleeCast
            {
                Source = evt.Source,
                Direction = direction,
                Range = Mathf.Max(0.4f, melee.Range),
                PunchCount = Mathf.Clamp(_punchesPerCast, 1, 6),
                NextPunchIndex = 0,
                NextPunchTime = Time.time,
                PunchIntervalSeconds = Mathf.Max(0.001f, _punchIntervalSeconds)
            });
        }

        private void PrewarmPool()
        {
            int count = Mathf.Clamp(_initialPoolSize, 0, Mathf.Max(0, _maxPoolSize));
            for (int i = 0; i < count; i++)
                _pool.Add(CreatePunchInstance());
        }

        private void SpawnPunch(ScheduledMeleeCast cast, int punchIndex)
        {
            PunchInstance punch = GetPunchInstance();
            if (punch == null || cast.Source == null)
                return;

            Vector3 direction = cast.Direction.sqrMagnitude > 0.001f
                ? cast.Direction.normalized
                : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            side = side.sqrMagnitude > 0.001f ? side.normalized : Vector3.right;

            float sideSign = punchIndex % 2 == 0 ? -1f : 1f;
            float comboT = cast.PunchCount <= 1
                ? 0f
                : punchIndex / Mathf.Max(1f, cast.PunchCount - 1f);
            float range = Mathf.Max(0.4f, cast.Range);

            Vector3 origin = cast.Source.GetCastPosition() + Vector3.up * Mathf.Max(0f, _heightOffset);
            punch.StartPosition = origin +
                                  direction * Mathf.Max(0f, _startForwardOffset) +
                                  side * (sideSign * Mathf.Lerp(_sideOffset * 0.80f, _sideOffset, comboT));
            punch.EndPosition = cast.Source.Position +
                                Vector3.up * Mathf.Max(0f, _heightOffset) +
                                direction * (range * Mathf.Clamp01(_endRangeRatio)) +
                                side * (sideSign * Mathf.Lerp(_sideOffset, _sideOffset * 0.28f, comboT));
            punch.Direction = direction;
            punch.DurationSeconds = Mathf.Max(0.04f, _punchDurationSeconds);
            punch.ElapsedSeconds = 0f;
            punch.Color = _fistColor;
            punch.SetActive(true);
            ApplyPunchVisual(punch, 0f);
        }

        private PunchInstance GetPunchInstance()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                PunchInstance punch = _pool[i];
                if (punch != null && !punch.IsActive)
                    return punch;
            }

            if (_pool.Count >= Mathf.Max(1, _maxPoolSize))
                return null;

            PunchInstance created = CreatePunchInstance();
            _pool.Add(created);
            return created;
        }

        private PunchInstance CreatePunchInstance()
        {
            GameObject go = new GameObject("MeleeFistFlash");
            go.name = "MeleeFistFlash";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.zero;

            Renderer[] renderers = new Renderer[7];
            renderers[0] = CreateVisualPart(
                go.transform,
                "Palm",
                PrimitiveType.Sphere,
                new Vector3(0f, 0f, -0.05f),
                new Vector3(0.70f, 0.48f, 0.54f));
            renderers[1] = CreateVisualPart(
                go.transform,
                "CenterKnuckle",
                PrimitiveType.Sphere,
                new Vector3(0f, 0.015f, 0.25f),
                new Vector3(0.34f, 0.27f, 0.30f));
            renderers[2] = CreateVisualPart(
                go.transform,
                "LeftKnuckle",
                PrimitiveType.Sphere,
                new Vector3(-0.17f, 0f, 0.20f),
                new Vector3(0.30f, 0.24f, 0.28f));
            renderers[3] = CreateVisualPart(
                go.transform,
                "RightKnuckle",
                PrimitiveType.Sphere,
                new Vector3(0.17f, 0f, 0.20f),
                new Vector3(0.30f, 0.24f, 0.28f));
            renderers[4] = CreateVisualPart(
                go.transform,
                "PunchSmear",
                PrimitiveType.Cube,
                new Vector3(0f, 0f, -0.44f),
                new Vector3(0.24f, 0.18f, 0.78f));
            renderers[5] = CreateVisualPart(
                go.transform,
                "LeftSpeedLine",
                PrimitiveType.Cube,
                new Vector3(-0.22f, 0f, -0.34f),
                new Vector3(0.055f, 0.080f, 0.58f));
            renderers[6] = CreateVisualPart(
                go.transform,
                "RightSpeedLine",
                PrimitiveType.Cube,
                new Vector3(0.22f, 0f, -0.34f),
                new Vector3(0.055f, 0.080f, 0.58f));

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.115f;
            trail.startWidth = 0.34f;
            trail.endWidth = 0.040f;
            trail.startColor = _trailColor;
            trail.endColor = WithAlpha(_trailColor, 0f);
            trail.material = _fistMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.emitting = false;

            PunchInstance instance = new PunchInstance(go, renderers, trail);
            instance.SetActive(false);
            return instance;
        }

        private Renderer CreateVisualPart(
            Transform parent,
            string partName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = _fistMaterial;
            }

            return renderer;
        }

        private void ApplyPunchVisual(PunchInstance punch, float t)
        {
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2f);
            punch.Transform.position = Vector3.Lerp(punch.StartPosition, punch.EndPosition, eased);
            punch.Transform.rotation = Quaternion.LookRotation(punch.Direction, Vector3.up);
            punch.Transform.localScale = Vector3.Lerp(_startScale, _endScale, Mathf.Sin(t * Mathf.PI));

            Color color = punch.Color;
            float visibility = 1f - Mathf.Clamp01(t * t);
            color.a *= visibility;
            ApplyRendererColor(punch.Renderers, color);

            if (punch.Trail != null)
            {
                Color trailStart = _trailColor;
                trailStart.a *= Mathf.Lerp(1f, 0.22f, t);
                Color trailEnd = trailStart;
                trailEnd.a = 0f;
                punch.Trail.startColor = trailStart;
                punch.Trail.endColor = trailEnd;
            }
        }

        private void ApplyRendererColor(Renderer[] renderers, Color color)
        {
            if (renderers == null)
                return;

            EnsurePropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ColorId, color);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(EmissionColorId, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.color = color;
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private struct ScheduledMeleeCast
        {
            public BrawlerController Source;
            public Vector3 Direction;
            public float Range;
            public int PunchCount;
            public int NextPunchIndex;
            public float NextPunchTime;
            public float PunchIntervalSeconds;
        }

        private sealed class PunchInstance
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly Renderer[] Renderers;
            public readonly TrailRenderer Trail;
            public Vector3 StartPosition;
            public Vector3 EndPosition;
            public Vector3 Direction;
            public Color Color;
            public float DurationSeconds;
            public float ElapsedSeconds;
            public bool IsActive;

            public PunchInstance(GameObject gameObject, Renderer[] renderers, TrailRenderer trail)
            {
                GameObject = gameObject;
                Transform = gameObject != null ? gameObject.transform : null;
                Renderers = renderers;
                Trail = trail;
            }

            public void SetActive(bool active)
            {
                IsActive = active;

                if (Trail != null)
                {
                    Trail.emitting = active;
                    if (!active)
                        Trail.Clear();
                }

                if (GameObject != null && GameObject.activeSelf != active)
                    GameObject.SetActive(active);
            }
        }
    }
}
