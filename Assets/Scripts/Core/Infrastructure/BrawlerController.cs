using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;
using System;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerController : SimulationEntity, IAbilityUser, ISpatialEntity
    {
        [SerializeField] private BrawlerDefinition _definition;
        [SerializeField] private TeamType _team;
        [SerializeField] private GameObject _visualModel;

        private Vector3 _lastTickPosition;
        private readonly InputBuffer _inputBuffer = new InputBuffer();
        private Vector3 _currentMoveInput;

        private IAbilityLogic _mainAttack;
        private IAbilityLogic _superAbility;
        private IAbilityLogic _gadgetLogic;

        private bool _isInitialized;

        public BrawlerDefinition Definition => _definition;
        public BrawlerState State { get; private set; }

        public TeamType Team => _team;
        public Vector3 Position => transform.position;
        public Vector3 CurrentPosition => transform.position;
        public float CollisionRadius => 0.5f;
        public int EntityID => gameObject.GetInstanceID();

        public void SetMoveInput(Vector3 direction)
        {
            _currentMoveInput = direction;
        }

        public void BufferAttack(InputCommandType type, Vector3 direction)
        {
            _inputBuffer.Enqueue(type, direction);
        }

        protected override void Awake()
        {
            base.Awake();

            if (_definition == null)
            {
                Debug.LogError($"BrawlerDefinition missing on {gameObject.name}");
                return;
            }

            if (!_isInitialized)
            {
                InternalInitialize(_definition, _team);
            }
        }

        public void InitializeFromMatchmaking(BrawlerDefinition def, TeamType team)
        {
            InternalInitialize(def, team);
        }

        private void InternalInitialize(BrawlerDefinition def, TeamType team)
        {
            if (_isInitialized)
                return;

            if (def == null)
            {
                Debug.LogError($"Initialize failed on {gameObject.name}: BrawlerDefinition is null.");
                return;
            }

            _definition = def;
            _team = team;

            State = new BrawlerState(_definition, _team);

            State.Owner = this;
            _mainAttack = _definition.MainAttack?.CreateLogic();
            _superAbility = _definition.SuperAbility?.CreateLogic();
            _gadgetLogic = _definition.Gadget?.CreateLogic();

            _definition.StarPower?.Apply(State);

            _lastTickPosition = transform.position;
            _isInitialized = true;
            CombatRegistry.Register(this);
            SimulationClock.Grid?.Add(this);
            State.OnDeath += HandleDeath;

            Debug.Log($"[SIM] {gameObject.name} initialized as {_definition.BrawlerName} on Team {_team}");
        }

        private void HandleDeath()
        {
            TeamType enemyTeam = (_team == TeamType.Blue) ? TeamType.Red : TeamType.Blue;
            MatchManager.Instance.AddScore(enemyTeam, 1);

            gameObject.SetActive(false);
            SimulationClock.Grid?.Remove(this, transform.position);

            SpawnManager.Instance.RequestRespawn(this, _team);
        }

        public override void Tick(uint currentTick)
        {
            if (!_isInitialized || State == null || State.IsDead)
                return;

            if (MatchManager.Instance.CurrentState != MatchState.Active)
            {
                State.UpdateResources(SimulationClock.TickDeltaTime);
                return;
            }

            State.TickEffects(currentTick);
            State.UpdateActionState(currentTick);
            State.UpdateResources(SimulationClock.TickDeltaTime);

            if (State.CanMove(currentTick))
                ProcessMovement();
            else
                SetMoveInput(Vector3.zero);

            State.Hypercharge.Tick(currentTick, () =>
            {
                State.MoveSpeed.RemoveModifiersFromSource(State.Hypercharge);
                Debug.Log("[SIM] Hypercharge Ended");
            });

            if (_inputBuffer.HasPending && State.CanUseActionInput(currentTick) && CanPerformAction())
            {
                var cmd = _inputBuffer.Consume();
                ExecuteCommand(cmd);
            }

            SimulationClock.Grid?.UpdateEntity(this, _lastTickPosition, transform.position);
            _lastTickPosition = transform.position;

            _mainAttack?.Tick(currentTick);
            _superAbility?.Tick(currentTick);
            _gadgetLogic?.Tick(currentTick);

            UpdateVisualStealth();
        }

        private bool CanPerformAction()
        {
            return State.Ammo.AvailableBars >= 1;
        }

        private void ProcessMovement()
        {
            if (_currentMoveInput.sqrMagnitude <= 0.01f)
                return;

            float speed = State.IncomingMovementModifiers.Apply(State.MoveSpeed.Value);
            float tickDelta = 1f / 30f;

            Vector3 movement = _currentMoveInput.normalized * (speed * tickDelta);
            transform.position += movement;

            if (movement != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(movement);
        }

        public void TakeDamage(float amount)
        {
            State?.TakeDamage(amount);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SimulationClock.Grid?.Remove(this, transform.position);
            CombatRegistry.Unregister(this);
        }

        public void FireProjectile(Vector3 origin, Vector3 direction, float speed, float range, float damage)
        {
            var projectileService = ServiceProvider.Get<IProjectileService>();

            projectileService.FireProjectile(
                this,
                origin,
                direction,
                speed,
                range,
                damage,
                Team,
                0.20f
            );
        }

        private void ExecuteCommand(BufferedCommand cmd)
        {
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            switch (cmd.Type)
            {
                case InputCommandType.MainAttack:
                    if (_definition.MainAttack != null && State.Ammo.Consume(1))
                    {
                        State.EnterActionState(
                            BrawlerActionStateType.CastingMainAttack,
                            currentTick,
                            _definition.MainAttack.GetCastDurationTicks(),
                            _definition.MainAttack.AllowMovementDuringCast,
                            _definition.MainAttack.AllowActionInputDuringCast,
                            _definition.MainAttack.IsInterruptible);

                        var executionContext = new AbilityExecutionContext
                        {
                            Source = this,
                            AbilityDefinition = _definition.MainAttack,
                            SlotType = AbilitySlotType.MainAttack,
                            Origin = transform.position,
                            Direction = cmd.Direction,
                            StartTick = currentTick,
                            IsSuper = false,
                            IsGadget = false
                        };

                        State.LastAttackTick = currentTick;
                        _mainAttack?.Execute(this, executionContext);
                    }
                    break;

                case InputCommandType.Gadget:
                    if (_definition.Gadget != null && State.RemainingGadgets > 0)
                    {
                        State.EnterActionState(
                            BrawlerActionStateType.CastingGadget,
                            currentTick,
                            _definition.Gadget.GetCastDurationTicks(),
                            _definition.Gadget.AllowMovementDuringCast,
                            _definition.Gadget.AllowActionInputDuringCast,
                            _definition.Gadget.IsInterruptible);

                        var executionContext = new AbilityExecutionContext
                        {
                            Source = this,
                            AbilityDefinition = _definition.Gadget,
                            SlotType = AbilitySlotType.Gadget,
                            Origin = transform.position,
                            Direction = cmd.Direction,
                            StartTick = currentTick,
                            IsSuper = false,
                            IsGadget = true
                        };

                        _gadgetLogic?.Execute(this, executionContext);
                        State.UseGadgetCharge();
                        Debug.Log($"[SIM] Gadget used! Remaining: {State.RemainingGadgets}");
                    }
                    break;

                case InputCommandType.Super:
                    if (_definition.SuperAbility != null && State.TryConsumeSuper())
                    {
                        State.EnterActionState(
                            BrawlerActionStateType.CastingSuper,
                            currentTick,
                            _definition.SuperAbility.GetCastDurationTicks(),
                            _definition.SuperAbility.AllowMovementDuringCast,
                            _definition.SuperAbility.AllowActionInputDuringCast,
                            _definition.SuperAbility.IsInterruptible);

                        var executionContext = new AbilityExecutionContext
                        {
                            Source = this,
                            AbilityDefinition = _definition.SuperAbility,
                            SlotType = AbilitySlotType.Super,
                            Origin = transform.position,
                            Direction = cmd.Direction,
                            StartTick = currentTick,
                            IsSuper = true,
                            IsGadget = false
                        };

                        State.LastAttackTick = currentTick;
                        _superAbility?.Execute(this, executionContext);
                    }
                    break;
            }
        }
        public void ActivateHypercharge()
        {
            var def = _definition.Hypercharge;
            if (def == null)
                return;

            if (State.Hypercharge.ChargePercent >= 1.0f)
            {
                State.Hypercharge.Activate(ServiceProvider.Get<ISimulationClock>().CurrentTick);

                var speedMod = new StatModifier(0.25f, ModifierType.Multiplicative, State.Hypercharge);
                var dmgMod = new StatModifier(0.15f, ModifierType.Multiplicative, State.Hypercharge);

                State.MoveSpeed.AddModifier(speedMod);
                State.Damage.AddModifier(dmgMod);

                Debug.Log("[SIM] Hypercharge Activated! Brawler is now buffed.");
            }
        }

        public void Respawn(Vector3 position)
        {
            transform.position = position;
            _lastTickPosition = position;

            State.Reset();

            gameObject.SetActive(true);
            SimulationClock.Grid?.Add(this);
        }

        private void UpdateVisualStealth()
        {
            if (_visualModel == null || State == null)
                return;

            bool hidden = State.IsHiddenTo(TeamType.Red);
            _visualModel.SetActive(!hidden);
        }

        public void GrantSuperCharge(float amount)
        {
            State?.AddSuperCharge(amount);
        }
    }
}