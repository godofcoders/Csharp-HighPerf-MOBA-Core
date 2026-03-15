using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;
using System;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerController : SimulationEntity, IAbilityUser, ISpatialEntity
    {
        [SerializeField] private BrawlerDefinition _definition;
        public Vector3 Position => transform.position;
        public float CollisionRadius => 0.5f; // Hardcoded or from Definition
        public int EntityID => gameObject.GetInstanceID();

        private Vector3 _lastTickPosition;

        public BrawlerState State { get; private set; }
        private IAbilityLogic _mainAttack;

        public Vector3 CurrentPosition => transform.position;
        private InputBuffer _inputBuffer = new InputBuffer();
        private Vector3 _currentMoveInput;

        [SerializeField] private TeamType _team;

        private IAbilityLogic _superAbility;
        private IAbilityLogic _gadgetLogic;
        [SerializeField] private GameObject _visualModel;
        public TeamType Team => _team;
        private bool _isInitialized; // The Guard
        // The Bridge calls this to set the "Intent"
        public void SetMoveInput(Vector3 direction)
        {
            Debug.Log("Move Input: " + direction);
            _currentMoveInput = direction;
        }

        // Called by the Input Bridge
        public void BufferAttack(InputCommandType type, Vector3 direction)
        {
            _inputBuffer.Enqueue(type, direction);
        }

        // This override now works because SimulationEntity.Awake is virtual
        protected override void Awake()
        {
            base.Awake(); // Call the base class Awake

            if (_definition == null)
            {
                Debug.LogError($"BrawlerDefinition missing on {gameObject.name}");
                return;
            }
            if (_definition != null && !_isInitialized)
            {
                InternalInitialize(_definition, _team);
            }

            State = new BrawlerState(_definition, _team);
            _mainAttack = _definition.MainAttack?.CreateLogic();
            _superAbility = _definition.SuperAbility?.CreateLogic();
            _definition.StarPower?.Apply(State);
            _gadgetLogic = _definition.Gadget?.CreateLogic();

        }

        public void InitializeFromMatchmaking(BrawlerDefinition def, TeamType team)
        {
            // Inject the data from the Matchmaking roster
            InternalInitialize(def, team);
        }

        private void InternalInitialize(BrawlerDefinition def, TeamType team)
        {
            if (_isInitialized) return; // Prevent double-initialization

            _definition = def;
            _team = team;

            // Initialize the Logic Layer
            State = new BrawlerState(_definition, _team);

            // Create Ability Logics
            _mainAttack = _definition.MainAttack?.CreateLogic();
            _superAbility = _definition.SuperAbility?.CreateLogic();
            _gadgetLogic = _definition.Gadget?.CreateLogic();

            // Apply Passives
            _definition.StarPower?.Apply(State);
            _lastTickPosition = transform.position;

            _isInitialized = true;

            SimulationClock.Grid?.Add(this);
            State.OnDeath += HandleDeath;
            Debug.Log($"[SIM] {gameObject.name} initialized as {def.BrawlerName} on Team {team}");
        }

        private void HandleDeath()
        {
            // If a Blue player dies, Red gets a point
            // 1. Report score to MatchManager
            TeamType enemyTeam = (_team == TeamType.Blue) ? TeamType.Red : TeamType.Blue;
            MatchManager.Instance.AddScore(enemyTeam, 1);

            // 2. Hide the View (Character Model/HUD)
            // In a real project, trigger a "Death" animation here
            gameObject.SetActive(false);

            // 3. Remove from Spatial Grid so projectiles don't hit a ghost
            SimulationClock.Grid?.Remove(this, transform.position);

            // 4. Ask SpawnManager for a respawn
            SpawnManager.Instance.RequestRespawn(this, _team);
        }

        public override void Tick(uint currentTick)
        {
            if (!_isInitialized || State.IsDead || State == null) return;

            if (MatchManager.Instance.CurrentState != MatchState.Active)
            {
                // We still tick things like reloading, but we don't process movement/input
                State.UpdateResources(SimulationClock.TickDeltaTime);
                return;
            }

            State.TickEffects(currentTick);

            // 2. Resources & Movement
            State.UpdateResources(SimulationClock.TickDeltaTime);

            if (!State.IsStunned)
                ProcessMovement();

            State.Hypercharge.Tick(currentTick, () =>
           {
               State.MoveSpeed.RemoveModifiersFromSource(State.Hypercharge);
               Debug.Log("[SIM] Hypercharge Ended");
           });

            if (_inputBuffer.HasPending && !State.IsStunned && CanPerformAction())
            {
                var cmd = _inputBuffer.Consume();
                // Consume 1 ammo bar per attack
                if (State.Ammo.Consume(1))
                {
                    ExecuteCommand(cmd);
                }
            }

            // 2. Spatial Grid Update
            SimulationClock.Grid?.UpdateEntity(this, _lastTickPosition, transform.position);
            _lastTickPosition = transform.position;

            // 3. Ability Logic
            _mainAttack?.Tick(currentTick);
            UpdateVisualStealth();
        }
        private bool CanPerformAction()
        {
            // Now we check if we actually have at least 1 bar of ammo
            return State.Ammo.AvailableBars >= 1;
        }

        private void ProcessMovement()
        {
            // 1. Movement Logic
            if (_currentMoveInput.sqrMagnitude > 0.01f)
            {
                float speed = State.MoveSpeed.Value;
                float tickDelta = 1f / 30f; // Standard 30Hz tick

                Vector3 movement = _currentMoveInput.normalized * (speed * tickDelta);
                transform.position += movement;

                // Rotate towards movement
                if (movement != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(movement);
            }
        }

        public void TakeDamage(float amount) => State.TakeDamage(amount);

        protected override void OnDisable()
        {
            base.OnDisable();
            SimulationClock.Grid?.Remove(this, transform.position);
        }

        public void FireProjectile(Vector3 origin, Vector3 direction, float speed, float range, float damage)
        {
            // 1. Fetch the Projectile Service from the registry
            var projectileService = ServiceProvider.Get<IProjectileService>();

            // 2. Delegate the firing logic to the manager
            // We pass 'this.Team' to the service so the manager knows which team to ignore (No Friendly Fire)
            projectileService.FireProjectile(
                origin,
                direction,
                speed,
                range,
                damage,
                this.Team
            );
        }

        private void ExecuteCommand(BufferedCommand cmd)
        {
            // Context for the logic POCOs
            var context = new AbilityContext
            {
                Origin = transform.position,
                Direction = cmd.Direction,
                StartTick = ServiceProvider.Get<ISimulationClock>().CurrentTick
            };

            switch (cmd.Type)
            {
                case InputCommandType.MainAttack:
                    if (State.Ammo.Consume(1))
                        _mainAttack?.Execute(this, context);
                    break;

                case InputCommandType.Gadget:
                    // High-End Logic: Check if we have charges left
                    if (State.RemainingGadgets > 0)
                    {
                        _gadgetLogic?.Execute(this, context);
                        State.UseGadgetCharge(); // Decrement the POCO state
                        Debug.Log($"[SIM] Gadget used! Remaining: {State.RemainingGadgets}");
                    }
                    break;

                case InputCommandType.Super:
                    _superAbility?.Execute(this, context);
                    break;
            }
        }

        public void ActivateHypercharge()
        {
            var def = _definition.Hypercharge;
            if (def == null) return;

            // Hypercharge doesn't go through the buffer; it's an instant state change
            if (State.Hypercharge.ChargePercent >= 1.0f)
            {
                State.Hypercharge.Activate(ServiceProvider.Get<ISimulationClock>().CurrentTick);

                // Apply the "Pioneer" modifiers
                var speedMod = new StatModifier(0.25f, ModifierType.Multiplicative, State.Hypercharge);
                var dmgMod = new StatModifier(0.15f, ModifierType.Multiplicative, State.Hypercharge);

                State.MoveSpeed.AddModifier(speedMod);
                State.Damage.AddModifier(dmgMod);

                Debug.Log("[SIM] Hypercharge Activated! Brawler is now buffed.");
            }
        }

        // Called by SpawnManager after the delay
        public void Respawn(Vector3 position)
        {
            // 1. Reset Position and State
            transform.position = position;
            _lastTickPosition = position;

            // We need a Reset method in BrawlerState to refill HP/Ammo
            State.Reset();

            // 2. Show the View again
            gameObject.SetActive(true);

            // 3. Re-register with the Grid
            SimulationClock.Grid?.Add(this);
        }

        private void UpdateVisualStealth()
        {
            // Simplified: Local player always sees themselves, but enemies fade out
            // You would check: Is this brawler hidden from the LOCAL player's team?
            bool hidden = State.IsHiddenTo(TeamType.Red); // Example check
            _visualModel.SetActive(!hidden);
        }
    }
}