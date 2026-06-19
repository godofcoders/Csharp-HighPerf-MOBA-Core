using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine.Rendering;

namespace MOBA.Core.Simulation
{
    public class AreaHazardController : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const int BubbleCount = 5;

        private static Material _runtimeHazardMaterial;

        private AreaHazardDefinition _definition;
        private BrawlerController _owner;
        private TeamType _team;
        private AbilityDefinition _sourceAbility;
        private AbilitySlotType _slotType;
        private bool _isSuper;
        private AreaHazardService _hazardService;

        private float _elapsedLifetime;
        private float _tickTimer;

        private GameObject _visualInstance;
        private readonly List<ISpatialEntity> _targets = new List<ISpatialEntity>(16);
        private readonly Transform[] _bubbleTransforms = new Transform[BubbleCount];
        private readonly Renderer[] _bubbleRenderers = new Renderer[BubbleCount];
        private MaterialPropertyBlock _propertyBlock;
        private Transform _runtimeVisualRoot;
        private Transform _puddleCore;
        private Transform _puddleEdge;
        private Renderer _puddleCoreRenderer;
        private Renderer _puddleEdgeRenderer;
        private Color _puddleCoreColor;
        private Color _puddleEdgeColor;
        private Color _puddleBubbleColor;

        public BrawlerController Owner => _owner;
        public TeamType Team => _team;
        public Vector3 Position => transform.position;
        public float Radius => _definition != null ? _definition.Radius : 0f;
        public float DamagePerTick => _definition != null ? _definition.DamagePerTick : 0f;
        public bool IsSuper => _isSuper;

        public void Initialize(in AreaHazardSpawnRequest request, AreaHazardService hazardService = null)
        {
            _definition = request.Definition;
            _owner = request.Owner;
            _team = request.Team;
            _sourceAbility = request.SourceAbility;
            _slotType = request.SlotType;
            _isSuper = request.IsSuper;
            _hazardService = hazardService;

            transform.position = request.Position;

            BuildVisual();
        }

        private void OnDestroy()
        {
            _hazardService?.Unregister(this);
        }

        public bool CanThreatenTeam(TeamType observerTeam)
        {
            if (_definition == null || _definition.DamagePerTick <= 0f)
                return false;

            switch (_definition.TargetTeamRule)
            {
                case AbilityTargetTeamRule.Enemy:
                    return TeamRelationshipUtility.AreEnemies(_team, observerTeam);

                case AbilityTargetTeamRule.Ally:
                    return TeamRelationshipUtility.AreAllies(_team, observerTeam);

                case AbilityTargetTeamRule.Any:
                    return true;

                default:
                    return false;
            }
        }

