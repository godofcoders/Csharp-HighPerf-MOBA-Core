using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;
using System.Collections;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerController : SimulationEntity, IAbilityUser, ISpatialEntity
    {
        [SerializeField] private BrawlerDefinition _definition;
        [SerializeField] private TeamType _team;
        [SerializeField] private GameObject _visualModel;

        [SerializeField] private Transform _visualRoot;
        [SerializeField] private BrawlerPresentationAnchors _presentationAnchors;

        private GameObject _spawnedVisualInstance;

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

        private readonly List<BrawlerCommand> _commandBuffer = new List<BrawlerCommand>(8);
        private IBrawlerCommandSource _commandSource;

        private readonly BrawlerDebugSnapshot _debugSnapshot = new BrawlerDebugSnapshot();

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

            // Generic shell flow:
            // definition may be injected later by InitializeFromMatchmaking.
            if (_definition != null && !_isInitialized)
            {
                InternalInitialize(_definition, _team);
            }
        }

        public void SetCommandSource(IBrawlerCommandSource source)
        {
            _commandSource = source;
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

            BuildVisualFromDefinition();

            State = new BrawlerState(_definition, _team);
            State.Owner = this;

            _mainAttack = _definition.MainAttack?.CreateLogic();
            _superAbility = _definition.SuperAbility?.CreateLogic();

            State.RuntimeKit.SetMainAttack(_definition.MainAttack, _mainAttack);
            State.RuntimeKit.SetSuper(_definition.SuperAbility, _superAbility);

            BrawlerBuildDefinition buildToUse = GetBuildToUse();
            if (buildToUse != null)
            {
                if (BrawlerBuildResolver.TryResolveUnlockedOnly(_definition, buildToUse, State.CurrentPowerLevel, out ResolvedBrawlerBuild resolved, out string error))
                {
                    _resolvedBuildSource = buildToUse;
                    ApplyResolvedBuild(resolved);
                    State.RefreshGadgetChargesFromRuntimeKit();
                }
                else
                {
                    Debug.LogWarning($"[Build] Failed to resolve build '{buildToUse.name}' for '{_definition.name}': {error}");
                    _resolvedBuildSource = null;
                    ApplyLegacyFallbackBuild();
                    State.RefreshGadgetChargesFromRuntimeKit();
                }
            }
            else
            {
                _resolvedBuildSource = null;
                ApplyLegacyFallbackBuild();
                State.RefreshGadgetChargesFromRuntimeKit();
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

            State.RuntimeKit.SetMainAttack(_definition.MainAttack, _mainAttack);
            State.RuntimeKit.SetSuper(_definition.SuperAbility, _superAbility);
            State.RuntimeKit.SetGadget(_definition.Gadget, _gadgetLogic);
            State.RuntimeKit.SetHypercharge(_equippedHypercharge);

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

            State.RuntimeKit.SetMainAttack(_definition.MainAttack, _mainAttack);
            State.RuntimeKit.SetSuper(_definition.SuperAbility, _superAbility);
            State.RuntimeKit.SetGadget(activeGadget, _gadgetLogic);
            State.RuntimeKit.SetHypercharge(_equippedHypercharge);

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
            if (State?.RuntimeKit?.GadgetDefinition != null)
                return State.RuntimeKit.GadgetDefinition;

            if (_equippedGadgets.Count > 0 && _equippedGadgets[0] != null)
                return _equippedGadgets[0];

            return _definition != null ? _definition.Gadget : null;
        }

        private IAbilityLogic GetCurrentSuperLogic()
        {
            AbilityDefinition currentSuperDef = State?.GetCurrentSuperDefinition();
            if (currentSuperDef == null)
                return null;

            AbilityDefinition baseSuperDef = State?.RuntimeKit?.SuperDefinition ?? _definition?.SuperAbility;
            IAbilityLogic baseSuperLogic = State?.RuntimeKit?.SuperLogic ?? _superAbility;

            if (currentSuperDef == baseSuperDef)
                return baseSuperLogic;

            return currentSuperDef.CreateLogic();
        }

        private void SetMoveInput(Vector3 direction)
        {
            _currentMoveInput = direction;
        }

        private void BufferAttack(InputCommandType type, Vector3 direction, Vector3 targetPoint, bool hasTargetPoint)
        {
            _inputBuffer.Enqueue(type, direction, targetPoint, hasTargetPoint);
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

            _commandBuffer.Clear();
            _commandSource?.CollectCommands(_commandBuffer, currentTick);

            bool receivedMoveCommand = false;

            for (int i = 0; i < _commandBuffer.Count; i++)
            {
                if (_commandBuffer[i].Type == BrawlerCommandType.Move)
                    receivedMoveCommand = true;

                ProcessCommand(_commandBuffer[i]);
            }

            if (!receivedMoveCommand)
                SetMoveInput(Vector3.zero);

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

            if (_inputBuffer.HasPending && State.CanUseActionInput(currentTick))
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
            UpdateDebugSnapshot(currentTick);
        }

        private void ProcessCommand(BrawlerCommand cmd)
        {
            switch (cmd.Type)
            {
                case BrawlerCommandType.Move:
                    TrySetMove(cmd.Direction);
                    break;

                case BrawlerCommandType.MainAttack:
                    TryUseMainAttack(cmd.Direction, cmd.TargetPoint, cmd.HasTargetPoint, out _);
                    break;

                case BrawlerCommandType.Gadget:
                    TryUseGadget(cmd.Direction, out _);
                    break;

                case BrawlerCommandType.Super:
                    TryUseSuper(cmd.Direction, cmd.TargetPoint, cmd.HasTargetPoint, out _);
                    break;

                case BrawlerCommandType.Hypercharge:
                    TryActivateHypercharge(out _);
                    break;
            }
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
            BrawlerDebugTracker.Remove(this);
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
            bool isGadget,
            ProjectilePresentationProfile presentationProfile = null)
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
                IsGadget = isGadget,

                IsHybrid = false,
                AllyHealAmount = 0f,
                EnemyDamageAmount = 0f,
                HitTeamRule = ProjectileHitTeamRule.EnemiesOnly,

                DeliveryType = ProjectileDeliveryType.DirectHit,
                TargetPoint = Vector3.zero,

                HasHybridAoEImpact = false,
                ImpactRadius = 0f,
                ImpactEnemyDamage = 0f,
                ImpactAllyHeal = 0f,

                UseArcMotion = false,
                ArcHeight = 0f,
                TravelDistance = 0f,

                PresentationProfile = presentationProfile,
                IsChainProjectile = false,
                RemainingBounces = 0,
                BounceRadius = 0f,
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
                        AbilityDefinition currentMainAttackDef = State.GetCurrentMainAttackDefinition();
                        IAbilityLogic mainAttackLogic = State?.RuntimeKit?.MainAttackLogic ?? _mainAttack;
                        BrawlerActionRequestType actionType = BrawlerActionRequestType.MainAttack;

                        if (currentMainAttackDef == null || !State.CanUseAction(actionType, currentTick))
                            break;

                        if (!State.TryConsumeActionCost(actionType))
                            break;

                        State.EnterActionState(
                            BrawlerActionStateType.CastingMainAttack,
                            currentTick,
                            currentMainAttackDef.GetCastDurationTicks(),
                            currentMainAttackDef.AllowMovementDuringCast,
                            currentMainAttackDef.AllowActionInputDuringCast,
                            currentMainAttackDef.IsInterruptible);

                        var executionContext = new AbilityExecutionContext
                        {
                            Source = this,
                            AbilityDefinition = currentMainAttackDef,
                            SlotType = AbilitySlotType.MainAttack,
                            Origin = transform.position,
                            Direction = cmd.Direction,
                            TargetPoint = cmd.TargetPoint,
                            HasTargetPoint = cmd.HasTargetPoint,
                            StartTick = currentTick,
                            IsSuper = false,
                            IsGadget = false
                        };

                        State.LastAttackTick = currentTick;

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = AbilityEventType.CastStarted,
                            Source = this,
                            AbilityDefinition = currentMainAttackDef,
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
                            AbilityDefinition = currentMainAttackDef,
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
                            AbilityDefinition = currentMainAttackDef,
                            Position = executionContext.Origin,
                            Direction = executionContext.Direction,
                            Value = 0f,
                            Tick = currentTick
                        });

                        var result = mainAttackLogic != null
                            ? mainAttackLogic.Execute(this, executionContext)
                            : AbilityExecutionResult.Failed(currentMainAttackDef, AbilitySlotType.MainAttack);

                        if (result.Success)
                        {
                            State.StartCooldownForAction(actionType, currentTick);
                        }

                        AbilityEventBus.Raise(new AbilityExecutionEvent
                        {
                            EventType = result.Success ? AbilityEventType.CastSucceeded : AbilityEventType.CastFailed,
                            Source = this,
                            AbilityDefinition = currentMainAttackDef,
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
                                AbilityDefinition = currentMainAttackDef,
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
                                AbilityDefinition = currentMainAttackDef,
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
                                AbilityDefinition = currentMainAttackDef,
                                Position = executionContext.Origin,
                                Direction = executionContext.Direction,
                                Value = 0f,
                                Tick = currentTick
                            });
                        }

                        break;
                    }

                case InputCommandType.Gadget:
                    {
                        GadgetDefinition currentGadgetDef = GetActiveGadgetDefinition();
                        BrawlerActionRequestType actionType = BrawlerActionRequestType.Gadget;

                        if (currentGadgetDef == null || !State.CanUseAction(actionType, currentTick))
                            break;

                        if (!State.TryConsumeActionCost(actionType))
                            break;

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
                            State.StartCooldownForAction(actionType, currentTick);
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

                        break;
                    }

                case InputCommandType.Super:
                    {
                        Debug.Log("Executing Super");
                        AbilityDefinition currentSuperDef = State.GetCurrentSuperDefinition();
                        BrawlerActionRequestType actionType = BrawlerActionRequestType.Super;

                        if (currentSuperDef == null || !State.CanUseAction(actionType, currentTick))
                            break;

                        if (!State.TryConsumeActionCost(actionType))
                            break;

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
                            TargetPoint = cmd.TargetPoint,
                            HasTargetPoint = cmd.HasTargetPoint,
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
                            State.StartCooldownForAction(actionType, currentTick);
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

                        break;
                    }
            }
        }

        public BrawlerController ResolveTarget(
        AbilityTargetTeamRule teamRule,
        AbilityTargetSelectionRule selectionRule,
        float range,
        bool includeSelf = false,
        bool requireAlive = true)
        {
            AbilityTargetRequest request = new AbilityTargetRequest
            {
                Source = this,
                Origin = Position,
                Direction = transform.forward,
                Range = range,
                TeamRule = teamRule,
                SelectionRule = selectionRule,
                CountRule = AbilityTargetCountRule.Single,
                IncludeSelf = includeSelf,
                RequireAlive = requireAlive
            };

            return AbilityTargetResolver.ResolveSingleTarget(request);
        }

        public void ResolveTargets(
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule,
            float range,
            List<BrawlerController> results,
            bool includeSelf = false,
            bool requireAlive = true)
        {
            AbilityTargetRequest request = new AbilityTargetRequest
            {
                Source = this,
                Origin = Position,
                Direction = transform.forward,
                Range = range,
                TeamRule = teamRule,
                SelectionRule = selectionRule,
                CountRule = AbilityTargetCountRule.Multiple,
                IncludeSelf = includeSelf,
                RequireAlive = requireAlive
            };

            AbilityTargetResolver.ResolveTargets(request, results);
        }

        private void ActivateHypercharge()
        {
            HyperchargeDefinition def = State.EquippedHypercharge ?? _definition.Hypercharge;
            if (def == null)
                return;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            if (!State.CanUseHypercharge(currentTick))
                return;

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
            State.RefreshGadgetChargesFromRuntimeKit();

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

        private void TrySetMove(Vector3 direction)
        {
            SetMoveInput(direction);
        }

        public bool TryUseMainAttack(Vector3 direction, Vector3 targetPoint, bool hasTargetPoint, out BrawlerActionBlockReason blockReason)
        {
            if (State == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            AbilityDefinition currentMainAttackDef = State.GetCurrentMainAttackDefinition();
            if (currentMainAttackDef == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            blockReason = State.GetBlockReasonForAction(
                BrawlerActionRequestType.MainAttack,
                ServiceProvider.Get<ISimulationClock>().CurrentTick);

            if (blockReason != BrawlerActionBlockReason.None)
                return false;

            BufferAttack(InputCommandType.MainAttack, direction, targetPoint, hasTargetPoint);
            return true;
        }

        public bool TryUseGadget(Vector3 direction, out BrawlerActionBlockReason blockReason)
        {
            if (State == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            GadgetDefinition currentGadgetDef = GetActiveGadgetDefinition();
            if (currentGadgetDef == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            blockReason = State.GetBlockReasonForAction(
                BrawlerActionRequestType.Gadget,
                ServiceProvider.Get<ISimulationClock>().CurrentTick);

            if (blockReason != BrawlerActionBlockReason.None)
                return false;

            BufferAttack(InputCommandType.Gadget, direction, Vector3.zero, false);
            return true;
        }

        public bool TryUseSuper(Vector3 direction, Vector3 targetPoint, bool hasTargetPoint, out BrawlerActionBlockReason blockReason)
        {
            if (State == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            AbilityDefinition currentSuperDef = State.GetCurrentSuperDefinition();
            if (currentSuperDef == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            blockReason = State.GetBlockReasonForAction(
                BrawlerActionRequestType.Super,
                ServiceProvider.Get<ISimulationClock>().CurrentTick);

            if (blockReason != BrawlerActionBlockReason.None)
                return false;

            BufferAttack(InputCommandType.Super, direction, targetPoint, hasTargetPoint);
            return true;
        }

        public bool TryActivateHypercharge(out BrawlerActionBlockReason blockReason)
        {
            if (State == null)
            {
                blockReason = BrawlerActionBlockReason.MissingDefinition;
                return false;
            }

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            blockReason = State.GetBlockReasonForAction(BrawlerActionRequestType.Hypercharge, currentTick);

            if (blockReason != BrawlerActionBlockReason.None)
                return false;

            ActivateHypercharge();
            return true;
        }

        private void UpdateDebugSnapshot(uint currentTick)
        {
            if (State == null)
                return;

            _debugSnapshot.ClearLists();

            _debugSnapshot.BrawlerName = Definition != null ? Definition.BrawlerName : name;
            _debugSnapshot.EntityId = EntityID;

            _debugSnapshot.CurrentHealth = State.CurrentHealth;
            _debugSnapshot.MaxHealth = State.MaxHealth.Value;
            _debugSnapshot.CurrentPowerLevel = State.CurrentPowerLevel;

            _debugSnapshot.ActionState = State.ActionState.StateType.ToString();
            _debugSnapshot.CanMove = State.CanMove(currentTick);
            _debugSnapshot.CanUseActionInput = State.CanUseActionInput(currentTick);

            _debugSnapshot.MainAttackReady = State.CanUseMainAttack(currentTick);
            _debugSnapshot.GadgetReady = State.CanUseGadget(currentTick);
            _debugSnapshot.SuperReady = State.CanUseSuper(currentTick);
            _debugSnapshot.HyperchargeReady = State.CanUseHypercharge(currentTick);

            _debugSnapshot.MainAttackBlockReason = State.GetActionBlockReasonText(State.GetMainAttackBlockReason(currentTick));
            _debugSnapshot.GadgetBlockReason = State.GetActionBlockReasonText(State.GetGadgetBlockReason(currentTick));
            _debugSnapshot.SuperBlockReason = State.GetActionBlockReasonText(State.GetSuperBlockReason(currentTick));
            _debugSnapshot.HyperchargeBlockReason = State.GetActionBlockReasonText(State.GetHyperchargeBlockReason(currentTick));

            _debugSnapshot.EquippedGadget = State.RuntimeBuild?.EquippedGadget != null ? State.RuntimeBuild.EquippedGadget.name : "None";
            _debugSnapshot.EquippedStarPower = State.RuntimeBuild?.EquippedStarPower != null ? State.RuntimeBuild.EquippedStarPower.name : "None";
            _debugSnapshot.EquippedHypercharge = State.RuntimeBuild?.EquippedHypercharge != null ? State.RuntimeBuild.EquippedHypercharge.name : "None";

            if (State.RuntimeBuild != null)
            {
                for (int i = 0; i < State.RuntimeBuild.EquippedGears.Count; i++)
                {
                    var gear = State.RuntimeBuild.EquippedGears[i];
                    _debugSnapshot.EquippedGears.Add(gear != null ? gear.name : "None");
                }

                _debugSnapshot.Gear1Unlocked = State.RuntimeBuild.IsGearSlot1Unlocked;
                _debugSnapshot.Gear2Unlocked = State.RuntimeBuild.IsGearSlot2Unlocked;
                _debugSnapshot.GadgetUnlocked = State.RuntimeBuild.IsGadgetSlotUnlocked;
                _debugSnapshot.StarPowerUnlocked = State.RuntimeBuild.IsStarPowerSlotUnlocked;
                _debugSnapshot.HyperchargeUnlocked = State.RuntimeBuild.IsHyperchargeSlotUnlocked;
            }

            for (int i = 0; i < State.EquippedPassives.Count; i++)
            {
                PassiveDefinition passive = State.EquippedPassives[i];
                _debugSnapshot.EquippedPassives.Add(passive != null ? passive.name : "None");
            }

            _debugSnapshot.HyperchargeActive = State.Hypercharge.IsActive;
            _debugSnapshot.HyperchargeChargePercent = State.Hypercharge.ChargePercent;
            _debugSnapshot.SuperCharged = State.SuperCharge.IsReady;
            _debugSnapshot.Position = Position;

            BrawlerDebugTracker.UpdateSnapshot(this, _debugSnapshot);
        }

        public Vector3 GetPrimaryFirePosition()
        {
            return _presentationAnchors != null
                ? _presentationAnchors.GetPrimaryFirePosition(transform)
                : transform.position;
        }

        public Vector3 GetSecondaryFirePosition()
        {
            return _presentationAnchors != null
                ? _presentationAnchors.GetSecondaryFirePosition(transform)
                : transform.position;
        }

        public Vector3 GetCastPosition()
        {
            return _presentationAnchors != null
                ? _presentationAnchors.GetCastPosition(transform)
                : transform.position;
        }

        public Coroutine RunTimedBurst(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        private void BuildVisualFromDefinition()
        {
            if (_visualRoot == null)
                return;

            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_visualRoot.GetChild(i).gameObject);
            }

            _spawnedVisualInstance = null;
            _presentationAnchors = null;
            _visualModel = null;

            if (_definition == null || _definition.ModelPrefab == null)
                return;

            _spawnedVisualInstance = Instantiate(_definition.ModelPrefab, _visualRoot);
            _spawnedVisualInstance.transform.localPosition = Vector3.zero;
            _spawnedVisualInstance.transform.localRotation = Quaternion.identity;
            _spawnedVisualInstance.transform.localScale = Vector3.one;

            _presentationAnchors = _spawnedVisualInstance.GetComponentInChildren<BrawlerPresentationAnchors>();
            _visualModel = _spawnedVisualInstance;
        }
    }
}