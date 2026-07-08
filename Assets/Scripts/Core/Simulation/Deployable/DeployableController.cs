using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class DeployableController : SimulationEntity, ISpatialEntity
    {
        private DeployableDefinition _definition;
        private BrawlerController _owner;
        private TeamType _team;
        private uint _spawnTick;
        private uint _expiryTick;
        private float _currentHealth;
        private IDeployableBehavior _behavior;
        private DeployableState _state;
        private int _entityId;
        private Vector3 _lastKnownPosition;
        private bool _isDespawning;
        private DeployablePresentationView _presentationView;
        public DeployableState State => _state;

        public DeployableDefinition Definition => _definition;
        public BrawlerController Owner => _owner;
        public TeamType Team => _team;
        public Vector3 Position => this != null ? GetLivePosition() : _lastKnownPosition;
        public Vector3 CurrentPosition => Position;
        public float CollisionRadius => 0.5f;
        public int EntityID => GetEntityId();
        public bool IsExpired(uint currentTick) => currentTick >= _expiryTick;
        public bool IsDead => _state != null ? _state.IsDead : _currentHealth <= 0f;

        private DeployableAbilityUser _abilityUser;
        private IAbilityLogic _abilityLogic;

        public DeployableAbilityUser AbilityUser => _abilityUser;
        public IAbilityLogic AbilityLogic => _abilityLogic;

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

        protected override void Awake()
        {
            _entityId = gameObject.GetInstanceID();
            _lastKnownPosition = transform.position;
            base.Awake();
        }

        public void Initialize(DeployableSpawnRequest request)
        {
            _definition = request.Definition;
            _owner = request.Owner;
            _team = request.Team;
            _spawnTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            _expiryTick = _spawnTick + SimulationClock.SecondsToTicks(_definition.LifetimeSeconds);
            _currentHealth = _definition.MaxHealth;
            _abilityUser = new DeployableAbilityUser(this);
            _abilityLogic = _definition.AbilityDefinition != null
                ? _definition.AbilityDefinition.CreateLogic()
                : null;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            _state = new DeployableState(_definition, _owner, _team, currentTick);
            _state.Controller = this;

            transform.position = request.Position;
            _lastKnownPosition = request.Position;
            _isDespawning = false;
            EnsurePresentation();

            _behavior = CreateBehavior(_definition.DeployableType);
            _behavior?.Initialize(this);

            // Note: SimulationClock.Registry.Register(this) is NOT called here.
            // The base class SimulationEntity.OnEnable already registered us under
            // the default Movement phase when Unity instantiated this GameObject.
            // Tick() guards on _definition == null, so ticks before Initialize()
            // completes are safe no-ops.
            CombatRegistry.Register(this);
            SimulationClock.Grid?.Add(this);
        }

        public override void Tick(uint currentTick)
        {
            if (_definition == null)
                return;

            if (_state == null)
                return;

            if (_isDespawning)
                return;

            if (_state.IsDead || _state.IsExpired(currentTick))
            {
                Despawn();
                return;
            }

            _lastKnownPosition = Position;
            _behavior?.Tick(currentTick);
            _presentationView?.TickPresentation(SimulationClock.TickDeltaTime);
        }

        public void SetPresentationAimDirection(Vector3 direction)
        {
            _presentationView?.SetAimDirection(direction);
        }

        public void TakeDamage(float amount)
        {
            if (!MatchStateUtility.IsCombatResolutionOpen())
                return;

            if (_state == null)
                return;

            _state.TakeDamage(amount);

            if (_state.IsDead)
                Despawn();
        }

        public void Despawn()
        {
            if (_isDespawning)
                return;

            _isDespawning = true;
            _lastKnownPosition = Position;

            IDeployableRegistry registry = ServiceProvider.Get<IDeployableRegistry>();
            registry?.Unregister(this);

            // Destroy(gameObject) will fire OnDisable, which unregisters from
            // SimulationClock.Registry via the SimulationEntity base class.
            SimulationClock.Grid?.Remove(this, _lastKnownPosition);
            CombatRegistry.Unregister(this);
            Destroy(gameObject);
        }

        private IDeployableBehavior CreateBehavior(DeployableType type)
        {
            switch (type)
            {
                case DeployableType.Turret:
                    return new TurretDeployableBehavior();

                case DeployableType.BuffZone:
                    return new BuffZoneDeployableBehavior();

                case DeployableType.HealingStation:
                    return new HealingStationDeployableBehavior();

                case DeployableType.SummonUnit:
                    return new SummonUnitDeployableBehavior();

                default:
                    return null;
            }
        }

        private void EnsurePresentation()
        {
            if (_presentationView == null)
                _presentationView = GetComponent<DeployablePresentationView>();

            if (_presentationView == null)
                _presentationView = gameObject.AddComponent<DeployablePresentationView>();

            _presentationView.Build(this);
        }

        protected override void OnDisable()
        {
            _lastKnownPosition = Position;
            base.OnDisable();
            SimulationClock.Grid?.Remove(this, _lastKnownPosition);
            CombatRegistry.Unregister(this);
        }
    }
}
