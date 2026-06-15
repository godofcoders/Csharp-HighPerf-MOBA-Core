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

        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
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
            if (_definition == null || _definition.DestroyedVisualPrefab == null)
                return;

            Instantiate(_definition.DestroyedVisualPrefab, transform.position, transform.rotation);
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

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }
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
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
