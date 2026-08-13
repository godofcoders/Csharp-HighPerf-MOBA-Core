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
        [Tooltip("Prototype match power level. Defaulting to 11 exposes full build-kit systems such as gadgets, star powers, gears, and hypercharge while the progression UI is still lightweight.")]
        [Range(1, 11)]
        [SerializeField] private int _startingPowerLevel = 11;

        [Header("Debug / Testing")]
        [Tooltip("Temporary playtest helper: the locally controlled brawler starts and respawns with full super + hypercharge.")]
        [SerializeField] private bool _debugReadySuperAndHyperchargeForPlayer = true;

        [SerializeField] private Transform _visualRoot;

        [SerializeField] private Transform _presentationAnchor;
        [SerializeField] private BrawlerPresentationAnchors _presentationAnchors;

        private Vector3 _previousSimPosition;
        private Vector3 _currentSimPosition;
        private Quaternion _previousSimRotation;
        private Quaternion _currentSimRotation;
        private float _lastSimulationUpdateTime;
        private Vector3 _presentationWorldOffset;
        private Coroutine _presentationLeapRoutine;

        private const float SimulationTickInterval = 1f / 30f;
        private const float LocalObserverRefreshIntervalSeconds = 0.5f;
        private const int WorldCollisionOverlapBufferSize = 16;
        private const int WorldCollisionDepenetrationPasses = 3;
        private const float WorldCollisionEpsilon = 0.000001f;
        private const float WorldCollisionSkinEpsilon = 0.001f;
        private const float MovementInputDeadZoneSqr = 0.01f;
        private const float MovementVelocityStopEpsilonSqr = 0.0004f;
        public const float BodyScaleMultiplier = 1.25f;
        private const float BaseCollisionRadius = 0.5f;

        private static TeamType _cachedLocalObserverTeam = TeamType.Neutral;
        private static float _nextLocalObserverRefreshTime;
        private static bool _hasCachedLocalObserver;
        private static readonly Collider[] _worldCollisionOverlapBuffer =
            new Collider[WorldCollisionOverlapBufferSize];

        private GameObject _spawnedVisualInstance;

        private Vector3 _lastTickPosition;
        private readonly InputBuffer _inputBuffer = new InputBuffer();
        private Vector3 _currentMoveInput;
        private Vector3 _actionFacingDirection;
        private uint _actionFacingUntilTick;
        private Vector3 _planarVelocity;

        private IAbilityLogic _mainAttack;
        private IAbilityLogic _superAbility;
        private IAbilityLogic _gadgetLogic;

        private bool _isInitialized;

        private const uint ActionFacingGraceTicks = 2u;

        private readonly List<GadgetDefinition> _equippedGadgets = new List<GadgetDefinition>(2);
        private HyperchargeDefinition _equippedHypercharge;
        private NanopowerDefinition _activeNanopower;
        private BrawlerBuildDefinition _resolvedBuildSource;
        private BrawlerBuildDefinition _buildOverride;
        private int _powerLevelOverride;

        private readonly List<BrawlerCommand> _commandBuffer = new List<BrawlerCommand>(8);
        private IBrawlerCommandSource _commandSource;

        private readonly BrawlerDebugSnapshot _debugSnapshot = new BrawlerDebugSnapshot();
        private int _entityId;
        private Vector3 _lastKnownPosition;
        private BrawlerStealthPresentation _stealthPresentation;

        public BrawlerDefinition Definition => _definition;
        public BrawlerState State { get; private set; }

        public TeamType Team => _team;
        public Vector3 Position => this != null ? GetLivePosition() : _lastKnownPosition;
        public Vector3 CurrentPosition => Position;
        public float CollisionRadius => BaseCollisionRadius * BodyScaleMultiplier;
        public int EntityID => GetEntityId();
        public Transform PresentationFollowTarget => _presentationAnchor != null ? _presentationAnchor : transform;
        public GameObject VisualModel => _visualModel;
        public Vector3 PlanarVelocity => _planarVelocity;
        public bool DebugReadySuperAndHyperchargeForPlayer => _debugReadySuperAndHyperchargeForPlayer;
        public NanopowerDefinition ActiveNanopower => _activeNanopower;

        [Header("Movement Feel")]
        [Tooltip("Global feel multiplier applied after brawler stats/modifiers. Keeps authored balance intact while tuning how fast movement reads on the current camera/map scale.")]
        [SerializeField, Range(0.5f, 1.1f)] private float _movementFeelSpeedScale = 0.90f;
        [Tooltip("Meters/second^2 used when a brawler starts moving or changes movement direction.")]
        [SerializeField, Min(1f)] private float _groundAcceleration = 24f;
        [Tooltip("Meters/second^2 used when movement input is released. Higher values keep movement responsive while avoiding instant stops.")]
        [SerializeField, Min(1f)] private float _groundDeceleration = 40f;
        [Tooltip("Maximum body turn speed while moving normally.")]
        [SerializeField, Min(90f)] private float _bodyTurnSpeedDegrees = 540f;
        [Tooltip("Maximum body turn speed while an attack/super is holding facing direction.")]
        [SerializeField, Min(90f)] private float _actionTurnSpeedDegrees = 1080f;

        [Header("World Collision")]
        [SerializeField] private LayerMask _worldCollisionLayer;
        [SerializeField] private float _worldCollisionRadius = 0.45f;
        [SerializeField] private float _worldCollisionProbeHeight = 0.5f;
        [SerializeField] private float _worldCollisionSkin = 0.03f;
        [Tooltip("Maximum overlap recovery applied in one simulation tick. Prevents visible snap/teleport when a brawler briefly clips a wall or spawn-side blocker.")]
        [SerializeField, Min(0.02f)] private float _maxWorldOverlapCorrectionPerTick = 0.12f;
        [SerializeField] private bool _slideAlongWorldCollision = true;

        private bool _hasResolvedWorldCollisionMask;
        private int _resolvedWorldCollisionMask;

        public bool TryGetWorldCollisionProbe(
            out int collisionMask,
            out float radius,
            out float probeHeight,
            out float skin)
        {
            collisionMask = ResolveWorldCollisionMask();
            radius = EffectiveWorldCollisionRadius;
            probeHeight = Mathf.Max(0f, _worldCollisionProbeHeight);
            skin = Mathf.Max(0f, _worldCollisionSkin);
            return collisionMask != 0;
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

        protected override void Awake()
        {
            _entityId = gameObject.GetInstanceID();
            _lastKnownPosition = transform.position;
            base.Awake();

            // Generic shell flow:
            // definition may be injected later by InitializeFromMatchmaking.
            if (_definition != null && !_isInitialized)
            {
                InternalInitialize(_definition, _team);
            }
        }

        private void LateUpdate()
        {
            if (_presentationAnchor == null)
                return;

            float alpha = Mathf.Clamp01((Time.time - _lastSimulationUpdateTime) / SimulationTickInterval);

            Vector3 interpolatedWorldPosition = Vector3.Lerp(_previousSimPosition, _currentSimPosition, alpha);
            Quaternion interpolatedWorldRotation = Quaternion.Slerp(_previousSimRotation, _currentSimRotation, alpha);

            _presentationAnchor.position = interpolatedWorldPosition + _presentationWorldOffset;
            _presentationAnchor.rotation = interpolatedWorldRotation;

            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
            }
        }

        public void SetCommandSource(IBrawlerCommandSource source)
        {
            _commandSource = source;
        }

        public void InitializeFromMatchmaking(
            BrawlerDefinition def,
            TeamType team,
            BrawlerBuildDefinition buildOverride = null,
            int powerLevelOverride = 0)
        {
            _buildOverride = buildOverride;
            _powerLevelOverride = powerLevelOverride;
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
            EnsureHyperchargePresentation();
            EnsureStealthPresentation();
            EnsureLingeringDamagePresentation();

            State = new BrawlerState(_definition, _team);
            State.Owner = this;
            State.SetPowerLevel(ResolveStartingPowerLevel(), false);

            _mainAttack = _definition.MainAttack?.CreateLogic();
            _superAbility = _definition.SuperAbility?.CreateLogic();

            State.RuntimeKit.SetMainAttack(_definition.MainAttack, _mainAttack);
            State.RuntimeKit.SetSuper(_definition.SuperAbility, _superAbility);

            ResolveAndApplyCurrentBuild();
            State.ResetHealthToMax();

            _lastTickPosition = transform.position;
            _lastKnownPosition = transform.position;
            CancelPresentationLeap();

            _previousSimPosition = transform.position;
            _currentSimPosition = transform.position;
            _previousSimRotation = transform.rotation;
            _currentSimRotation = transform.rotation;
            _lastSimulationUpdateTime = Time.time;

            if (_presentationAnchor != null)
            {
                _presentationAnchor.position = transform.position;
                _presentationAnchor.rotation = transform.rotation;
            }

            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
            }

            _isInitialized = true;

            CombatRegistry.Register(this);
            SimulationClock.Grid?.Add(this);
            State.OnDeath += HandleDeath;

            Debug.Log($"[SIM] {gameObject.name} initialized as {_definition.BrawlerName} on Team {_team}");
        }

        /// <summary>
        /// Resolves the current default build for this brawler/power-level and
        /// applies it to State (passives, runtime kit slots, runtime build,
        /// hypercharge) and to local controller fields (_equippedGadgets,
        /// _equippedHypercharge, _gadgetLogic). Falls back to the legacy
        /// definition-direct build on resolve failure.
        ///
        /// Called from both InternalInitialize (match start) and Respawn
        /// (after State.Reset wipes RuntimeBuild/RuntimeKit). Keeping the flow
        /// in one place means respawn behavior can't drift from match-start
        /// behavior the next time the resolver or ApplyResolvedBuild evolves.
        /// </summary>
        private void ResolveAndApplyCurrentBuild()
        {
            BrawlerBuildDefinition buildToUse = GetBuildToUse();
            if (buildToUse != null)
            {
                if (BrawlerBuildResolver.TryResolveUnlockedOnly(_definition, buildToUse, State.CurrentPowerLevel, out ResolvedBrawlerBuild resolved, out string error))
                {
                    _resolvedBuildSource = buildToUse;
                    ApplyResolvedBuild(resolved);
                    State.RefreshGadgetChargesFromRuntimeKit();
                    return;
                }

                Debug.LogWarning($"[Build] Failed to resolve build '{buildToUse.name}' for '{_definition.name}': {error}");
            }

            _resolvedBuildSource = null;
            ApplyLegacyFallbackBuild();
            State.RefreshGadgetChargesFromRuntimeKit();
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
            State.SetPassiveLoadout(BuildPassiveLoadoutWithActiveNanopower(fallbackPassives), false);

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

            if (_buildOverride != null)
                return _buildOverride;

            return _definition.GetUsableDefaultBuild(State.CurrentPowerLevel);
        }

        private int ResolveStartingPowerLevel()
        {
            int requested = _powerLevelOverride > 0
                ? _powerLevelOverride
                : _startingPowerLevel;

            return Mathf.Clamp(requested, 1, 11);
        }

        private void ApplyResolvedBuild(ResolvedBrawlerBuild resolved)
        {
            _equippedGadgets.Clear();
            _equippedHypercharge = null;

            if (resolved == null)
            {
                State.SetPassiveLoadout(BuildPassiveLoadoutWithActiveNanopower(null), false);
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

            State.SetPassiveLoadout(BuildPassiveLoadoutWithActiveNanopower(resolved.PassiveOptions), false);

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

        public void SetActiveNanopower(NanopowerDefinition nanopower, bool preserveHealthRatio = true)
        {
            if (_activeNanopower == nanopower)
                return;

            _activeNanopower = nanopower;

            if (State == null)
                return;

            State.SetPassiveLoadout(
                BuildPassiveLoadoutWithActiveNanopower(State.EquippedPassives),
                preserveHealthRatio);
        }

        private List<PassiveDefinition> BuildPassiveLoadoutWithActiveNanopower(IEnumerable<PassiveDefinition> basePassives)
        {
            List<PassiveDefinition> result = new List<PassiveDefinition>(6);

            if (basePassives != null)
            {
                foreach (PassiveDefinition passive in basePassives)
                {
                    if (passive == null || passive is NanopowerDefinition)
                        continue;

                    if (!result.Contains(passive))
                        result.Add(passive);
                }
            }

            if (_activeNanopower != null && !result.Contains(_activeNanopower))
                result.Add(_activeNanopower);

            return result;
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
            direction.y = 0f;
            _currentMoveInput = Vector3.ClampMagnitude(direction, 1f);
        }

        private void BufferAttack(
            InputCommandType type,
            Vector3 direction,
            Vector3 targetPoint,
            bool hasTargetPoint,
            uint currentTick)
        {
            _inputBuffer.Enqueue(type, direction, targetPoint, hasTargetPoint, currentTick);
        }

        private void TryExecuteBufferedCommand(uint currentTick)
        {
            if (!_inputBuffer.TryPeek(currentTick, out BufferedCommand cmd))
                return;

            BrawlerActionRequestType actionType = ToActionRequestType(cmd.Type);
            if (actionType == BrawlerActionRequestType.None)
            {
                _inputBuffer.Clear();
                return;
            }

            BrawlerActionBlockReason blockReason = State.GetBlockReasonForAction(actionType, currentTick);
            if (blockReason != BrawlerActionBlockReason.None)
            {
                if (!ShouldKeepBufferedAction(actionType, blockReason))
                    _inputBuffer.Clear();

                return;
            }

            if (_inputBuffer.TryConsume(currentTick, out cmd))
                ExecuteCommand(cmd, currentTick);
        }

        private static BrawlerActionRequestType ToActionRequestType(InputCommandType type)
        {
            switch (type)
            {
                case InputCommandType.MainAttack:
                    return BrawlerActionRequestType.MainAttack;

                case InputCommandType.Gadget:
                    return BrawlerActionRequestType.Gadget;

                case InputCommandType.Super:
                    return BrawlerActionRequestType.Super;

                case InputCommandType.Hypercharge:
                    return BrawlerActionRequestType.Hypercharge;

                default:
                    return BrawlerActionRequestType.None;
            }
        }

        private static bool ShouldKeepBufferedAction(
            BrawlerActionRequestType actionType,
            BrawlerActionBlockReason blockReason)
        {
            switch (blockReason)
            {
                case BrawlerActionBlockReason.ActionLocked:
                case BrawlerActionBlockReason.AbilityCooldown:
                    return true;

                case BrawlerActionBlockReason.NoAmmo:
                    return actionType == BrawlerActionRequestType.MainAttack;

                default:
                    return false;
            }
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
            State.TickSuperChargeSources(SimulationClock.TickDeltaTime, currentTick);
            State.UpdateActionState(currentTick);
            State.UpdateResources(SimulationClock.TickDeltaTime);

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

            TryExecuteBufferedCommand(currentTick);

            if (State.CanMove(currentTick))
                ProcessMovement(currentTick);
            else
            {
                SetMoveInput(Vector3.zero);
                _planarVelocity = Vector3.zero;
            }

            if (_currentMoveInput.sqrMagnitude <= MovementInputDeadZoneSqr &&
                _planarVelocity.sqrMagnitude <= MovementVelocityStopEpsilonSqr)
            {
                _planarVelocity = Vector3.zero;
                _previousSimPosition = _currentSimPosition;
                _previousSimRotation = _currentSimRotation;
                _currentSimPosition = transform.position;
                _currentSimRotation = transform.rotation;
                _lastSimulationUpdateTime = Time.time;
            }

            SimulationClock.Grid?.UpdateEntity(this, _lastTickPosition, transform.position);
            _lastTickPosition = transform.position;
            _lastKnownPosition = transform.position;

            _mainAttack?.Tick(currentTick);
            _superAbility?.Tick(currentTick);
            _gadgetLogic?.Tick(currentTick);
            State.TickHealthRegeneration(currentTick, SimulationClock.TickDeltaTime);

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
                    if (TryKickBrawlBall(cmd.Direction, false, cmd.Tick))
                        break;

                    TryUseMainAttack(cmd.Direction, cmd.TargetPoint, cmd.HasTargetPoint, out _);
                    break;

                case BrawlerCommandType.Gadget:
                    TryUseGadget(cmd.Direction, out _);
                    break;

                case BrawlerCommandType.Super:
                    if (TryKickBrawlBall(cmd.Direction, true, cmd.Tick))
                        break;

                    TryUseSuper(cmd.Direction, cmd.TargetPoint, cmd.HasTargetPoint, out _);
                    break;

                case BrawlerCommandType.Hypercharge:
                    TryActivateHypercharge(out _);
                    break;
            }
        }

        private bool TryKickBrawlBall(Vector3 direction, bool isSuperKick, uint currentTick)
        {
            if (State == null || State.IsDead || !State.CanUseActionInput(currentTick))
                return false;

            BrawlBallMode mode = BrawlBallMode.Instance;
            if (mode == null || !mode.CanKickBall(this))
                return false;

            Vector3 kickDirection = direction;
            kickDirection.y = 0f;
            if (kickDirection.sqrMagnitude <= 0.001f)
                kickDirection = transform.forward;

            kickDirection.y = 0f;
            if (kickDirection.sqrMagnitude <= 0.001f)
                return false;

            kickDirection.Normalize();

            if (isSuperKick)
            {
                if (!State.CanUseSuper(currentTick))
                    return false;

                if (!State.TryConsumeActionCost(BrawlerActionRequestType.Super))
                    return false;

                State.StartCooldownForAction(BrawlerActionRequestType.Super, currentTick);
            }

            if (!mode.TryKickBall(this, kickDirection, isSuperKick, currentTick))
                return false;

            ApplyActionFacing(kickDirection, null, currentTick);
            State.LastAttackTick = currentTick;
            return true;
        }

        private void ProcessMovement(uint currentTick)
        {
            bool hasMoveInput = _currentMoveInput.sqrMagnitude > MovementInputDeadZoneSqr;
            bool hasResidualVelocity = _planarVelocity.sqrMagnitude > MovementVelocityStopEpsilonSqr;

            if (!hasMoveInput && !hasResidualVelocity)
                return;

            _previousSimPosition = _currentSimPosition;
            _previousSimRotation = _currentSimRotation;

            float tickDelta = SimulationTickInterval;
            float speed = State.IncomingMovementModifiers.Apply(State.MoveSpeed.Value) *
                          Mathf.Clamp(_movementFeelSpeedScale, 0.1f, 2f);

            float inputMagnitude = hasMoveInput ? Mathf.Clamp01(_currentMoveInput.magnitude) : 0f;
            Vector3 moveDirection = inputMagnitude > 0.001f
                ? _currentMoveInput / inputMagnitude
                : Vector3.zero;

            Vector3 desiredVelocity = moveDirection * (speed * inputMagnitude);
            float acceleration = hasMoveInput
                ? Mathf.Max(1f, _groundAcceleration)
                : Mathf.Max(1f, _groundDeceleration);

            if (hasMoveInput && _planarVelocity.sqrMagnitude > MovementVelocityStopEpsilonSqr)
            {
                float directionAlignment = Vector3.Dot(_planarVelocity.normalized, moveDirection);
                if (directionAlignment < -0.15f)
                    acceleration = Mathf.Max(acceleration, _groundDeceleration);
            }

            Vector3 nextVelocity = Vector3.MoveTowards(
                _planarVelocity,
                desiredVelocity,
                acceleration * tickDelta);

            if (!hasMoveInput && nextVelocity.sqrMagnitude <= MovementVelocityStopEpsilonSqr)
                nextVelocity = Vector3.zero;

            Vector3 desiredMovement = nextVelocity * tickDelta;
            Vector3 resolvedMovement = ResolveMovementAgainstWorld(desiredMovement);

            transform.position += resolvedMovement;
            _planarVelocity = tickDelta > 0f ? resolvedMovement / tickDelta : Vector3.zero;
            _planarVelocity.y = 0f;

            if (!hasMoveInput && _planarVelocity.sqrMagnitude <= MovementVelocityStopEpsilonSqr)
                _planarVelocity = Vector3.zero;

            Vector3 facingDirection = ResolveMovementFacingDirection(
                currentTick,
                resolvedMovement,
                nextVelocity,
                moveDirection,
                hasMoveInput);

            if (facingDirection.sqrMagnitude > 0.001f)
            {
                float turnSpeed = IsActionFacingActive(currentTick)
                    ? Mathf.Max(90f, _actionTurnSpeedDegrees)
                    : Mathf.Max(90f, _bodyTurnSpeedDegrees);

                RotateBodyTowards(facingDirection, turnSpeed, tickDelta);
            }

            _currentSimPosition = transform.position;
            _currentSimRotation = transform.rotation;
            _lastSimulationUpdateTime = Time.time;
        }

        private Vector3 ResolveMovementFacingDirection(
            uint currentTick,
            Vector3 resolvedMovement,
            Vector3 nextVelocity,
            Vector3 moveDirection,
            bool hasMoveInput)
        {
            if (IsActionFacingActive(currentTick))
                return _actionFacingDirection;

            if (resolvedMovement.sqrMagnitude > WorldCollisionEpsilon)
                return resolvedMovement;

            if (nextVelocity.sqrMagnitude > MovementVelocityStopEpsilonSqr)
                return nextVelocity;

            return hasMoveInput ? moveDirection : Vector3.zero;
        }

        private void RotateBodyTowards(Vector3 direction, float turnSpeedDegrees, float tickDelta)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeedDegrees * tickDelta);
        }

        private Vector3 ResolveMovementAgainstWorld(Vector3 desiredMovement)
        {
            desiredMovement.y = 0f;

            if (desiredMovement.sqrMagnitude <= WorldCollisionEpsilon)
                return Vector3.zero;

            int collisionMask = ResolveWorldCollisionMask();
            if (collisionMask == 0)
                return desiredMovement;

            float desiredDistance = desiredMovement.magnitude;
            Vector3 direction = desiredMovement / desiredDistance;

            float radius = EffectiveWorldCollisionRadius;
            float skin = Mathf.Max(0f, _worldCollisionSkin);
            Vector3 startCorrection = ClampWorldOverlapCorrection(
                ResolveWorldOverlap(transform.position, radius, skin, collisionMask),
                out bool overlapCorrectionWasCapped);

            if (overlapCorrectionWasCapped)
                return startCorrection;

            Vector3 startPosition = transform.position + startCorrection;

            Vector3 resolvedMovement = SweepWorldMovement(
                startPosition,
                desiredMovement,
                direction,
                desiredDistance,
                radius,
                skin,
                collisionMask);

            return startCorrection + resolvedMovement;
        }

        private Vector3 ClampWorldOverlapCorrection(Vector3 correction, out bool wasCapped)
        {
            wasCapped = false;
            correction.y = 0f;

            float maxCorrection = Mathf.Max(0.02f, _maxWorldOverlapCorrectionPerTick);
            float maxCorrectionSq = maxCorrection * maxCorrection;

            if (correction.sqrMagnitude <= maxCorrectionSq)
                return correction;

            wasCapped = true;
            return correction.normalized * maxCorrection;
        }

        private Vector3 SweepWorldMovement(
            Vector3 startPosition,
            Vector3 desiredMovement,
            Vector3 direction,
            float desiredDistance,
            float radius,
            float skin,
            int collisionMask)
        {
            Vector3 origin = startPosition + Vector3.up * Mathf.Max(0f, _worldCollisionProbeHeight);

            bool blocked = Physics.SphereCast(
                origin,
                radius,
                direction,
                out RaycastHit hit,
                desiredDistance + skin,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            if (!blocked)
                return desiredMovement;

            float allowedDistance = Mathf.Max(0f, hit.distance - skin);
            Vector3 resolvedMovement = direction * allowedDistance;

            if (!_slideAlongWorldCollision)
                return resolvedMovement;

            Vector3 remainingMovement = desiredMovement - resolvedMovement;

            if (remainingMovement.sqrMagnitude <= WorldCollisionEpsilon)
                return resolvedMovement;

            Vector3 slideMovement = Vector3.ProjectOnPlane(remainingMovement, hit.normal);
            slideMovement.y = 0f;

            if (slideMovement.sqrMagnitude <= WorldCollisionEpsilon)
                return resolvedMovement;

            float slideDistance = slideMovement.magnitude;
            Vector3 slideDirection = slideMovement / slideDistance;

            Vector3 slideOrigin = startPosition + resolvedMovement + Vector3.up * Mathf.Max(0f, _worldCollisionProbeHeight);

            bool slideBlocked = Physics.SphereCast(
                slideOrigin,
                radius,
                slideDirection,
                out RaycastHit slideHit,
                slideDistance + skin,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            if (!slideBlocked)
                return resolvedMovement + slideMovement;

            float allowedSlideDistance = Mathf.Max(0f, slideHit.distance - skin);
            return resolvedMovement + slideDirection * allowedSlideDistance;
        }

        private int ResolveWorldCollisionMask()
        {
            if (_worldCollisionLayer.value != 0)
                return _worldCollisionLayer.value;

            if (_hasResolvedWorldCollisionMask)
                return _resolvedWorldCollisionMask;

            _hasResolvedWorldCollisionMask = true;

            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
            {
                _resolvedWorldCollisionMask = mapGenerator.ObstacleLayer.value;
                return _resolvedWorldCollisionMask;
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            _resolvedWorldCollisionMask = obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
            return _resolvedWorldCollisionMask;
        }

        private float EffectiveWorldCollisionRadius => Mathf.Max(0.01f, _worldCollisionRadius * BodyScaleMultiplier);

        private Vector3 ResolveWorldOverlap(
            Vector3 position,
            float radius,
            float skin,
            int collisionMask)
        {
            Vector3 totalCorrection = Vector3.zero;
            Vector3 correctedPosition = position;
            float clearance = radius + skin;
            float probeHeight = Mathf.Max(0f, _worldCollisionProbeHeight);

            for (int pass = 0; pass < WorldCollisionDepenetrationPasses; pass++)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    correctedPosition + Vector3.up * probeHeight,
                    clearance,
                    _worldCollisionOverlapBuffer,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);

                Vector3 strongestCorrection = Vector3.zero;

                for (int i = 0; i < hitCount; i++)
                {
                    Collider hit = _worldCollisionOverlapBuffer[i];
                    if (hit == null)
                        continue;

                    if (!TryGetPlanarBoundsPushOut(correctedPosition, hit.bounds, clearance, out Vector3 correction))
                        continue;

                    if (correction.sqrMagnitude > strongestCorrection.sqrMagnitude)
                        strongestCorrection = correction;
                }

                if (strongestCorrection.sqrMagnitude <= WorldCollisionEpsilon)
                    break;

                correctedPosition += strongestCorrection;
                totalCorrection += strongestCorrection;
            }

            return totalCorrection;
        }

        private static bool TryGetPlanarBoundsPushOut(
            Vector3 position,
            Bounds bounds,
            float clearance,
            out Vector3 correction)
        {
            float closestX = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            float closestZ = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            float deltaX = position.x - closestX;
            float deltaZ = position.z - closestZ;
            float distanceSq = deltaX * deltaX + deltaZ * deltaZ;

            if (distanceSq > WorldCollisionEpsilon)
            {
                float distance = Mathf.Sqrt(distanceSq);
                float depth = clearance - distance;
                if (depth <= 0f)
                {
                    correction = Vector3.zero;
                    return false;
                }

                float scale = (depth + WorldCollisionSkinEpsilon) / distance;
                correction = new Vector3(deltaX * scale, 0f, deltaZ * scale);
                return true;
            }

            float left = Mathf.Abs(position.x - bounds.min.x) + clearance;
            float right = Mathf.Abs(bounds.max.x - position.x) + clearance;
            float back = Mathf.Abs(position.z - bounds.min.z) + clearance;
            float forward = Mathf.Abs(bounds.max.z - position.z) + clearance;

            float best = left;
            correction = new Vector3(-best, 0f, 0f);

            if (right < best)
            {
                best = right;
                correction = new Vector3(best, 0f, 0f);
            }

            if (back < best)
            {
                best = back;
                correction = new Vector3(0f, 0f, -best);
            }

            if (forward < best)
                correction = new Vector3(0f, 0f, forward);

            return correction.sqrMagnitude > WorldCollisionEpsilon;
        }

        private void ApplyActionFacing(
            Vector3 direction,
            AbilityDefinition abilityDefinition,
            uint currentTick)
        {
            Vector3 flatDirection = direction;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude <= 0.001f)
                return;

            flatDirection.Normalize();
            Quaternion facingRotation = Quaternion.LookRotation(flatDirection);

            transform.rotation = facingRotation;
            _actionFacingDirection = flatDirection;
            _actionFacingUntilTick = currentTick + ResolveActionFacingHoldTicks(abilityDefinition);

            _previousSimPosition = _currentSimPosition;
            _previousSimRotation = _currentSimRotation;
            _currentSimPosition = transform.position;
            _currentSimRotation = facingRotation;
            _lastSimulationUpdateTime = Time.time;

            if (_presentationAnchor != null)
            {
                _presentationAnchor.position = transform.position;
                _presentationAnchor.rotation = facingRotation;
            }
        }

        private bool IsActionFacingActive(uint currentTick)
        {
            return currentTick <= _actionFacingUntilTick &&
                   _actionFacingDirection.sqrMagnitude > 0.001f;
        }

        private uint ResolveActionFacingHoldTicks(AbilityDefinition abilityDefinition)
        {
            uint holdTicks = ActionFacingGraceTicks;

            if (abilityDefinition != null)
            {
                holdTicks = Max(holdTicks, abilityDefinition.GetCastDurationTicks());
                holdTicks = Max(holdTicks, ResolveProjectileCadenceTicks(abilityDefinition));
            }

            return holdTicks + ActionFacingGraceTicks;
        }

        private uint ResolveProjectileCadenceTicks(AbilityDefinition abilityDefinition)
        {
            float durationSeconds = 0f;

            if (abilityDefinition is ProjectileAbilityDefinition projectile)
            {
                int count = Mathf.Max(1, projectile.ProjectileCount);
                durationSeconds = Mathf.Max(0f, projectile.DelayBetweenProjectiles) * Mathf.Max(0, count - 1);
            }
            else if (abilityDefinition is BurstSequenceProjectileAbilityDefinition burst)
            {
                int count = Mathf.Max(1, burst.ProjectileCount);
                durationSeconds = Mathf.Max(0f, burst.DelayBetweenShots) * Mathf.Max(0, count - 1);
            }
            else if (abilityDefinition is VolleyProjectileAbilityDefinition volley)
            {
                int count = Mathf.Max(1, volley.ProjectileCount);
                durationSeconds = Mathf.Max(0f, volley.DelayBetweenShots) * Mathf.Max(0, count - 1);
            }

            return durationSeconds > 0f
                ? SimulationClock.SecondsToTicks(durationSeconds)
                : 0u;
        }

        private static uint Max(uint a, uint b)
        {
            return a > b ? a : b;
        }

        public void TakeDamage(float amount)
        {
            if (!MatchStateUtility.IsCombatResolutionOpen())
                return;

            State?.TakeDamage(amount);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CancelPresentationLeap();
            _lastKnownPosition = Position;
            SimulationClock.Grid?.Remove(this, _lastKnownPosition);
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
            RefreshStealthAttackReveal();
        }

        private void RefreshStealthAttackReveal()
        {
            if (State == null)
                return;

            if (ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock))
                State.LastAttackTick = clock.CurrentTick;
        }

        private void ExecuteCommand(BufferedCommand cmd, uint currentTick)
        {
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

                        ApplyActionFacing(cmd.Direction, currentMainAttackDef, currentTick);

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
                            IsHypercharged = false,
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

                        ApplyActionFacing(cmd.Direction, currentGadgetDef, currentTick);

                        var executionContext = new AbilityExecutionContext
                        {
                            Source = this,
                            AbilityDefinition = currentGadgetDef,
                            SlotType = AbilitySlotType.Gadget,
                            Origin = transform.position,
                            Direction = cmd.Direction,
                            StartTick = currentTick,
                            IsSuper = false,
                            IsHypercharged = false,
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
                        bool isHyperchargedSuper = State.Hypercharge != null &&
                                                    State.Hypercharge.IsActive;

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

                        ApplyActionFacing(cmd.Direction, currentSuperDef, currentTick);

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
                            IsHypercharged = isHyperchargedSuper,
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
                            IsSuper = true,
                            IsHypercharged = isHyperchargedSuper
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
                                IsSuper = true,
                                IsHypercharged = isHyperchargedSuper
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

                case InputCommandType.Hypercharge:
                    {
                        if (State.CanUseAction(BrawlerActionRequestType.Hypercharge, currentTick))
                            ActivateHypercharge();

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
            TeamType enemyTeam = TeamRelationshipUtility.GetPrimaryEnemyTeam(_team);
            if (enemyTeam != TeamType.Neutral &&
                ShouldAwardGenericDeathScore() &&
                MatchManager.Instance != null)
            {
                MatchManager.Instance.AddScore(enemyTeam, 1);
            }

            CancelPresentationLeap();
            gameObject.SetActive(false);
            _lastKnownPosition = Position;
            SimulationClock.Grid?.Remove(this, _lastKnownPosition);

            SpawnManager.Instance?.RequestRespawn(this, _team);
        }

        private static bool ShouldAwardGenericDeathScore()
        {
            switch (SceneSelection.SelectedMode)
            {
                case GameModeId.BrawlBall:
                case GameModeId.GemGrab:
                case GameModeId.HotZone:
                case GameModeId.Knockout:
                case GameModeId.SoloShowdown:
                    return false;

                default:
                    return true;
            }
        }

        public void Respawn(Vector3 position)
        {
            CancelPresentationLeap();
            transform.position = position;
            _lastTickPosition = position;
            _planarVelocity = Vector3.zero;

            State.Reset();

            // State.Reset clears RuntimeBuild + RuntimeKit (we want a clean
            // slate so transient installed ability logics get fresh instances
            // on respawn). Now re-resolve and re-apply the default build so
            // the brawler keeps their gadget / star power / gears / hypercharge
            // definition across death→respawn. Mirrors the flow at match start
            // in InternalInitialize — without this the brawler came back with
            // no usable gadget and an empty kit (Session 4 gap fix).
            State.RuntimeKit.SetMainAttack(_definition.MainAttack, _mainAttack);
            State.RuntimeKit.SetSuper(_definition.SuperAbility, _superAbility);
            ResolveAndApplyCurrentBuild();
            State.ResetHealthToMax();

            State.SetEquippedHypercharge(_equippedHypercharge ?? _definition.Hypercharge);
            State.RefreshGadgetChargesFromRuntimeKit();

            if (_debugReadySuperAndHyperchargeForPlayer &&
                GetComponent<PlayerCommandSource>() != null)
            {
                GrantTestingReadyCharge();
            }

            _previousSimPosition = position;
            _currentSimPosition = position;
            _previousSimRotation = transform.rotation;
            _currentSimRotation = transform.rotation;
            _lastSimulationUpdateTime = Time.time;

            if (_presentationAnchor != null)
            {
                _presentationAnchor.position = transform.position;
                _presentationAnchor.rotation = transform.rotation;
            }

            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
            }

            gameObject.SetActive(true);
            CombatRegistry.Register(this);
            _lastKnownPosition = Position;
            SimulationClock.Grid?.Add(this);
        }

        public void WarpTo(Vector3 position)
        {
            CancelPresentationLeap();
            Vector3 previousPosition = transform.position;
            transform.position = position;
            _lastTickPosition = position;
            _lastKnownPosition = position;
            _planarVelocity = Vector3.zero;

            _previousSimPosition = position;
            _currentSimPosition = position;
            _previousSimRotation = transform.rotation;
            _currentSimRotation = transform.rotation;
            _lastSimulationUpdateTime = Time.time;

            if (_presentationAnchor != null)
            {
                _presentationAnchor.position = position;
                _presentationAnchor.rotation = transform.rotation;
            }

            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
            }

            SimulationClock.Grid?.UpdateEntity(this, previousPosition, position);
        }

        private void UpdateVisualStealth()
        {
            if (State == null)
                return;

            if (_stealthPresentation != null)
            {
                _stealthPresentation.RefreshStealthPresentation();
                return;
            }

            if (_visualModel == null)
                return;

            if (!TryGetLocalObserverTeam(out TeamType observerTeam))
            {
                _visualModel.SetActive(true);
                return;
            }

            bool hidden = State.IsHiddenTo(observerTeam);
            _visualModel.SetActive(!hidden);
        }

        public static bool TryGetLocalObserverTeam(out TeamType team)
        {
            if (_hasCachedLocalObserver && Time.unscaledTime < _nextLocalObserverRefreshTime)
            {
                team = _cachedLocalObserverTeam;
                return true;
            }

            _nextLocalObserverRefreshTime = Time.unscaledTime + LocalObserverRefreshIntervalSeconds;
            _hasCachedLocalObserver = false;
            _cachedLocalObserverTeam = TeamType.Neutral;

            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                PlayerCommandSource source = sources[i];
                if (source == null)
                    continue;

                BrawlerController brawler = source.GetComponent<BrawlerController>();
                if (brawler == null || brawler.State == null)
                    continue;

                _cachedLocalObserverTeam = brawler.Team;
                _hasCachedLocalObserver = true;
                team = _cachedLocalObserverTeam;
                return true;
            }

            team = TeamType.Neutral;
            return false;
        }

        public void GrantSuperCharge(float amount)
        {
            State?.AddSuperCharge(amount);
        }

        public void GrantTestingReadyCharge()
        {
            if (State == null)
                return;

            State.AddSuperCharge(1f);
            State.Resources?.AddHypercharge(1f);
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

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            BrawlerActionRequestType actionType = BrawlerActionRequestType.MainAttack;
            blockReason = State.GetBlockReasonForAction(actionType, currentTick);

            if (blockReason != BrawlerActionBlockReason.None &&
                !ShouldKeepBufferedAction(actionType, blockReason))
            {
                _inputBuffer.Clear();
                return false;
            }

            BufferAttack(InputCommandType.MainAttack, direction, targetPoint, hasTargetPoint, currentTick);
            return blockReason == BrawlerActionBlockReason.None;
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

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            BrawlerActionRequestType actionType = BrawlerActionRequestType.Gadget;
            blockReason = State.GetBlockReasonForAction(actionType, currentTick);

            if (blockReason != BrawlerActionBlockReason.None &&
                !ShouldKeepBufferedAction(actionType, blockReason))
            {
                _inputBuffer.Clear();
                return false;
            }

            BufferAttack(InputCommandType.Gadget, direction, Vector3.zero, false, currentTick);
            return blockReason == BrawlerActionBlockReason.None;
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

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            BrawlerActionRequestType actionType = BrawlerActionRequestType.Super;
            blockReason = State.GetBlockReasonForAction(actionType, currentTick);

            if (blockReason != BrawlerActionBlockReason.None &&
                !ShouldKeepBufferedAction(actionType, blockReason))
            {
                _inputBuffer.Clear();
                return false;
            }

            BufferAttack(InputCommandType.Super, direction, targetPoint, hasTargetPoint, currentTick);
            return blockReason == BrawlerActionBlockReason.None;
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
            {
                if (ShouldKeepBufferedAction(BrawlerActionRequestType.Hypercharge, blockReason))
                    BufferAttack(InputCommandType.Hypercharge, transform.forward, Vector3.zero, false, currentTick);
                else
                    _inputBuffer.Clear();

                return false;
            }

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
            _debugSnapshot.IsInBush = State.IsInBush;
            _debugSnapshot.IsHidden = State.Stealth.IsHidden(currentTick);
            _debugSnapshot.IsRevealed = State.IsRevealed;
            _debugSnapshot.IsProximityRevealed = State.IsProximityRevealed;
            _debugSnapshot.IsStatusRevealed = State.IsStatusRevealed;
            _debugSnapshot.IsAttackRevealed = State.Stealth.IsAttackRevealed(currentTick);
            _debugSnapshot.IsDamageRevealed = State.Stealth.IsDamageRevealed(currentTick);
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

        public void PlayPresentationLeapArc(
            Vector3 takeoffPosition,
            Vector3 landingPosition,
            float durationSeconds,
            float jumpHeight,
            float apexHangPower = 1f)
        {
            CancelPresentationLeap();

            if (_presentationAnchor == null)
                return;

            float duration = Mathf.Max(0.01f, durationSeconds);
            _presentationLeapRoutine = StartCoroutine(PresentationLeapArcRoutine(
                takeoffPosition,
                landingPosition,
                duration,
                Mathf.Max(0f, jumpHeight),
                Mathf.Max(0.35f, apexHangPower)));
        }

        private IEnumerator PresentationLeapArcRoutine(
            Vector3 takeoffPosition,
            Vector3 landingPosition,
            float durationSeconds,
            float jumpHeight,
            float apexHangPower)
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                float easedTravel = Mathf.SmoothStep(0f, 1f, t);
                float arc = Mathf.Pow(Mathf.Sin(t * Mathf.PI), apexHangPower);
                Vector3 visualPosition = Vector3.Lerp(takeoffPosition, landingPosition, easedTravel);
                visualPosition.y += arc * jumpHeight;
                _presentationWorldOffset = visualPosition - transform.position;
                yield return null;
            }

            _presentationWorldOffset = landingPosition - transform.position;
            _presentationLeapRoutine = null;
        }

        private void CancelPresentationLeap()
        {
            if (_presentationLeapRoutine != null)
            {
                StopCoroutine(_presentationLeapRoutine);
                _presentationLeapRoutine = null;
            }

            _presentationWorldOffset = Vector3.zero;
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

            if (BrawlerVisualModelFactory.TryCreate(
                    _definition,
                    _visualRoot,
                    this,
                    out _spawnedVisualInstance))
            {
                _presentationAnchors = _spawnedVisualInstance.GetComponentInChildren<BrawlerPresentationAnchors>();
                _visualModel = _spawnedVisualInstance;
                return;
            }

            if (_definition == null || _definition.ModelPrefab == null)
                return;

            _spawnedVisualInstance = Instantiate(_definition.ModelPrefab, _visualRoot);
            _spawnedVisualInstance.transform.localPosition = Vector3.zero;
            _spawnedVisualInstance.transform.localRotation = Quaternion.identity;
            _spawnedVisualInstance.transform.localScale = Vector3.one;

            _presentationAnchors = _spawnedVisualInstance.GetComponentInChildren<BrawlerPresentationAnchors>();
            _visualModel = _spawnedVisualInstance;
        }

        private void EnsureHyperchargePresentation()
        {
            BrawlerHyperchargePresentation presentation = GetComponent<BrawlerHyperchargePresentation>();
            if (presentation == null)
                presentation = gameObject.AddComponent<BrawlerHyperchargePresentation>();

            presentation.Bind(this);
        }

        private void EnsureStealthPresentation()
        {
            _stealthPresentation = GetComponent<BrawlerStealthPresentation>();
            if (_stealthPresentation == null)
                _stealthPresentation = gameObject.AddComponent<BrawlerStealthPresentation>();

            _stealthPresentation.Bind(this);
        }

        private void EnsureLingeringDamagePresentation()
        {
            BrawlerLingeringDamagePresentation presentation =
                GetComponent<BrawlerLingeringDamagePresentation>();
            if (presentation == null)
                presentation = gameObject.AddComponent<BrawlerLingeringDamagePresentation>();

            presentation.Bind(this);
        }
    }
}
