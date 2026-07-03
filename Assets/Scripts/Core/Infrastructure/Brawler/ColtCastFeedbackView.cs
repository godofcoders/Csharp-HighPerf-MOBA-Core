using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class ColtCastFeedbackView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        [Header("Pool")]
        [Min(4)]
        [SerializeField] private int _initialPoolSize = 24;
        [Min(8)]
        [SerializeField] private int _maxPoolSize = 96;
        [Min(4)]
        [SerializeField] private int _maxScheduledBursts = 32;

        [Header("Texture Resource")]
        [SerializeField] private string _muzzleTextureResource = "VFX/Particles/flare_01";

        [Header("Placement")]
        [SerializeField] private float _forwardOffset = 0.28f;
        [SerializeField] private float _heightOffset = 0.20f;

        [Header("Timing")]
        [SerializeField] private float _mainDurationSeconds = 0.050f;
        [SerializeField] private float _superDurationSeconds = 0.070f;
        [SerializeField] private float _hyperDurationSeconds = 0.080f;

        [Header("Scale")]
        [SerializeField] private float _mainStartScale = 0.20f;
        [SerializeField] private float _mainEndScale = 0.34f;
        [SerializeField] private float _superStartScale = 0.34f;
        [SerializeField] private float _superEndScale = 0.54f;
        [SerializeField] private float _hyperStartScale = 0.40f;
        [SerializeField] private float _hyperEndScale = 0.66f;

        [Header("Colors")]
        [SerializeField] private Color _mainColor = new Color(1f, 0.74f, 0.14f, 0.92f);
        [SerializeField] private Color _superColor = new Color(1f, 0.30f, 0.02f, 0.96f);
        [SerializeField] private Color _hyperColor = new Color(0.18f, 0.92f, 1f, 0.98f);

        private readonly List<FlashInstance> _flashPool = new List<FlashInstance>(96);
        private readonly List<ScheduledBurst> _scheduledBursts = new List<ScheduledBurst>(32);
        private MaterialPropertyBlock _propertyBlock;
        private Material _muzzleMaterial;
        private Camera _cachedCamera;

        private void Awake()
        {
            EnsurePropertyBlock();
            _muzzleMaterial = CreateMuzzleMaterial();
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
            DestroyMaterial(_muzzleMaterial);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            float now = Time.time;

            for (int i = 0; i < _flashPool.Count; i++)
            {
                FlashInstance flash = _flashPool[i];
                if (flash == null || !flash.IsActive)
                    continue;

                flash.ElapsedSeconds += deltaTime;
                float duration = Mathf.Max(0.001f, flash.DurationSeconds);
                float t = Mathf.Clamp01(flash.ElapsedSeconds / duration);

                if (t >= 1f)
                {
                    flash.SetActive(false);
                    continue;
                }

                ApplyFlashVisual(flash, t);
            }

            for (int i = _scheduledBursts.Count - 1; i >= 0; i--)
            {
                ScheduledBurst burst = _scheduledBursts[i];
                if (burst.Source == null || burst.NextShotIndex >= burst.ShotCount)
                {
                    _scheduledBursts.RemoveAt(i);
                    continue;
                }

                int safety = 0;
                while (burst.NextShotIndex < burst.ShotCount &&
                       now + 0.0001f >= burst.NextShotTime &&
                       safety < 4)
                {
                    SpawnFlash(burst, burst.NextShotIndex);
                    burst.NextShotIndex++;
                    burst.NextShotTime += Mathf.Max(0.001f, burst.DelaySeconds);
                    safety++;
                }

                if (burst.NextShotIndex >= burst.ShotCount)
                    _scheduledBursts.RemoveAt(i);
                else
                    _scheduledBursts[i] = burst;
            }
        }

        private void HandleBrawlerPresentationEvent(BrawlerPresentationEvent evt)
        {
            if (evt.EventType != BrawlerPresentationEventType.MainAttackStarted &&
                evt.EventType != BrawlerPresentationEventType.SuperStarted)
            {
                return;
            }

            if (evt.Source == null || evt.AbilityDefinition == null || !IsColt(evt.Source, evt.AbilityDefinition))
                return;

            if (!TryResolveBurst(evt.AbilityDefinition, out int shotCount, out float delaySeconds, out bool alternateMuzzles))
                return;

            if (_scheduledBursts.Count >= Mathf.Max(1, _maxScheduledBursts))
                _scheduledBursts.RemoveAt(0);

            Vector3 direction = evt.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = evt.Source.transform.forward;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;

            bool isSuper = evt.EventType == BrawlerPresentationEventType.SuperStarted;
            _scheduledBursts.Add(new ScheduledBurst
            {
                Source = evt.Source,
                Direction = direction,
                ShotCount = Mathf.Clamp(shotCount, 1, 24),
                NextShotIndex = 0,
                DelaySeconds = Mathf.Max(0f, delaySeconds),
                NextShotTime = Time.time,
                AlternateMuzzles = alternateMuzzles,
                IsSuper = isSuper,
                IsHypercharged = IsHyperchargedColtSuper(evt.Source, evt.AbilityDefinition, isSuper)
            });
        }

        private void PrewarmPool()
        {
            int count = Mathf.Clamp(_initialPoolSize, 0, Mathf.Max(0, _maxPoolSize));
            for (int i = 0; i < count; i++)
                _flashPool.Add(CreateFlashInstance());
        }

        private void SpawnFlash(ScheduledBurst burst, int shotIndex)
        {
            if (_muzzleMaterial == null)
                return;

            FlashInstance flash = GetFlashInstance();
            if (flash == null || burst.Source == null)
                return;

            Vector3 position = ResolveMuzzlePosition(burst, shotIndex);
            flash.Transform.position = position;
            flash.Transform.rotation = ResolveBillboardRotation(position) *
                                       Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            ResolveFlashStyle(
                burst.IsSuper,
                burst.IsHypercharged,
                out flash.Color,
                out flash.StartScale,
                out flash.EndScale,
                out flash.DurationSeconds);

            flash.ElapsedSeconds = 0f;
            flash.SetActive(true);
            ApplyFlashVisual(flash, 0f);
        }

        private Vector3 ResolveMuzzlePosition(ScheduledBurst burst, int shotIndex)
        {
            BrawlerController source = burst.Source;
            Vector3 position;

            if (source == null)
            {
                position = Vector3.zero;
            }
            else if (!burst.AlternateMuzzles)
            {
                position = source.GetCastPosition();
            }
            else
            {
                position = shotIndex % 2 == 0
                    ? source.GetPrimaryFirePosition()
                    : source.GetSecondaryFirePosition();
            }

            return position + burst.Direction * Mathf.Max(0f, _forwardOffset) +
                   Vector3.up * Mathf.Max(0f, _heightOffset);
        }

        private FlashInstance GetFlashInstance()
        {
            for (int i = 0; i < _flashPool.Count; i++)
            {
                FlashInstance flash = _flashPool[i];
                if (flash != null && !flash.IsActive)
                    return flash;
            }

            if (_flashPool.Count >= Mathf.Max(1, _maxPoolSize))
                return null;

            FlashInstance created = CreateFlashInstance();
            _flashPool.Add(created);
            return created;
        }

        private FlashInstance CreateFlashInstance()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ColtMuzzleFlash";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.zero;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_muzzleMaterial != null)
                    renderer.sharedMaterial = _muzzleMaterial;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            FlashInstance flash = new FlashInstance(go, renderer);
            flash.SetActive(false);
            return flash;
        }

        private void ResolveFlashStyle(
            bool isSuper,
            bool isHypercharged,
            out Color color,
            out float startScale,
            out float endScale,
            out float durationSeconds)
        {
            if (isHypercharged)
            {
                color = _hyperColor;
                startScale = _hyperStartScale;
                endScale = _hyperEndScale;
                durationSeconds = _hyperDurationSeconds;
                return;
            }

            if (isSuper)
            {
                color = _superColor;
                startScale = _superStartScale;
                endScale = _superEndScale;
                durationSeconds = _superDurationSeconds;
                return;
            }

            color = _mainColor;
            startScale = _mainStartScale;
            endScale = _mainEndScale;
            durationSeconds = _mainDurationSeconds;
        }

        private static bool TryResolveBurst(
            AbilityDefinition ability,
            out int shotCount,
            out float delaySeconds,
            out bool alternateMuzzles)
        {
            if (ability is BurstSequenceProjectileAbilityDefinition burst)
            {
                shotCount = Mathf.Max(1, burst.ProjectileCount);
                delaySeconds = Mathf.Max(0f, burst.DelayBetweenShots);
                alternateMuzzles = burst.AlternateMuzzles;
                return true;
            }

            if (ability is ProjectileAbilityDefinition projectile)
            {
                shotCount = Mathf.Max(1, projectile.ProjectileCount);
                delaySeconds = Mathf.Max(0f, projectile.DelayBetweenProjectiles);
                alternateMuzzles = false;
                return true;
            }

            shotCount = 0;
            delaySeconds = 0f;
            alternateMuzzles = false;
            return false;
        }

        private static bool IsColt(BrawlerController source, AbilityDefinition ability)
        {
            string brawlerName = source != null && source.Definition != null
                ? source.Definition.BrawlerName
                : string.Empty;

            if (ContainsToken(brawlerName, "colt"))
                return true;

            return ContainsToken(ability != null ? ability.name : string.Empty, "colt") ||
                   ContainsToken(ability != null ? ability.AbilityName : string.Empty, "colt");
        }

        private static bool IsHyperchargedColtSuper(
            BrawlerController source,
            AbilityDefinition ability,
            bool isSuper)
        {
            if (!isSuper)
                return false;

            if (ContainsToken(ability != null ? ability.name : string.Empty, "hypercharge") ||
                ContainsToken(ability != null ? ability.AbilityName : string.Empty, "hyper"))
            {
                return true;
            }

            return source != null &&
                   source.State != null &&
                   source.State.Hypercharge != null &&
                   source.State.Hypercharge.IsActive;
        }

        private static bool ContainsToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Material CreateMuzzleMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Texture2D texture = string.IsNullOrWhiteSpace(_muzzleTextureResource)
                ? null
                : Resources.Load<Texture2D>(_muzzleTextureResource);
            if (texture == null)
                return null;

            Material material = new Material(shader)
            {
                name = "Colt Muzzle Flash",
                enableInstancing = true
            };

            material.mainTexture = texture;
            if (material.HasProperty(MainTexId))
                material.SetTexture(MainTexId, texture);
            if (material.HasProperty(BaseMapId))
                material.SetTexture(BaseMapId, texture);

            material.color = Color.white;
            material.SetOverrideTag("RenderType", "Transparent");
            SetMaterialFloatIfPresent(material, SurfaceId, 1f);
            SetMaterialFloatIfPresent(material, BlendId, 0f);
            SetMaterialFloatIfPresent(material, AlphaClipId, 0f);
            SetMaterialFloatIfPresent(material, CullId, (float)CullMode.Off);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static void SetMaterialFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetFloat(propertyId, value);
        }

        private Quaternion ResolveBillboardRotation(Vector3 position)
        {
            if (_cachedCamera == null)
                _cachedCamera = Camera.main;

            if (_cachedCamera == null)
                return Quaternion.identity;

            Vector3 toCamera = _cachedCamera.transform.position - position;
            if (toCamera.sqrMagnitude <= 0.001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private void ApplyFlashVisual(FlashInstance flash, float normalizedTime)
        {
            if (flash == null)
                return;

            float t = Mathf.Clamp01(normalizedTime);
            float ease = EaseOutCubic(t);
            float scale = Mathf.Lerp(flash.StartScale, flash.EndScale, ease);
            flash.Transform.localScale = new Vector3(scale, scale, 1f);

            if (flash.Renderer == null)
                return;

            Color color = flash.Color;
            color.a *= 1f - t;
            ApplyRendererColor(flash.Renderer, color);
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

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
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

        private struct ScheduledBurst
        {
            public BrawlerController Source;
            public Vector3 Direction;
            public int ShotCount;
            public int NextShotIndex;
            public float DelaySeconds;
            public float NextShotTime;
            public bool AlternateMuzzles;
            public bool IsSuper;
            public bool IsHypercharged;
        }

        private sealed class FlashInstance
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly Renderer Renderer;

            public bool IsActive;
            public float ElapsedSeconds;
            public float DurationSeconds;
            public float StartScale;
            public float EndScale;
            public Color Color;

            public FlashInstance(GameObject gameObject, Renderer renderer)
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
