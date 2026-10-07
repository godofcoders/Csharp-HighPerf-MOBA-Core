using System.Collections;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class PowerCubeCrateController : MonoBehaviour, ISpatialEntity
    {
        private const string RuntimeChestRootName = "ChestVisual";
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Crate")]
        [SerializeField, Min(1f)] private float _maxHealth = 4000f;
        [SerializeField, Min(0.05f)] private float _collisionRadius = 0.58f;
        [SerializeField] private bool _blocksNavigation = true;
        [SerializeField, Min(0.05f)] private float _navigationClearRadius = 0.72f;

        [Header("Reward")]
        [SerializeField] private PowerCube _powerCubePrefab;
        [SerializeField, Min(1)] private int _powerCubeValue = 1;
        [SerializeField, Min(0f)] private float _dropHeightOffset = 0.08f;

        [Header("Presentation")]
        [SerializeField] private Color _woodColor = new Color(0.36f, 0.16f, 0.055f, 1f);
        [SerializeField] private Color _lidColor = new Color(0.52f, 0.25f, 0.075f, 1f);
        [SerializeField] private Color _bandColor = new Color(0.15f, 0.17f, 0.20f, 1f);
        [SerializeField] private Color _latchColor = new Color(0.96f, 0.66f, 0.12f, 1f);
        [SerializeField, Min(0f)] private float _revealDelaySeconds = 0.10f;
        [SerializeField, Min(0f)] private float _powerCubeSpawnDelaySeconds = 0.24f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private MaterialPropertyBlock _propertyBlock;
        private int _entityId;
        private Vector3 _lastKnownPosition;
        private float _currentHealth;
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

        public void BuildFallbackPresentation()
        {
            Transform existing = transform.Find(RuntimeChestRootName);
            if (existing == null)
            {
                GameObject visualObject = new GameObject(RuntimeChestRootName);
                visualObject.layer = gameObject.layer;
                existing = visualObject.transform;
                existing.SetParent(transform, false);

                CreateChestPart(existing, "Body", new Vector3(0f, 0.25f, 0f), new Vector3(0.86f, 0.46f, 0.68f));
                CreateChestPart(existing, "Lid", new Vector3(0f, 0.54f, 0f), new Vector3(0.90f, 0.17f, 0.72f));
                CreateChestPart(existing, "BandLeft", new Vector3(-0.27f, 0.39f, 0f), new Vector3(0.085f, 0.61f, 0.73f));
                CreateChestPart(existing, "BandRight", new Vector3(0.27f, 0.39f, 0f), new Vector3(0.085f, 0.61f, 0.73f));
                CreateChestPart(existing, "Latch", new Vector3(0f, 0.42f, 0.37f), new Vector3(0.18f, 0.24f, 0.075f));
            }

            BoxCollider rootCollider = GetComponent<BoxCollider>();
            if (rootCollider == null)
                rootCollider = gameObject.AddComponent<BoxCollider>();

            rootCollider.center = new Vector3(0f, 0.33f, 0f);
            rootCollider.size = new Vector3(0.90f, 0.66f, 0.72f);

            CacheComponents();
            ApplyBaseColors();
        }

        private void Awake()
        {
            _entityId = gameObject.GetInstanceID();
            _lastKnownPosition = transform.position;
            _currentHealth = MaxHealth;
            CacheComponents();
            ApplyBaseColors();
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
            ApplyBaseColors();
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

            if (_currentHealth <= 0f)
                DestroyCrate();
        }

        public void DestroyCrate()
        {
            if (_destroyed)
                return;

            _destroyed = true;
            _lastKnownPosition = Position;

            Unregister();
            TrySetNavigationBlocked(false);
            SetPresentationEnabled(false);

            if (!Application.isPlaying)
            {
                SpawnPowerCube();
                return;
            }

            StartCoroutine(BreakAndDropRoutine());
        }

        private IEnumerator BreakAndDropRoutine()
        {
            Vector3 effectPosition = _lastKnownPosition + Vector3.up * 0.28f;
            PowerCubeCrateVfx.CreateBreakBurst(effectPosition);

            float revealDelay = Mathf.Max(0f, _revealDelaySeconds);
            if (revealDelay > 0f)
                yield return new WaitForSeconds(revealDelay);

            Vector3 dropPosition = ResolveDropPosition();
            PowerCubeCrateVfx.CreateRevealBurst(dropPosition);

            float remainingDelay = Mathf.Max(0f, _powerCubeSpawnDelaySeconds - revealDelay);
            if (remainingDelay > 0f)
                yield return new WaitForSeconds(remainingDelay);

            SpawnPowerCube();
            Destroy(gameObject);
        }

        private void SpawnPowerCube()
        {
            Vector3 dropPosition = ResolveDropPosition();
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

        private Vector3 ResolveDropPosition()
        {
            Vector3 origin = _destroyed ? _lastKnownPosition : Position;
            return origin + Vector3.up * Mathf.Max(0f, _dropHeightOffset);
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

        private void ApplyBaseColors()
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer crateRenderer = _renderers[i];
                if (crateRenderer == null)
                    continue;

                crateRenderer.GetPropertyBlock(_propertyBlock);
                Color partColor = ResolvePartColor(crateRenderer.transform.name);
                _propertyBlock.SetColor(ColorId, partColor);
                _propertyBlock.SetColor(BaseColorId, partColor);
                crateRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private Color ResolvePartColor(string partName)
        {
            if (partName == "Latch")
                return _latchColor;

            if (partName.StartsWith("Band", System.StringComparison.Ordinal))
                return _bandColor;

            return partName == "Lid" ? _lidColor : _woodColor;
        }

        private static void CreateChestPart(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.layer = parent.gameObject.layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider == null)
                return;

            partCollider.enabled = false;
            if (Application.isPlaying)
                Destroy(partCollider);
            else
                DestroyImmediate(partCollider);
        }
    }
}
