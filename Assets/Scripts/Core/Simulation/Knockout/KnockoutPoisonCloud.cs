using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MOBA.Core.Simulation
{
    public sealed class KnockoutPoisonCloud : MonoBehaviour
    {
        public static KnockoutPoisonCloud Instance { get; private set; }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private const string MatchSceneName = "Match";
        private const float SpawnFallbackBoundsPaddingX = 10f;
        private const float SpawnFallbackBoundsPaddingZ = 12f;

        [Header("Safe Zone")]
        [SerializeField] private Transform _centerOverride;
        [SerializeField] private Vector2 _initialHalfExtents = new Vector2(28f, 28f);
        [SerializeField] private Vector2 _finalHalfExtents = new Vector2(5.5f, 5.5f);
        [SerializeField, Min(0f)] private float _initialBoundsPadding = 9f;
        [SerializeField, Min(0f)] private float _shrinkDelaySeconds = 20f;
        [SerializeField, Min(1f)] private float _shrinkDurationSeconds = 55f;
        [SerializeField, Min(0f)] private float _dangerBuffer = 2.8f;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float _damagePerTick = 520f;
        [SerializeField, Min(0.1f)] private float _tickIntervalSeconds = 1f;
        [SerializeField, Min(0.1f)] private float _cacheRefreshSeconds = 1.5f;

        [Header("Visuals")]
        [SerializeField] private Color _cloudColor = new Color(0.32f, 0.78f, 0.16f, 0.24f);
        [SerializeField] private Color _edgeColor = new Color(0.72f, 1f, 0.18f, 0.46f);
        [SerializeField, Min(0.01f)] private float _cloudHeight = 0.04f;
        [SerializeField, Min(0.01f)] private float _edgeWidth = 0.18f;

        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(8);
        private readonly Transform[] _cloudStrips = new Transform[4];
        private readonly Transform[] _edgeStrips = new Transform[4];
        private readonly Renderer[] _cloudRenderers = new Renderer[4];
        private readonly Renderer[] _edgeRenderers = new Renderer[4];

        private Material _cloudMaterial;
        private Material _edgeMaterial;
        private float _elapsedActiveSeconds;
        private float _tickTimer;
        private float _cacheRefreshTimer;
        private bool _visualsBuilt;
        private bool _hasResolvedCenter;
        private Vector3 _resolvedCenter;

        public Vector3 Center => _centerOverride != null
            ? _centerOverride.position
            : _hasResolvedCenter
                ? _resolvedCenter
                : transform.position;

        public Vector2 CurrentHalfExtents { get; private set; }
        public Vector2 InitialHalfExtents => _initialHalfExtents;
        public bool IsShrinking => _elapsedActiveSeconds >= _shrinkDelaySeconds;
        public bool IsHazardActive => ShouldSimulatePoison() && IsShrinking;
        public float DamagePerTick => _damagePerTick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            CurrentHalfExtents = SanitizeHalfExtents(_initialHalfExtents);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDisable()
        {
            SetVisualsVisible(false);
        }

        private void Update()
        {
            if (!ShouldSimulatePoison())
            {
                SetVisualsVisible(false);
                return;
            }

            float deltaTime = Time.deltaTime;
            _elapsedActiveSeconds += deltaTime;
            _tickTimer += deltaTime;
            _cacheRefreshTimer += deltaTime;

            UpdateSafeZone();
            UpdateVisuals();

            if (_cacheRefreshTimer >= _cacheRefreshSeconds)
            {
                _cacheRefreshTimer = 0f;
                RefreshBrawlerCache();
            }

            if (_tickTimer >= _tickIntervalSeconds)
            {
                _tickTimer -= _tickIntervalSeconds;
                ApplyPoisonTick();
            }
        }

        public void ConfigureFromBrawlers(IList<BrawlerController> brawlers)
        {
            bool hasBrawlers = brawlers != null && brawlers.Count > 0;
            bool hasMapBounds = TryResolvePlayableMapBounds(out Bounds mapBounds);
            if (!hasBrawlers && !hasMapBounds)
                return;

            Vector3 center = Center;
            Vector2 halfExtents = SanitizeHalfExtents(_initialHalfExtents);
            if (hasMapBounds)
            {
                center = new Vector3(mapBounds.center.x, transform.position.y, mapBounds.center.z);
                _resolvedCenter = center;
                _hasResolvedCenter = true;
                halfExtents = SanitizeHalfExtents(new Vector2(mapBounds.extents.x, mapBounds.extents.z));
            }

            if (hasBrawlers)
            {
                for (int i = 0; i < brawlers.Count; i++)
                {
                    BrawlerController brawler = brawlers[i];
                    if (brawler == null)
                        continue;

                    if (!hasMapBounds)
                    {
                        Vector3 delta = brawler.Position - center;
                        halfExtents.x = Mathf.Max(halfExtents.x, Mathf.Abs(delta.x) + _initialBoundsPadding);
                        halfExtents.y = Mathf.Max(halfExtents.y, Mathf.Abs(delta.z) + _initialBoundsPadding);
                    }

                    if (!_brawlers.Contains(brawler))
                        _brawlers.Add(brawler);
                }
            }

            _initialHalfExtents = halfExtents;
            CurrentHalfExtents = halfExtents;
            EnsureVisuals();
            UpdateVisuals();
        }

        public void ResetCloud()
        {
            _elapsedActiveSeconds = 0f;
            _tickTimer = 0f;
            _cacheRefreshTimer = 0f;
            CurrentHalfExtents = SanitizeHalfExtents(_initialHalfExtents);
            UpdateVisuals();
        }

        public bool IsInsideSafeZone(Vector3 position)
        {
            Vector3 delta = position - Center;
            return Mathf.Abs(delta.x) <= CurrentHalfExtents.x &&
                   Mathf.Abs(delta.z) <= CurrentHalfExtents.y;
        }

        public float GetDistanceBeyondSafeZone(Vector3 position)
        {
            Vector3 delta = position - Center;
            float outsideX = Mathf.Max(0f, Mathf.Abs(delta.x) - CurrentHalfExtents.x);
            float outsideZ = Mathf.Max(0f, Mathf.Abs(delta.z) - CurrentHalfExtents.y);
            return Mathf.Max(outsideX, outsideZ);
        }

        public float GetEdgeDangerDistance(Vector3 position)
        {
            Vector3 delta = position - Center;
            float edgeX = CurrentHalfExtents.x - Mathf.Abs(delta.x);
            float edgeZ = CurrentHalfExtents.y - Mathf.Abs(delta.z);
            return Mathf.Min(edgeX, edgeZ);
        }

        public bool IsInDangerBand(Vector3 position)
        {
            return GetDistanceBeyondSafeZone(position) > 0f ||
                   GetEdgeDangerDistance(position) <= _dangerBuffer;
        }

        public void AppendPoisonThreatNonAlloc(
            Vector3 observerPosition,
            TeamType observerTeam,
            float scanRadius,
            List<GameplayThreatInfo> results)
        {
            if (results == null || !IsHazardActive)
                return;

            float outsideDistance = GetDistanceBeyondSafeZone(observerPosition);
            float edgeDistance = GetEdgeDangerDistance(observerPosition);
            if (outsideDistance <= 0f && edgeDistance > Mathf.Max(_dangerBuffer, scanRadius * 0.35f))
                return;

            Vector3 outward = ResolveOutwardDirection(observerPosition);
            if (outward.sqrMagnitude <= 0.001f)
                outward = Vector3.forward;

            float radius = outsideDistance > 0f
                ? Mathf.Max(1.6f, outsideDistance + _dangerBuffer)
                : Mathf.Max(1.2f, _dangerBuffer - edgeDistance);

            results.Add(new GameplayThreatInfo
            {
                Owner = null,
                Team = TeamType.Neutral,
                Position = observerPosition + outward.normalized * Mathf.Max(0.6f, radius),
                Direction = -outward.normalized,
                Radius = radius,
                Damage = _damagePerTick,
                TimeToImpact = outsideDistance > 0f ? 0f : 0.35f,
                IsProjectile = false,
                IsAreaHazard = true,
                IsSuper = false
            });
        }

        private void UpdateSafeZone()
        {
            Vector2 initial = SanitizeHalfExtents(_initialHalfExtents);
            Vector2 final = SanitizeHalfExtents(_finalHalfExtents);
            final.x = Mathf.Min(final.x, initial.x);
            final.y = Mathf.Min(final.y, initial.y);

            float progress = _elapsedActiveSeconds <= _shrinkDelaySeconds
                ? 0f
                : Mathf.Clamp01((_elapsedActiveSeconds - _shrinkDelaySeconds) /
                                Mathf.Max(0.1f, _shrinkDurationSeconds));

            CurrentHalfExtents = Vector2.Lerp(initial, final, progress);
        }

        private void RefreshBrawlerCache()
        {
            _brawlers.Clear();
            BrawlerController[] discovered = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < discovered.Length; i++)
            {
                if (discovered[i] != null)
                    _brawlers.Add(discovered[i]);
            }
        }

        private void ApplyPoisonTick()
        {
            if (!IsHazardActive || _damagePerTick <= 0f)
                return;

            if (_brawlers.Count == 0)
                RefreshBrawlerCache();

            if (!ServiceProvider.TryGet<IDamageService>(out var damageService))
                return;

            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController brawler = _brawlers[i];
                if (!SpatialEntityUtility.IsAlive(brawler) ||
                    brawler.State == null ||
                    brawler.State.IsDead ||
                    IsInsideSafeZone(brawler.Position))
                {
                    continue;
                }

                Vector3 direction = brawler.Position - Center;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.001f)
                    direction = Vector3.forward;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = null,
                    Target = brawler,
                    Damage = _damagePerTick,
                    Type = DamageType.AoE,
                    HitPosition = brawler.Position,
                    Direction = direction.normalized,
                    SourceAbility = null,
                    IsSuper = false
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = null,
                    Target = brawler,
                    AbilityDefinition = null,
                    SlotType = default,
                    Position = brawler.Position,
                    Direction = direction.normalized,
                    Value = _damagePerTick,
                    IsSuper = false,
                    IsHypercharged = false,
                    IsLingeringAreaEffect = true
                });
            }
        }

        private Vector3 ResolveOutwardDirection(Vector3 position)
        {
            Vector3 delta = position - Center;
            float edgeX = CurrentHalfExtents.x - Mathf.Abs(delta.x);
            float edgeZ = CurrentHalfExtents.y - Mathf.Abs(delta.z);

            if (edgeX < edgeZ)
                return new Vector3(delta.x >= 0f ? 1f : -1f, 0f, 0f);

            return new Vector3(0f, 0f, delta.z >= 0f ? 1f : -1f);
        }

        private void EnsureVisuals()
        {
            if (_visualsBuilt)
                return;

            _visualsBuilt = true;
            _cloudMaterial = CreateRuntimeMaterial(_cloudColor);
            _edgeMaterial = CreateRuntimeMaterial(_edgeColor);

            for (int i = 0; i < 4; i++)
            {
                _cloudStrips[i] = CreateStrip($"PoisonCloud_{i}", _cloudMaterial, out _cloudRenderers[i]);
                _edgeStrips[i] = CreateStrip($"PoisonEdge_{i}", _edgeMaterial, out _edgeRenderers[i]);
            }
        }

        private Transform CreateStrip(string stripName, Material material, out Renderer stripRenderer)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = stripName;
            strip.transform.SetParent(transform, false);

            Collider collider = strip.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            stripRenderer = strip.GetComponent<Renderer>();
            if (stripRenderer != null)
                stripRenderer.sharedMaterial = material;

            return strip.transform;
        }

        private void UpdateVisuals()
        {
            EnsureVisuals();

            SetVisualsVisible(IsHazardActive);

            Vector3 center = Center;
            Vector2 initial = SanitizeHalfExtents(_initialHalfExtents);
            Vector2 current = CurrentHalfExtents;

            float leftWidth = Mathf.Max(0.01f, initial.x - current.x);
            float rightWidth = leftWidth;
            float bottomDepth = Mathf.Max(0.01f, initial.y - current.y);
            float topDepth = bottomDepth;
            float fullDepth = initial.y * 2f;
            float currentWidth = current.x * 2f;

            SetStrip(_cloudStrips[0], center + new Vector3(-current.x - leftWidth * 0.5f, _cloudHeight, 0f), new Vector3(leftWidth, _cloudHeight, fullDepth));
            SetStrip(_cloudStrips[1], center + new Vector3(current.x + rightWidth * 0.5f, _cloudHeight, 0f), new Vector3(rightWidth, _cloudHeight, fullDepth));
            SetStrip(_cloudStrips[2], center + new Vector3(0f, _cloudHeight, -current.y - bottomDepth * 0.5f), new Vector3(currentWidth, _cloudHeight, bottomDepth));
            SetStrip(_cloudStrips[3], center + new Vector3(0f, _cloudHeight, current.y + topDepth * 0.5f), new Vector3(currentWidth, _cloudHeight, topDepth));

            SetStrip(_edgeStrips[0], center + new Vector3(-current.x, _cloudHeight * 1.35f, 0f), new Vector3(_edgeWidth, _cloudHeight * 1.4f, current.y * 2f));
            SetStrip(_edgeStrips[1], center + new Vector3(current.x, _cloudHeight * 1.35f, 0f), new Vector3(_edgeWidth, _cloudHeight * 1.4f, current.y * 2f));
            SetStrip(_edgeStrips[2], center + new Vector3(0f, _cloudHeight * 1.35f, -current.y), new Vector3(current.x * 2f, _cloudHeight * 1.4f, _edgeWidth));
            SetStrip(_edgeStrips[3], center + new Vector3(0f, _cloudHeight * 1.35f, current.y), new Vector3(current.x * 2f, _cloudHeight * 1.4f, _edgeWidth));
        }

        private static void SetStrip(Transform strip, Vector3 position, Vector3 scale)
        {
            if (strip == null)
                return;

            strip.position = position;
            strip.localRotation = Quaternion.identity;
            strip.localScale = new Vector3(
                Mathf.Max(0.01f, scale.x),
                Mathf.Max(0.01f, scale.y),
                Mathf.Max(0.01f, scale.z));
        }

        private static void SetStripVisible(Transform strip, bool visible)
        {
            if (strip != null && strip.gameObject.activeSelf != visible)
                strip.gameObject.SetActive(visible);
        }

        private void SetVisualsVisible(bool visible)
        {
            for (int i = 0; i < _cloudStrips.Length; i++)
            {
                SetStripVisible(_cloudStrips[i], visible);
                SetStripVisible(_edgeStrips[i], visible);
            }
        }

        private static bool ShouldSimulatePoison()
        {
            MatchManager matchManager = MatchManager.Instance;
            return matchManager != null &&
                   matchManager.CurrentState == MatchState.Active &&
                   KnockoutMode.Instance != null &&
                   SceneManager.GetActiveScene().name == MatchSceneName;
        }

        private static bool TryResolvePlayableMapBounds(out Bounds bounds)
        {
            if (TryResolveSpawnedMapGroundBounds(out bounds))
                return true;

            if (TryResolveSpawnPointBounds(out bounds))
                return true;

            return TryResolveGeneratorBounds(out bounds);
        }

        private static bool TryResolveSpawnedMapGroundBounds(out Bounds bounds)
        {
            bounds = default;

            MapLoader mapLoader = FindObjectOfType<MapLoader>();
            GameObject spawnedMap = mapLoader != null ? mapLoader.SpawnedMapInstance : null;
            if (spawnedMap == null)
                return false;

            int excludedMask = ResolveObstacleMask() |
                               ResolveLayerMask("Bushes") |
                               ResolveLayerMask("Bush");
            bool found = false;

            Collider[] colliders = spawnedMap.GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.isTrigger ||
                    !IsMapBoundsCandidate(collider.gameObject, excludedMask))
                {
                    continue;
                }

                EncapsulateBounds(collider.bounds, ref bounds, ref found);
            }

            if (found)
                return true;

            Renderer[] renderers = spawnedMap.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !IsMapBoundsCandidate(renderer.gameObject, excludedMask))
                {
                    continue;
                }

                EncapsulateBounds(renderer.bounds, ref bounds, ref found);
            }

            return found;
        }

        private static bool IsMapBoundsCandidate(GameObject candidate, int excludedMask)
        {
            if (candidate == null)
                return false;

            int layerMask = 1 << candidate.layer;
            if ((excludedMask & layerMask) != 0)
                return false;

            string objectName = candidate.name;
            return objectName.IndexOf("Poison", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                   objectName.IndexOf("ArenaWall", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                   objectName.IndexOf("RuntimeArenaBoundary", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool TryResolveSpawnPointBounds(out Bounds bounds)
        {
            SpawnPointMarker[] markers = FindObjectsOfType<SpawnPointMarker>(false);
            if (markers == null || markers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bool found = false;
            bounds = default;
            for (int i = 0; i < markers.Length; i++)
            {
                SpawnPointMarker marker = markers[i];
                if (marker == null)
                    continue;

                EncapsulateBounds(new Bounds(marker.transform.position, Vector3.zero), ref bounds, ref found);
            }

            if (!found)
                return false;

            bounds.Expand(new Vector3(SpawnFallbackBoundsPaddingX, 0f, SpawnFallbackBoundsPaddingZ));
            return true;
        }

        private static bool TryResolveGeneratorBounds(out Bounds bounds)
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator == null)
            {
                bounds = default;
                return false;
            }

            float cellSize = Mathf.Max(0.1f, mapGenerator.CellSize);
            float width = Mathf.Max(1, mapGenerator.Width) * cellSize;
            float height = Mathf.Max(1, mapGenerator.Height) * cellSize;
            bounds = new Bounds(
                mapGenerator.transform.position,
                new Vector3(width, 0f, height));
            return true;
        }

        private static void EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool found)
        {
            if (!found)
            {
                bounds = candidate;
                found = true;
                return;
            }

            bounds.Encapsulate(candidate);
        }

        private static int ResolveObstacleMask()
        {
            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
                return mapGenerator.ObstacleLayer.value;

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
        }

        private static int ResolveLayerMask(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }

        private static Vector2 SanitizeHalfExtents(Vector2 value)
        {
            return new Vector2(
                Mathf.Max(0.5f, value.x),
                Mathf.Max(0.5f, value.y));
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            Shader shader = ResolveTransparentShader();
            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.name = "Runtime_KnockoutPoisonGas";
            material.color = color;
            material.SetOverrideTag("RenderType", "Transparent");

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);

            SetMaterialFloatIfPresent(material, SurfaceId, 1f);
            SetMaterialFloatIfPresent(material, ModeId, 3f);
            SetMaterialFloatIfPresent(material, BlendId, 0f);
            SetMaterialFloatIfPresent(material, AlphaClipId, 0f);
            SetMaterialFloatIfPresent(material, CullId, (float)CullMode.Off);
            SetMaterialIntIfPresent(material, SrcBlendId, (int)BlendMode.SrcAlpha);
            SetMaterialIntIfPresent(material, DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            SetMaterialIntIfPresent(material, ZWriteId, 0);
            SetMaterialIntIfPresent(material, CullId, (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static Shader ResolveTransparentShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                return Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                       Shader.Find("Universal Render Pipeline/Unlit") ??
                       Shader.Find("Sprites/Default") ??
                       Shader.Find("Standard");
            }

            return Shader.Find("Particles/Standard Unlit") ??
                   Shader.Find("Sprites/Default") ??
                   Shader.Find("Legacy Shaders/Transparent/Diffuse") ??
                   Shader.Find("Unlit/Transparent") ??
                   Shader.Find("Standard");
        }

        private static void SetMaterialFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetFloat(propertyId, value);
        }

        private static void SetMaterialIntIfPresent(Material material, int propertyId, int value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetInt(propertyId, value);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Center;
            Vector2 current = Application.isPlaying
                ? CurrentHalfExtents
                : SanitizeHalfExtents(_initialHalfExtents);

            Gizmos.color = new Color(0.44f, 0.95f, 0.18f, 0.48f);
            Vector3 size = new Vector3(current.x * 2f, 0.1f, current.y * 2f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