        private void Update()
        {
            if (_definition == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsedLifetime += Time.deltaTime;
            _tickTimer += Time.deltaTime;

            if (_tickTimer >= _definition.TickIntervalSeconds)
            {
                _tickTimer -= _definition.TickIntervalSeconds;
                ApplyTick();
            }

            if (_elapsedLifetime >= _definition.DurationSeconds)
            {
                Destroy(gameObject);
            }

            TickVisuals();
        }

        private void ApplyTick()
        {
            if (SimulationClock.Grid == null || _owner == null)
                return;

            _targets.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(transform.position, _definition.Radius, _targets);

            var damageService = ServiceProvider.Get<IDamageService>();
            if (damageService == null)
                return;

            float sqrRadius = _definition.Radius * _definition.Radius;

            for (int i = 0; i < _targets.Count; i++)
            {
                ISpatialEntity target = _targets[i];
                if (!SpatialEntityUtility.IsAlive(target))
                    continue;

                BrawlerController targetBrawler = target as BrawlerController;
                BreakableObjectController targetBreakable = target as BreakableObjectController;
                if (targetBrawler == null && targetBreakable == null)
                    continue;

                if (targetBrawler != null && (targetBrawler.State == null || targetBrawler.State.IsDead))
                    continue;

                if (targetBreakable != null && targetBreakable.IsDestroyed)
                    continue;

                Vector3 targetPosition = target.Position;
                Vector3 delta = targetPosition - transform.position;
                delta.y = 0f;

                if (delta.sqrMagnitude > sqrRadius)
                    continue;

                if (!IsValidTarget(target))
                    continue;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = _owner,
                    Target = target,
                    Damage = _definition.DamagePerTick,
                    Type = DamageType.AoE,
                    HitPosition = targetPosition,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward,
                    SourceAbility = _sourceAbility,
                    IsSuper = _isSuper
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = _owner,
                    Target = targetBrawler,
                    AbilityDefinition = _sourceAbility,
                    SlotType = _slotType,
                    Position = targetPosition,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward,
                    Value = _definition.DamagePerTick,
                    IsSuper = _isSuper
                });
            }
        }

        private bool IsValidTarget(ISpatialEntity target)
        {
            switch (_definition.TargetTeamRule)
            {
                case AbilityTargetTeamRule.Enemy:
                    return target.Team == TeamType.Neutral ||
                           TeamRelationshipUtility.AreEnemies(_team, target.Team);

                case AbilityTargetTeamRule.Ally:
                    return TeamRelationshipUtility.AreAllies(_team, target.Team);

                case AbilityTargetTeamRule.Any:
                    return true;

                default:
                    return false;
            }
        }

        private void BuildVisual()
        {
            float diameter = _definition.Radius * 2f;
            bool runtimeOnly = ShouldUseRuntimeOnlyHazardVisual();
            ResolveHazardPalette(
                out _puddleCoreColor,
                out _puddleEdgeColor,
                out _puddleBubbleColor);

            if (_definition.VisualPrefab != null && !runtimeOnly)
            {
                _visualInstance = Instantiate(_definition.VisualPrefab, transform);

                // Keep it centered on the hazard origin
                _visualInstance.transform.localPosition = Vector3.up * 0.02f;

                Vector3 authoredScale = _visualInstance.transform.localScale;

                // Hazards should communicate their true gameplay radius. Keep
                // authored height, but never let a tiny prefab under-represent
                // the damaging footprint.
                float targetDiameter = diameter * 0.96f;
                _visualInstance.transform.localScale = new Vector3(
                    Mathf.Max(Mathf.Abs(authoredScale.x) * diameter, targetDiameter),
                    Mathf.Max(0.01f, Mathf.Abs(authoredScale.y)),
                    Mathf.Max(Mathf.Abs(authoredScale.z) * diameter, targetDiameter));

                ConfigureExistingVisual(_visualInstance);
            }

            BuildRuntimeHazardFlair(diameter);
        }

        private bool ShouldUseRuntimeOnlyHazardVisual()
        {
            string key = ResolveHazardKey();
            return key.Contains("barley") || key.Contains("puddle");
        }

        private void BuildRuntimeHazardFlair(float diameter)
        {
            GameObject root = new GameObject("RuntimeHazardFlair");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _runtimeVisualRoot = root.transform;

            _puddleCore = CreatePrimitiveVisual(
                root.transform,
                "PuddleCore",
                PrimitiveType.Cylinder,
                Vector3.up * 0.016f,
                new Vector3(diameter * 0.82f, 0.012f, diameter * 0.82f),
                Quaternion.identity,
                _puddleCoreColor,
                out _puddleCoreRenderer);

            _puddleEdge = CreatePrimitiveVisual(
                root.transform,
                "PuddleEdge",
                PrimitiveType.Cylinder,
                Vector3.up * 0.022f,
                new Vector3(diameter, 0.008f, diameter),
                Quaternion.identity,
                _puddleEdgeColor,
                out _puddleEdgeRenderer);

            for (int i = 0; i < BubbleCount; i++)
            {
                float t = i / (float)BubbleCount;
                float angle = t * 360f + 18f;
                float radius = diameter * (0.15f + 0.22f * ((i % 3) / 2f));
                Vector3 offset =
                    Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                offset.y = 0.085f;

                Transform bubble = CreatePrimitiveVisual(
                    root.transform,
                    "PuddleBubble",
                    PrimitiveType.Sphere,
                    offset,
                    Vector3.one * (diameter * (0.035f + i % 2 * 0.012f)),
                    Quaternion.identity,
                    _puddleBubbleColor,
                    out Renderer bubbleRenderer);

                _bubbleTransforms[i] = bubble;
                _bubbleRenderers[i] = bubbleRenderer;
            }
        }

        private Transform CreatePrimitiveVisual(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Color color,
            out Renderer renderer)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            renderer = go.GetComponent<Renderer>();
            ConfigureRenderer(renderer, color);
            return go.transform;
        }

        private void ConfigureExistingVisual(GameObject visual)
        {
            if (visual == null)
                return;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ConfigureRenderer(renderers[i], _puddleCoreColor);
            }
        }

        private void ConfigureRenderer(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ResolveRuntimeHazardMaterial();
            ApplyRendererColor(renderer, color);
        }

        private void TickVisuals()
        {
            if (_definition == null || _runtimeVisualRoot == null)
                return;

            float duration = Mathf.Max(0.001f, _definition.DurationSeconds);
            float remaining = Mathf.Clamp01((duration - _elapsedLifetime) / duration);
            float fade = Mathf.Clamp01(remaining / 0.18f);
            float pulse = 1f + Mathf.Sin(Time.time * 5.2f + transform.position.x) * 0.035f;
            float diameter = _definition.Radius * 2f;

            if (_puddleCore != null)
                _puddleCore.localScale = new Vector3(
                    diameter * 0.82f * pulse,
                    0.012f,
                    diameter * 0.82f * pulse);

            if (_puddleEdge != null)
                _puddleEdge.localScale = new Vector3(
                    diameter * (1.01f + (pulse - 1f) * 1.8f),
                    0.008f,
                    diameter * (1.01f + (pulse - 1f) * 1.8f));

            Color coreColor = _puddleCoreColor;
            coreColor.a *= fade;
            Color edgeColor = _puddleEdgeColor;
            edgeColor.a *= fade * (0.72f + Mathf.PingPong(Time.time * 2.4f, 0.18f));

            ApplyRendererColor(_puddleCoreRenderer, coreColor);
            ApplyRendererColor(_puddleEdgeRenderer, edgeColor);

            for (int i = 0; i < BubbleCount; i++)
            {
                Transform bubble = _bubbleTransforms[i];
                if (bubble == null)
                    continue;

                float phase = Time.time * (2.2f + i * 0.23f) + i * 1.7f;
                float bubblePulse = 0.78f + Mathf.Sin(phase) * 0.22f;
                float diameterScale = diameter * (0.035f + i % 2 * 0.012f);
                bubble.localScale = Vector3.one * Mathf.Max(0.01f, diameterScale * bubblePulse);

                Vector3 pos = bubble.localPosition;
                pos.y = 0.075f + Mathf.PingPong(phase * 0.015f, 0.045f);
                bubble.localPosition = pos;

                Color bubbleColor = _puddleBubbleColor;
                bubbleColor.a *= fade * Mathf.Clamp01(bubblePulse);
                ApplyRendererColor(_bubbleRenderers[i], bubbleColor);
            }
        }

        private string ResolveHazardKey()
        {
            return $"{_definition?.name} {_definition?.HazardName} {_sourceAbility?.name}"
                .ToLowerInvariant();
        }

        private void ResolveHazardPalette(
            out Color core,
            out Color edge,
            out Color bubble)
        {
            string key = ResolveHazardKey();
            if (key.Contains("barley") || key.Contains("puddle"))
            {
                core = new Color(0.52f, 0.12f, 0.86f, 0.52f);
                edge = new Color(0.72f, 1f, 0.18f, 0.42f);
                bubble = new Color(1f, 0.82f, 0.22f, 0.82f);
                return;
            }

            core = new Color(1f, 0.28f, 0.18f, 0.42f);
            edge = new Color(1f, 0.78f, 0.16f, 0.38f);
            bubble = new Color(1f, 0.58f, 0.20f, 0.72f);
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

        private static Material ResolveRuntimeHazardMaterial()
        {
            if (_runtimeHazardMaterial != null)
                return _runtimeHazardMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            _runtimeHazardMaterial = new Material(shader);
            _runtimeHazardMaterial.color = Color.white;
            _runtimeHazardMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _runtimeHazardMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _runtimeHazardMaterial.SetInt("_ZWrite", 0);
            _runtimeHazardMaterial.DisableKeyword("_ALPHATEST_ON");
            _runtimeHazardMaterial.EnableKeyword("_ALPHABLEND_ON");
            _runtimeHazardMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _runtimeHazardMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _runtimeHazardMaterial;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }
    }
}
