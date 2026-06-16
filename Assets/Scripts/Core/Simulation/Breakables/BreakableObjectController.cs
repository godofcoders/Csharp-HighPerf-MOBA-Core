using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class BreakableObjectController : MonoBehaviour, ISpatialEntity
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private BreakableObjectDefinition _definition;
        [SerializeField] private float _fallbackCollisionRadius = 0.55f;

        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private int _entityId;
        private Vector3 _lastKnownPosition;
        private float _currentHealth;
        private float _flashUntilTime;
        private bool _registered;
        private bool _gridRegistered;
        private bool _destroyed;

        public BreakableObjectDefinition Definition => _definition;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _definition != null ? Mathf.Max(1f, _definition.MaxHealth) : 1f;
        public bool IsDestroyed => _destroyed;

        public int EntityID => GetEntityId();
        public Vector3 Position => this != null ? GetLivePosition() : _lastKnownPosition;
        public float CollisionRadius => _definition != null
            ? Mathf.Max(0.05f, _definition.CollisionRadius)
            : Mathf.Max(0.05f, _fallbackCollisionRadius);
        public TeamType Team => TeamType.Neutral;

        private void Awake()
        {
            EnsurePropertyBlock();
            _entityId = gameObject.GetInstanceID();
            _lastKnownPosition = transform.position;
            CachePresentation();

            if (_definition != null)
                Initialize(_definition);
        }

        private void OnEnable()
        {
            if (_definition != null && !_destroyed)
                Register();
        }

        private void Start()
        {
            if (_definition != null && !_destroyed)
                Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        private void Update()
        {
            if (_destroyed)
                return;

            if (_flashUntilTime > 0f && Time.time >= _flashUntilTime)
            {
                _flashUntilTime = 0f;
                ApplyHealthTint();
            }
        }

        public void Initialize(BreakableObjectDefinition definition)
        {
            _definition = definition;
            _destroyed = false;
            _currentHealth = MaxHealth;
            _lastKnownPosition = transform.position;
            CachePresentation();
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
            ApplyHealthTint();
            Register();
        }

        public bool CanReceiveDamage(in DamageContext context)
        {
            if (_destroyed || _definition == null)
                return false;

            if (context.Damage <= 0f)
                return false;

            if (_definition.RequiresSuperDamage && !context.IsSuper)
                return false;

            if (_definition.RequiredSourceAbility != null &&
                context.SourceAbility != _definition.RequiredSourceAbility)
            {
                return false;
            }

            switch (context.Type)
            {
                case DamageType.Projectile:
                    return _definition.CanBeDamagedByProjectiles;

                case DamageType.AoE:
                    return _definition.CanBeDamagedByAreaEffects;

                default:
                    return true;
            }
        }

        public void TakeDamage(float amount)
        {
            if (_destroyed || _definition == null || amount <= 0f)
                return;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            FlashHit();

            if (_currentHealth <= 0f)
                DestroyBreakable();
            else
                ApplyHealthTint();
        }

        public void DestroyBreakable()
        {
            if (_destroyed)
                return;

            _destroyed = true;
            _lastKnownPosition = Position;

            Unregister();
            UpdateNavigationAfterDestroyed();
            SpawnDestroyedVisual();

            SetCollidersEnabled(false);
            SetRenderersEnabled(false);

            if (_definition != null && _definition.DestroyGameObjectOnDeath)
                DestroyGeneratedObject(gameObject);
        }

        private int GetEntityId()
        {
            if (_entityId != 0)
                return _entityId;

            if (this == null)
                return 0;

            _entityId = gameObject.GetInstanceID();
            return _entityId;
        }

        private Vector3 GetLivePosition()
        {
            _lastKnownPosition = transform.position;
            return _lastKnownPosition;
        }

        private void Register()
        {
            if (_destroyed)
                return;

            if (!_registered)
            {
                CombatRegistry.Register(this);
                _registered = true;
            }

            if (!_gridRegistered && SimulationClock.Grid != null)
            {
                SimulationClock.Grid.Add(this);
                _gridRegistered = true;
            }
        }

        private void Unregister()
        {
            if (!_registered && !_gridRegistered)
                return;

            if (_gridRegistered)
            {
                SimulationClock.Grid?.Remove(this, _lastKnownPosition);
                _gridRegistered = false;
            }

            if (_registered)
            {
                CombatRegistry.Unregister(this);
                _registered = false;
            }
        }

        private void UpdateNavigationAfterDestroyed()
        {
            if (_definition == null || !_definition.BlocksNavigation)
                return;

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return;

            float radius = Mathf.Max(CollisionRadius, _definition.NavigationClearRadius);
            pathfinder.SetWalkableCircle(Position, radius, true);
        }

        private void SpawnDestroyedVisual()
        {
            if (_definition == null)
                return;

            if (_definition.DestroyedVisualPrefab != null)
            {
                Instantiate(_definition.DestroyedVisualPrefab, transform.position, transform.rotation);
                return;
            }

            if (_definition.SpawnFallbackDebris)
                SpawnFallbackDebris();
        }

        private void SpawnFallbackDebris()
        {
            int pieceCount = Mathf.Clamp(_definition.FallbackDebrisPieces, 0, 12);
            if (pieceCount <= 0)
                return;

            GameObject root = new GameObject($"{name}_DestroyedDebris");
            root.transform.position = transform.position;
            root.transform.rotation = Quaternion.identity;

            Material debrisMaterial = CreateFallbackDebrisMaterial();
            float radius = Mathf.Max(0.25f, CollisionRadius);

            for (int i = 0; i < pieceCount; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"Debris_{i + 1}";
                piece.transform.SetParent(root.transform, false);

                float t = i / Mathf.Max(1f, pieceCount);
                float angle = t * Mathf.PI * 2f;
                float distance = radius * (0.25f + 0.08f * (i % 3));
                piece.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * distance,
                    0.08f + 0.035f * (i % 4),
                    Mathf.Sin(angle) * distance);

                float size = 0.18f + 0.035f * (i % 3);
                piece.transform.localScale = new Vector3(size * 1.25f, size * 0.65f, size);
                piece.transform.localRotation = Quaternion.Euler(
                    12f + i * 17f,
                    Mathf.Rad2Deg * angle,
                    8f + i * 23f);

                Collider pieceCollider = piece.GetComponent<Collider>();
                if (pieceCollider != null)
                    DestroyGeneratedObject(pieceCollider);

                Renderer pieceRenderer = piece.GetComponent<Renderer>();
                if (pieceRenderer != null && debrisMaterial != null)
                {
                    pieceRenderer.sharedMaterial = debrisMaterial;
                    pieceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pieceRenderer.receiveShadows = false;
                }
            }

            float lifetime = Mathf.Max(0.1f, _definition.FallbackDebrisLifetimeSeconds);
            DestroyGeneratedObject(root, lifetime);
            DestroyGeneratedObject(debrisMaterial, lifetime);
        }

        private Material CreateFallbackDebrisMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");

            if (shader == null)
                return null;

            Material material = new Material(shader);
            Color color = _definition != null
                ? _definition.FallbackDebrisColor
                : new Color(0.42f, 0.34f, 0.26f, 1f);

            material.color = color;
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);

            return material;
        }

        private void CachePresentation()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        private void FlashHit()
        {
            if (_definition == null)
                return;

            _flashUntilTime = Time.time + Mathf.Max(0.01f, _definition.HitFlashSeconds);
            ApplyColor(_definition.HitFlashColor);
        }

        private void ApplyHealthTint()
        {
            if (_definition == null || _currentHealth <= 0f)
            {
                ClearColor();
                return;
            }

            float healthPercent = _currentHealth / MaxHealth;
            if (healthPercent <= Mathf.Clamp01(_definition.CriticalHealthPercent))
                ApplyColor(_definition.CriticalTint);
            else
                ClearColor();
        }

        private void ApplyColor(Color color)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                EnsurePropertyBlock();
                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private void ClearColor()
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                    _colliders[i].enabled = enabled;
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = enabled;
            }
        }

        private static void DestroyGeneratedObject(Object target)
        {
            DestroyGeneratedObject(target, 0f);
        }

        private static void DestroyGeneratedObject(Object target, float delaySeconds)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target, Mathf.Max(0f, delaySeconds));
            else
                DestroyImmediate(target);
        }
    }
}
