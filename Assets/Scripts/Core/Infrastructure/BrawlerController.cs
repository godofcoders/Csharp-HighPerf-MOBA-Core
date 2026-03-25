using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;

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

        private readonly List<GadgetDefinition> _equippedGadgets = new List<GadgetDefinition>(2);
        private HyperchargeDefinition _equippedHypercharge;
        private BrawlerBuildDefinition _resolvedBuildSource;

        public BrawlerDefinition Definition => _definition;
        public BrawlerState State { get; private set; }

        public TeamType Team => _team;
        public Vector3 Position => transform.position;
        public Vector3 CurrentPosition => transform.position;
        public float CollisionRadius => 0.5f;
        public int EntityID => gameObject.GetInstanceID();

        protected override void Awake()
        {
            base.Awake();

            if (_definition == null)
            {
                Debug.LogError($"BrawlerDefinition missing on {gameObject.name}");
                return;
            }

            if (!_isInitialized)
                InternalInitialize(_definition, _team);
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

            BrawlerBuildDefinition buildToUse = GetBuildToUse();
            if (buildToUse != null)
            {
                if (BrawlerBuildResolver.TryResolveUnlockedOnly(_definition, buildToUse, State.CurrentPowerLevel, out ResolvedBrawlerBuild resolved, out string error))
                {
                    _resolvedBuildSource = buildToUse;
                    ApplyResolvedBuild(resolved);
                }
                else
                {
                    Debug.LogWarning($"[Build] Failed to resolve build '{buildToUse.name}' for '{_definition.name}': {error}");
                    _resolvedBuildSource = null;
                    ApplyLegacyFallbackBuild();
                }
            }
            else
            {
                _resolvedBuildSource = null;
                ApplyLegacyFallbackBuild();
            }

            _lastTickPosition = transform.position;
            _isInitialized = true;
            CombatRegistry.Register(this);
            SimulationClock.Grid?.Add(this);
            State.OnDeath += HandleDeath;

            Debug.Log($"[SIM] {gameObject.name} initialized as {_definition.BrawlerName} on Team {_team}");
        }

        private void ApplyLegacyFallbackBuild()
        {
            _equippedGadgets.Clear();

            if (_definition.Gadget != null)
                _equippedGadgets.Add(_definition.Gadget);

            _gadgetLogic = _definition.Gadget?.CreateLogic();

            _equippedHypercharge = _definition.Hypercharge;
            State.SetEquippedHypercharge(_equippedHypercharge);

            List<PassiveDefinition> fallbackPassives = _definition.BuildDefaultPassiveLoadout();
            State.SetPassiveLoadout(fallbackPassives, false);

            StarPowerDefinition equippedStarPower = null;
            List<GearDefinition> equippedGears = new List<GearDefinition>(2);

            for (int i = 0; i < fallbackPassives.Count; i++)
            {
                PassiveDefinition passive = fallbackPassives[i];
                if (passive == null)
                    continue;

                if (passive is StarPowerDefinition starPower)
                {
                    equippedStarPower = starPower;
                }
                else if (passive is GearDefinition gear)
                {
                    if (!equippedGears.Contains(gear))
                        equippedGears.Add(gear);
                }
            }

            if (State.RuntimeBuild != null)
            {
                State.RuntimeBuild.Clear();
                State.RefreshRuntimeBuildUnlockState();
                State.RuntimeBuild.SetEquippedGadget(GetActiveGadgetDefinition());
                State.RuntimeBuild.SetEquippedStarPower(equippedStarPower);
                State.RuntimeBuild.SetEquippedHypercharge(_equippedHypercharge);
                State.RuntimeBuild.SetEquippedGears(equippedGears);
            }
        }

        private BrawlerBuildDefinition GetBuildToUse()
        {
            if (_definition == null || State == null)
                return null;

            return _definition.GetUsableDefaultBuild(State.CurrentPowerLevel);
        }

        private void ApplyResolvedBuild(ResolvedBrawlerBuild resolved)
        {
            _equippedGadgets.Clear();
            _equippedHypercharge = null;

            if (resolved == null)
            {
                State.SetPassiveLoadout(null, false);
                State.SetEquippedHypercharge(null);
                State.RuntimeBuild?.Clear();
                State.RefreshRuntimeBuildUnlockState();
                _gadgetLogic = _definition.Gadget?.CreateLogic();
                return;
            }

            for (int i = 0; i < resolved.Gadgets.Count; i++)
            {
                GadgetDefinition gadget = resolved.Gadgets[i];
                if (gadget != null && !_equippedGadgets.Contains(gadget))
                    _equippedGadgets.Add(gadget);
            }

            _equippedHypercharge = resolved.Hypercharge;
            State.SetEquippedHypercharge(_equippedHypercharge);

            GadgetDefinition activeGadget = GetActiveGadgetDefinition();
            _gadgetLogic = activeGadget?.CreateLogic();

            State.SetPassiveLoadout(resolved.PassiveOptions, false);

            StarPowerDefinition equippedStarPower = null;
            List<GearDefinition> equippedGears = new List<GearDefinition>(2);

            for (int i = 0; i < resolved.PassiveOptions.Count; i++)
            {
                PassiveDefinition passive = resolved.PassiveOptions[i];
                if (passive == null)
                    continue;

                if (passive is StarPowerDefinition starPower)
                {
                    equippedStarPower = starPower;
                }
                else if (passive is GearDefinition gear)
                {
                    if (!equippedGears.Contains(gear))
                        equippedGears.Add(gear);
                }
            }

            if (State.RuntimeBuild != null)
            {
                State.RuntimeBuild.Clear();
                State.RefreshRuntimeBuildUnlockState();
                State.RuntimeBuild.SetEquippedGadget(activeGadget);
                State.RuntimeBuild.SetEquippedStarPower(equippedStarPower);
                State.RuntimeBuild.SetEquippedHypercharge(_equippedHypercharge);
                State.RuntimeBuild.SetEquippedGears(equippedGears);
            }
        }

        private GadgetDefinition GetActiveGadgetDefinition()
        {
            if (_equippedGadgets.Count > 0 && _equippedGadgets[0] != null)
                return _equippedGadgets[0];

            return _definition != null ? _definition.Gadget : null;
        }

        private IAbilityLogic GetCurrentSuperLogic()
        {
            AbilityDefinition currentSuperDef = State?.GetCurrentSuperDefinition();
            if (currentSuperDef == null)
                return null;

            if (_definition != null && currentSuperDef == _definition.SuperAbility)
                return _superAbility;

            return currentSuperDef.CreateLogic();
        }

        public void SetMoveInput(Vector3 direction)
        {
            _currentMoveInput = direction;
        }

        public void BufferAttack(InputCommandType type, Vector3 direction)
        {
            _inputBuffer.Enqueue(type, direction);
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
       State.ClearHyperchargeRuntimeModifiers();

       BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
       {
           EventType = BrawlerPresentationEventType.HyperchargeEnded,
           Source = this,
           AbilityDefinition = State.GetCurrentSuperDefinition(),
           Position = transform.position,
           Direction = transform.forward,
           Value = 0f,
           Tick = currentTick
       });

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
                    {
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
                            BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                            {
                                EventType = BrawlerPresentationEventType.MainAttackStarted,
                                Source = this,
                                AbilityDefinition = _definition.MainAttack,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                Tick = currentTick
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

                                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                                {
                                    EventType = BrawlerPresentationEventType.MainAttackSucceeded,
                                    Source = this,
                                    AbilityDefinition = _definition.MainAttack,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    Tick = currentTick
                                });
                            }
                            else
                            {
                                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                                {
                                    EventType = BrawlerPresentationEventType.MainAttackFailed,
                                    Source = this,
                                    AbilityDefinition = _definition.MainAttack,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    Tick = currentTick
                                });
                            }
                        }

                        break;
                    }

                case InputCommandType.Gadget:
                    {
                        GadgetDefinition currentGadgetDef = GetActiveGadgetDefinition();

                        if (currentGadgetDef != null &&
                            State.RemainingGadgets > 0 &&
                            State.IsAbilityReady(AbilityRuntimeSlot.Gadget, currentTick))
                        {
                            State.EnterActionState(
                                BrawlerActionStateType.CastingGadget,
                                currentTick,
                                currentGadgetDef.GetCastDurationTicks(),
                                currentGadgetDef.AllowMovementDuringCast,
                                currentGadgetDef.AllowActionInputDuringCast,
                                currentGadgetDef.IsInterruptible);

                            var executionContext = new AbilityExecutionContext
                            {
                                Source = this,
                                AbilityDefinition = currentGadgetDef,
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
                                AbilityDefinition = currentGadgetDef,
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
                                AbilityDefinition = currentGadgetDef,
                                SlotType = AbilitySlotType.Gadget,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                IsSuper = false
                            });

                            BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                            {
                                EventType = BrawlerPresentationEventType.GadgetStarted,
                                Source = this,
                                AbilityDefinition = currentGadgetDef,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                Tick = currentTick
                            });

                            var result = _gadgetLogic != null
                                ? _gadgetLogic.Execute(this, executionContext)
                                : AbilityExecutionResult.Failed(currentGadgetDef, AbilitySlotType.Gadget);

                            if (result.Success)
                            {
                                State.UseGadgetCharge();
                                State.StartAbilityCooldown(
                                    AbilityRuntimeSlot.Gadget,
                                    currentTick,
                                    currentGadgetDef.Cooldown);

                                Debug.Log($"[SIM] Gadget used! Remaining: {State.RemainingGadgets}");
                            }

                            AbilityEventBus.Raise(new AbilityExecutionEvent
                            {
                                EventType = result.Success ? AbilityEventType.CastSucceeded : AbilityEventType.CastFailed,
                                Source = this,
                                AbilityDefinition = currentGadgetDef,
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
                                    AbilityDefinition = currentGadgetDef,
                                    SlotType = AbilitySlotType.Gadget,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    IsSuper = false
                                });

                                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                                {
                                    EventType = BrawlerPresentationEventType.GadgetSucceeded,
                                    Source = this,
                                    AbilityDefinition = currentGadgetDef,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    Tick = currentTick
                                });
                            }
                            else
                            {
                                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                                {
                                    EventType = BrawlerPresentationEventType.GadgetFailed,
                                    Source = this,
                                    AbilityDefinition = currentGadgetDef,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    Tick = currentTick
                                });
                            }
                        }

                        break;
                    }

                case InputCommandType.Super:
                    {
                        AbilityDefinition currentSuperDef = State.GetCurrentSuperDefinition();

                        if (currentSuperDef != null &&
                            State.SuperCharge.IsReady &&
                            State.IsAbilityReady(AbilityRuntimeSlot.Super, currentTick))
                        {
                            State.EnterActionState(
                                BrawlerActionStateType.CastingSuper,
                                currentTick,
                                currentSuperDef.GetCastDurationTicks(),
                                currentSuperDef.AllowMovementDuringCast,
                                currentSuperDef.AllowActionInputDuringCast,
                                currentSuperDef.IsInterruptible);

                            var executionContext = new AbilityExecutionContext
                            {
                                Source = this,
                                AbilityDefinition = currentSuperDef,
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
                                AbilityDefinition = currentSuperDef,
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
                                AbilityDefinition = currentSuperDef,
                                SlotType = AbilitySlotType.Super,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                IsSuper = true
                            });

                            BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                            {
                                EventType = BrawlerPresentationEventType.SuperStarted,
                                Source = this,
                                AbilityDefinition = currentSuperDef,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                Tick = currentTick
                            });

                            IAbilityLogic currentSuperLogic = GetCurrentSuperLogic();

                            var result = currentSuperLogic != null
                                ? currentSuperLogic.Execute(this, executionContext)
                                : AbilityExecutionResult.Failed(currentSuperDef, AbilitySlotType.Super);

                            if (result.Success)
                            {
                                State.TryConsumeSuper();
                                State.StartAbilityCooldown(
                                    AbilityRuntimeSlot.Super,
                                    currentTick,
                                    currentSuperDef.Cooldown);
                            }
                            else
                            {
                                if (State.ActionState.StateType == BrawlerActionStateType.CastingSuper)
                                    State.ClearActionState();
                            }

                            AbilityEventBus.Raise(new AbilityExecutionEvent
                            {
                                EventType = result.Success ? AbilityEventType.CastSucceeded : AbilityEventType.CastFailed,
                                Source = this,
                                AbilityDefinition = currentSuperDef,
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
                                    AbilityDefinition = currentSuperDef,
                                    SlotType = AbilitySlotType.Super,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    IsSuper = true
                                });
                                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                                {
                                    EventType = BrawlerPresentationEventType.SuperSucceeded,
                                    Source = this,
                                    AbilityDefinition = currentSuperDef,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    Tick = currentTick
                                });
                            }
                            else
                            {
                                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                                {
                                    EventType = BrawlerPresentationEventType.SuperFailed,
                                    Source = this,
                                    AbilityDefinition = currentSuperDef,
                                    Position = executionContext.Origin,
                                    Direction = executionContext.Direction,
                                    Value = 0f,
                                    Tick = currentTick
                                });
                            }
                        }

                        break;
                    }
            }
        }

        public void ActivateHypercharge()
        {
            HyperchargeDefinition def = State.EquippedHypercharge ?? _definition.Hypercharge;
            if (def == null)
                return;

            if (State.Hypercharge.ChargePercent < 1.0f)
                return;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            State.ClearHyperchargeRuntimeModifiers();
            State.Hypercharge.Activate(currentTick, def.DurationSeconds);

            if (def.SpeedBuff != 0f)
            {
                var speedMod = new StatModifier(
                    def.SpeedBuff,
                    ModifierType.Multiplicative,
                    State.HyperchargeModifierSource);

                State.MoveSpeed.AddModifier(speedMod);
            }

            if (def.DamageBuff != 0f)
            {
                var damageMod = new StatModifier(
                    def.DamageBuff,
                    ModifierType.Multiplicative,
                    State.HyperchargeModifierSource);

                State.Damage.AddModifier(damageMod);
            }

            if (def.ShieldBuff != 0f)
            {
                var reductionMod = new DamageModifier(
                    DamageModifierType.PercentReduction,
                    def.ShieldBuff,
                    State.HyperchargeModifierSource);

                State.AddIncomingDamageModifier(reductionMod);
            }

            BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
            {
                EventType = BrawlerPresentationEventType.HyperchargeStarted,
                Source = this,
                AbilityDefinition = def.EnhancedSuper != null ? def.EnhancedSuper : _definition.SuperAbility,
                Position = transform.position,
                Direction = transform.forward,
                Value = 0f,
                Tick = currentTick
            });

            Debug.Log($"[SIM] Hypercharge Activated! {def.name} is now active.");
        }

        private void HandleDeath()
        {
            TeamType enemyTeam = (_team == TeamType.Blue) ? TeamType.Red : TeamType.Blue;
            MatchManager.Instance.AddScore(enemyTeam, 1);

            gameObject.SetActive(false);
            SimulationClock.Grid?.Remove(this, transform.position);

            SpawnManager.Instance.RequestRespawn(this, _team);
        }

        public void Respawn(Vector3 position)
        {
            transform.position = position;
            _lastTickPosition = position;

            State.Reset();
            State.SetEquippedHypercharge(_equippedHypercharge ?? _definition.Hypercharge);

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