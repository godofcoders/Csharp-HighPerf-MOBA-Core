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
        public DeployableState State => _state;

        public DeployableDefinition Definition => _definition;
        public BrawlerController Owner => _owner;
        public TeamType Team => _team;
        public Vector3 Position => transform.position;
        public Vector3 CurrentPosition => transform.position;
        public float CollisionRadius => 0.5f;
        public int EntityID => gameObject.GetInstanceID();
        public bool IsExpired(uint currentTick) => currentTick >= _expiryTick;
        public bool IsDead => _currentHealth <= 0f;

        private DeployableAbilityUser _abilityUser;
        private IAbilityLogic _abilityLogic;

        public DeployableAbilityUser AbilityUser => _abilityUser;
        public IAbilityLogic AbilityLogic => _abilityLogic;

        public void Initialize(DeployableSpawnRequest request)
        {
            _definition = request.Definition;
            _owner = request.Owner;
            _team = request.Team;
            _spawnTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            _expiryTick = _spawnTick + (uint)(_definition.LifetimeSeconds * 30f);
            _currentHealth = _definition.MaxHealth;
            _abilityUser = new DeployableAbilityUser(this);
            _abilityLogic = _definition.AbilityDefinition != null
                ? _definition.AbilityDefinition.CreateLogic()
                : null;
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            _state = new DeployableState(_definition, _owner, _team, currentTick);

            transform.position = request.Position;

            _behavior = CreateBehavior(_definition.DeployableType);
            _behavior?.Initialize(this);

            CombatRegistry.Register(this);
            SimulationClock.Grid?.Add(this);
        }

        public override void Tick(uint currentTick)
        {
            if (_definition == null)
                return;

            if (_state == null)
                return;

            if (_state.IsDead || _state.IsExpired(currentTick))
            {
                Despawn();
                return;
            }

            _behavior?.Tick(currentTick);
        }

        public void TakeDamage(float amount)
        {
            if (_state == null)
                return;

            _state.TakeDamage(amount);

            if (_state.IsDead)
                Despawn();
        }

        public void Despawn()
        {
            IDeployableRegistry registry = ServiceProvider.Get<IDeployableRegistry>();
            registry?.Unregister(this);

            SimulationClock.Grid?.Remove(this, transform.position);
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

        protected override void OnDisable()
        {
            base.OnDisable();
            SimulationClock.Grid?.Remove(this, transform.position);
            CombatRegistry.Unregister(this);
        }
    }
}