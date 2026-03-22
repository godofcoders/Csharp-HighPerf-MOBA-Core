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

            State.SetPassiveLoadout(_definition.BuildDefaultPassiveLoadout(), false);

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
            State.TickPassives(currentTick);
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

        public void FireProjectile(
      Vector3 origin,
      Vector3 direction,
      float speed,
      float range,
      float damage,
      AbilityDefinition sourceAbility,
      AbilitySlotType slotType,
      bool isSuper,
      bool isGadget)
        {
            var projectileService = ServiceProvider.Get<IProjectileService>();

            var spawnContext = new ProjectileSpawnContext
            {
                Owner = this,
                SourceAbility = sourceAbility,
                SlotType = slotType,
                Origin = origin,
                Direction = direction,
                Speed = speed,
                Range = range,
                Damage = damage,
                Team = Team,
                SuperChargeOnHit = 0.20f,
                IsSuper = isSuper,
                IsGadget = isGadget
            };

            projectileService.FireProjectile(spawnContext);
        }

        private void ExecuteCommand(BufferedCommand cmd)
        {
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            switch (cmd.Type)
            {
                case InputCommandType.MainAttack:
                    if (_definition.MainAttack != null &&
                        State.IsAbilityReady(AbilityRuntimeSlot.MainAttack, currentTick) &&
                        State.Ammo.Consume(1))
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

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = AbilityEventType.CastStarted,
                            Source = this,
                            AbilityDefinition = _definition.MainAttack,
                            SlotType = AbilitySlotType.MainAttack,
                            Origin = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Tick = currentTick,
                            Result = default
                        });

                        CombatPresentationEventBus.Raise(new CombatPresentationEvent
                        {
                            EventType = CombatPresentationEventType.AbilityCastStarted,
                            Source = this,
                            Target = null,
                            AbilityDefinition = _definition.MainAttack,
                            SlotType = AbilitySlotType.MainAttack,
                            Position = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Value = 0f,
                            IsSuper = false
                        });

                        var result = _mainAttack != null
                            ? _mainAttack.Execute(this, executionContext)
                            : AbilityExecutionResult.Failed(_definition.MainAttack, AbilitySlotType.MainAttack);

                        if (result.Success)
                        {
                            State.StartAbilityCooldown(
                                AbilityRuntimeSlot.MainAttack,
                                currentTick,
                                _definition.MainAttack.Cooldown);
                        }

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = result.Success ? AbilityEventType.CastSucceeded : AbilityEventType.CastFailed,
                            Source = this,
                            AbilityDefinition = _definition.MainAttack,
                            SlotType = AbilitySlotType.MainAttack,
                            Origin = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Tick = currentTick,
                            Result = result
                        });

                        if (result.Success)
                        {
                            CombatPresentationEventBus.Raise(new CombatPresentationEvent
                            {
                                EventType = CombatPresentationEventType.AbilityCastSucceeded,
                                Source = this,
                                Target = null,
                                AbilityDefinition = _definition.MainAttack,
                                SlotType = AbilitySlotType.MainAttack,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                IsSuper = false
                            });
                        }
                    }
                    break;

                case InputCommandType.Gadget:
                    if (_definition.Gadget != null &&
                        State.RemainingGadgets > 0 &&
                        State.IsAbilityReady(AbilityRuntimeSlot.Gadget, currentTick))
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

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = AbilityEventType.CastStarted,
                            Source = this,
                            AbilityDefinition = _definition.Gadget,
                            SlotType = AbilitySlotType.Gadget,
                            Origin = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Tick = currentTick,
                            Result = default
                        });

                        CombatPresentationEventBus.Raise(new CombatPresentationEvent
                        {
                            EventType = CombatPresentationEventType.AbilityCastStarted,
                            Source = this,
                            Target = null,
                            AbilityDefinition = _definition.Gadget,
                            SlotType = AbilitySlotType.Gadget,
                            Position = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Value = 0f,
                            IsSuper = false
                        });

                        var result = _gadgetLogic != null
                            ? _gadgetLogic.Execute(this, executionContext)
                            : AbilityExecutionResult.Failed(_definition.Gadget, AbilitySlotType.Gadget);

                        if (result.Success)
                        {
                            State.UseGadgetCharge();
                            State.StartAbilityCooldown(
                                AbilityRuntimeSlot.Gadget,
                                currentTick,
                                _definition.Gadget.Cooldown);

                            Debug.Log($"[SIM] Gadget used! Remaining: {State.RemainingGadgets}");
                        }

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = result.Success ? AbilityEventType.CastSucceeded : AbilityEventType.CastFailed,
                            Source = this,
                            AbilityDefinition = _definition.Gadget,
                            SlotType = AbilitySlotType.Gadget,
                            Origin = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Tick = currentTick,
                            Result = result
                        });

                        if (result.Success)
                        {
                            CombatPresentationEventBus.Raise(new CombatPresentationEvent
                            {
                                EventType = CombatPresentationEventType.AbilityCastSucceeded,
                                Source = this,
                                Target = null,
                                AbilityDefinition = _definition.Gadget,
                                SlotType = AbilitySlotType.Gadget,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                IsSuper = false
                            });
                        }
                    }
                    break;

                case InputCommandType.Super:
                    if (_definition.SuperAbility != null &&
                        State.SuperCharge.IsReady &&
                        State.IsAbilityReady(AbilityRuntimeSlot.Super, currentTick))
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

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = AbilityEventType.CastStarted,
                            Source = this,
                            AbilityDefinition = _definition.SuperAbility,
                            SlotType = AbilitySlotType.Super,
                            Origin = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Tick = currentTick,
                            Result = default
                        });

                        CombatPresentationEventBus.Raise(new CombatPresentationEvent
                        {
                            EventType = CombatPresentationEventType.AbilityCastStarted,
                            Source = this,
                            Target = null,
                            AbilityDefinition = _definition.SuperAbility,
                            SlotType = AbilitySlotType.Super,
                            Position = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Value = 0f,
                            IsSuper = true
                        });

                        var result = _superAbility != null
                            ? _superAbility.Execute(this, executionContext)
                            : AbilityExecutionResult.Failed(_definition.SuperAbility, AbilitySlotType.Super);

                        if (result.Success)
                        {
                            State.TryConsumeSuper();
                            State.StartAbilityCooldown(
                                AbilityRuntimeSlot.Super,
                                currentTick,
                                _definition.SuperAbility.Cooldown);
                        }
                        else
                        {
                            if (State.ActionState.StateType == BrawlerActionStateType.CastingSuper)
                            {
                                State.ClearActionState();
                            }
                        }

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = result.Success ? AbilityEventType.CastSucceeded : AbilityEventType.CastFailed,
                            Source = this,
                            AbilityDefinition = _definition.SuperAbility,
                            SlotType = AbilitySlotType.Super,
                            Origin = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Tick = currentTick,
                            Result = result
                        });

                        if (result.Success)
                        {
                            CombatPresentationEventBus.Raise(new CombatPresentationEvent
                            {
                                EventType = CombatPresentationEventType.AbilityCastSucceeded,
                                Source = this,
                                Target = null,
                                AbilityDefinition = _definition.SuperAbility,
                                SlotType = AbilitySlotType.Super,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                IsSuper = true
                            });
                        }
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
            CombatRegistry.Register(this);
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