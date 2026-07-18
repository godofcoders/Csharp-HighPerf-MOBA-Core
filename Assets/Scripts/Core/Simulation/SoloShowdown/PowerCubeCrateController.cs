using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class PowerCubeCrateController : MonoBehaviour, ISpatialEntity
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Crate")]
        [SerializeField, Min(1f)] private float _maxHealth = 4000f;
        [SerializeField, Min(0.05f)] private float _collisionRadius = 0.82f;
        [SerializeField] private bool _blocksNavigation = true;
        [SerializeField, Min(0.05f)] private float _navigationClearRadius = 0.95f;

        [Header("Reward")]
        [SerializeField] private PowerCube _powerCubePrefab;
        [SerializeField, Min(1)] private int _powerCubeValue = 1;
        [SerializeField, Min(0f)] private float _dropHeightOffset = 0.08f;

        [Header("Presentation")]
        [SerializeField] private Color _healthyColor = new Color(0.50f, 0.37f, 0.18f, 1f);
        [SerializeField] private Color _damagedColor = new Color(0.86f, 0.63f, 0.20f, 1f);
        [SerializeField] private Color _criticalColor = new Color(0.34f, 0.22f, 0.09f, 1f);
        [SerializeField] private Color _flashColor = new Color(1f, 0.95f, 0.32f, 1f);
        [SerializeField, Min(0f)] private float _flashSeconds = 0.08f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _propertyBlock;
        private int _entityId;
        private Vector3 _lastKnownPosition;
        private float _currentHealth;
        private float _flashUntilTime;
        private bool _registered;
        private bool _gridRegistered;
        private bool _destroyed;
        private bool _navigationBlocked;

        public int EntityID => GetEntityId();
        public Vector3 Position => this != null ? GetLivePosition() : _lastKnownPosition;
        public float CollisionRadius => Mathf.Max(0.05f, _collisionRadius);
        public TeamType Team => TeamType.Neutral;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => Mathf.Max(1f, _maxHealth);
        public bool IsDestroyed => _destroyed;

        private void Awake()
        {
            _entityId = gameObject.GetInstanceID();
            _lastKnownPosition = transform.position;
            _currentHealth = MaxHealth;
            CacheComponents();
            ApplyHealthTint();
        }

        private void OnEnable()
        {
            if (!_destroyed)
                Register();
        }

        private void Start()
        {
            if (!_destroyed)
                Register();

            TrySetNavigationBlocked(true);
        }

        private void OnDisable()
        {
            Unregister();
            TrySetNavigationBlocked(false);
        }

        private void OnDestroy()
        {
            Unregister();
            TrySetNavigationBlocked(false);
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

        public void Configure(
            PowerCube powerCubePrefab,
            float maxHealth,
            int powerCubeValue)
        {
            _powerCubePrefab = powerCubePrefab;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _powerCubeValue = Mathf.Max(1, powerCubeValue);
            _currentHealth = MaxHealth;
            _destroyed = false;
            _lastKnownPosition = transform.position;
            CacheComponents();
            SetPresentationEnabled(true);
            ApplyHealthTint();
            Register();
            TrySetNavigationBlocked(true);
        }

        public void TakeDamage(float amount)
        {
            if (!MatchStateUtility.IsCombatResolutionOpen())
                return;

            if (_destroyed || amount <= 0f)
                return;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            FlashHit();

            if (_currentHealth <= 0f)
                DestroyCrate();
            else
                ApplyHealthTint();
        }

        public void DestroyCrate()
        {
            if (_destroyed)
                return;

            _destroyed = true;
            _lastKnownPosition = Position;

            SpawnPowerCube();
            Unregister();
            TrySetNavigationBlocked(false);
            SetPresentationEnabled(false);
            Destroy(gameObject);
        }

        private void SpawnPowerCube()
        {
            Vector3 dropPosition = Position + Vector3.up * Mathf.Max(0f, _dropHeightOffset);
            PowerCube cube;
            if (_powerCubePrefab != null)
            {
                cube = Instantiate(_powerCubePrefab, dropPosition, Quaternion.identity);
            }
            else
            {
                GameObject cubeObject = new GameObject("PowerCube");
                cubeObject.transform.position = dropPosition;
                cube = cubeObject.AddComponent<PowerCube>();
            }

            if (cube != null)
                cube.SetValue(_powerCubeValue);
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

        private void TrySetNavigationBlocked(bool blocked)
        {
            if (!_blocksNavigation || _navigationBlocked == blocked)
                return;

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return;

            float radius = Mathf.Max(CollisionRadius, _navigationClearRadius);
            pathfinder.SetWalkableCircle(Position, radius, !blocked);
            _navigationBlocked = blocked;
        }

        private void CacheComponents()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        private void SetPresentationEnabled(bool enabled)
        {
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                        _renderers[i].enabled = enabled;
                }
            }

            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null)
                        _colliders[i].enabled = enabled;
                }
            }
        }

        private void FlashHit()
        {
            _flashUntilTime = Time.time + Mathf.Max(0f, _flashSeconds);
            ApplyTint(_flashColor);
        }

        private void ApplyHealthTint()
        {
            float healthPercent = MaxHealth > 0f ? _currentHealth / MaxHealth : 0f;
            if (healthPercent <= 0.35f)
                ApplyTint(_criticalColor);
            else if (healthPercent <= 0.72f)
                ApplyTint(_damagedColor);
            else
                ApplyTint(_healthyColor);
        }

        private void ApplyTint(Color color)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer crateRenderer = _renderers[i];
                if (crateRenderer == null)
                    continue;

                crateRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ColorId, color);
                _propertyBlock.SetColor(BaseColorId, color);
                crateRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
