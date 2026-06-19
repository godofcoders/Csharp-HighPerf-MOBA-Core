using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MOBA.Core.Infrastructure
{
    public class PlayerCommandSource : MonoBehaviour, IBrawlerCommandSource, GameInput.IPlayerActions
    {
        [SerializeField] private BrawlerController _controlledBrawler;

        private GameInput _input;
        private Vector2 _moveInput;
        private Vector2 _aimInput;

        private bool _mainAttackQueued;
        private bool _gadgetQueued;
        private bool _superQueued;
        private bool _hyperchargeQueued;

        private Vector3 _lastAimDirection = Vector3.forward;

        private const float ManualAimThreshold = 0.20f;
        private const float MoveFallbackThreshold = 0.20f;
        private const float AimPreviewHoldDelaySeconds = 0.14f;

        private bool _hasManualAim;
        private Vector3 _manualAimDirection = Vector3.zero;

        // Main attack hold-release (RMB / secondary click)
        private bool _isHoldingMainAttackAim;
        private bool _wasRightMouseHeldLastFrame;
        private Vector3 _heldMainAttackAimDirection = Vector3.zero;
        private float _mainAttackAimHoldStartTime;
        private Vector3 _queuedMainAttackDirection = Vector3.zero;
        private Vector3 _queuedMainAttackTargetPoint = Vector3.zero;
        private bool _queuedMainAttackHasTargetPoint;

        // Super hold-release (E key via InputAction started/canceled)
        private bool _isHoldingSuperAim;
        private Vector3 _heldSuperAimDirection = Vector3.zero;
        private bool _wasSuperKeyHeldLastFrame;
        private bool _wasHyperchargeKeyHeldLastFrame;
        private float _superAimHoldStartTime;
        private Vector3 _heldMainAttackTargetPoint = Vector3.zero;
        private Vector3 _heldSuperTargetPoint = Vector3.zero;
        private Vector3 _queuedSuperDirection = Vector3.zero;
        private Vector3 _queuedSuperTargetPoint = Vector3.zero;
        private bool _queuedSuperHasTargetPoint;
        private int _previewCancelSequence;

        public int PreviewCancelSequence => _previewCancelSequence;

        private void Awake()
        {
            _input = new GameInput();
            _input.Player.AddCallbacks(this);

            if (_controlledBrawler == null)
                _controlledBrawler = GetComponent<BrawlerController>();
        }

        private Vector3 GetCurrentAimWorldPoint()
        {
            if (_controlledBrawler == null)
                return Vector3.zero;

            if (Mouse.current != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                    Ray ray = cam.ScreenPointToRay(mouseScreenPos);

                    Plane groundPlane = new Plane(Vector3.up, _controlledBrawler.Position);
                    if (groundPlane.Raycast(ray, out float enter))
                    {
                        return ray.GetPoint(enter);
                    }
                }
            }

            return _controlledBrawler.Position + (_manualAimDirection * 3f);
        }

        private Vector3 GetClampedTargetPointForAbility(AbilityDefinition abilityDefinition)
        {
            if (_controlledBrawler == null)
                return Vector3.zero;

            return GetClampedTargetPointForAbility(abilityDefinition, GetCurrentAimWorldPoint());
        }

        private Vector3 GetClampedTargetPointForAbility(AbilityDefinition abilityDefinition, Vector3 rawPoint)
        {
            if (_controlledBrawler == null)
                return Vector3.zero;

            Vector3 offset = rawPoint - _controlledBrawler.Position;
            offset.y = 0f;

            float maxRange = ResolveAimRange(abilityDefinition);
            if (offset.sqrMagnitude > maxRange * maxRange)
            {
                offset = offset.normalized * maxRange;
            }

            return _controlledBrawler.Position + offset;
        }

        private void UpdateDesktopSuperAimReleaseFlow()
        {
            if (_controlledBrawler == null || Keyboard.current == null)
                return;

            bool superKeyHeld = Keyboard.current.eKey.isPressed;

            if (superKeyHeld && !_wasSuperKeyHeldLastFrame)
            {
                _isHoldingSuperAim = true;
                _heldSuperAimDirection = Vector3.zero;
                _heldSuperTargetPoint = Vector3.zero;
                _superAimHoldStartTime = Time.time;
            }

            if (!superKeyHeld && _wasSuperKeyHeldLastFrame)
            {
                Vector3 releaseDirection = ResolvePreviewAimDirection(AimPreviewKind.Super);
                bool hasManualRelease = HasSuperPreviewDelayElapsed() &&
                                        releaseDirection.sqrMagnitude > 0.001f;

                if (_isHoldingSuperAim)
                {
                    if (hasManualRelease)
                    {
                        AbilityDefinition superAbility = GetAbilityDefinition(BrawlerActionRequestType.Super);
                        bool hasTargetPoint = AbilityUsesPointTarget(superAbility);
                        QueueSuperCommand(
                            releaseDirection,
                            hasTargetPoint
                                ? ResolveHeldTargetPoint(superAbility, releaseDirection, _heldSuperTargetPoint)
                                : Vector3.zero,
                            hasTargetPoint);
                    }
                    else
                    {
                        QueueAutoAimCommand(BrawlerActionRequestType.Super);
                    }
                }

                _isHoldingSuperAim = false;
                _heldSuperAimDirection = Vector3.zero;
                _heldSuperTargetPoint = Vector3.zero;
                MarkPreviewCanceled();
            }

            _wasSuperKeyHeldLastFrame = superKeyHeld;
        }

        private void UpdateDesktopHyperchargeFlow()
        {
            if (_controlledBrawler == null || Keyboard.current == null)
                return;

            bool hyperchargeKeyHeld = Keyboard.current.rKey.isPressed;
            if (hyperchargeKeyHeld && !_wasHyperchargeKeyHeldLastFrame)
                _hyperchargeQueued = true;

            _wasHyperchargeKeyHeldLastFrame = hyperchargeKeyHeld;
        }

        private void OnEnable()
        {
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            CancelPreviewState(true);
            _input.Player.Disable();
        }

        private void OnDestroy()
        {
            _input.Player.RemoveCallbacks(this);
            _input.Dispose();
        }

        private void Update()
        {
            UpdateManualAimState();
            UpdateHoldAimSnapshots();
            UpdateDesktopMainAttackAimReleaseFlow();
            UpdateDesktopSuperAimReleaseFlow();
            UpdateDesktopHyperchargeFlow();
        }

        public void SetControlledBrawler(BrawlerController controller)
        {
            _controlledBrawler = controller;
        }

        public void CollectCommands(List<BrawlerCommand> output, uint currentTick)
        {
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Move,
                    Direction = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized,
                    TargetPoint = Vector3.zero,
                    HasTargetPoint = false,
                    Tick = currentTick
                });
            }

            if (_mainAttackQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.MainAttack,
                    Direction = _queuedMainAttackDirection,
                    TargetPoint = _queuedMainAttackHasTargetPoint ? _queuedMainAttackTargetPoint : Vector3.zero,
                    HasTargetPoint = _queuedMainAttackHasTargetPoint,
                    Tick = currentTick
                });

                _mainAttackQueued = false;
                _queuedMainAttackDirection = Vector3.zero;
                _queuedMainAttackTargetPoint = Vector3.zero;
                _queuedMainAttackHasTargetPoint = false;
            }

            if (_gadgetQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Gadget,
                    Direction = ResolveActionDirection(BrawlerActionRequestType.Gadget),
                    TargetPoint = Vector3.zero,
                    HasTargetPoint = false,
                    Tick = currentTick
                });

                _gadgetQueued = false;
            }

            if (_superQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Super,
                    Direction = _queuedSuperDirection,
                    TargetPoint = _queuedSuperHasTargetPoint ? _queuedSuperTargetPoint : Vector3.zero,
                    HasTargetPoint = _queuedSuperHasTargetPoint,
                    Tick = currentTick
                });

                _superQueued = false;
                _queuedSuperDirection = Vector3.zero;
                _queuedSuperTargetPoint = Vector3.zero;
                _queuedSuperHasTargetPoint = false;
            }

            if (_hyperchargeQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Hypercharge,
                    Direction = ResolveActionDirection(BrawlerActionRequestType.Hypercharge),
                    TargetPoint = Vector3.zero,
                    HasTargetPoint = false,
                    Tick = currentTick
                });

                _hyperchargeQueued = false;
            }
        }

        public bool HasPreviewAim()
        {
            AimPreviewKind kind = GetPreviewKind();
            return kind != AimPreviewKind.None &&
                   HasPreviewDelayElapsed(kind) &&
                   ResolvePreviewAimDirection(kind).sqrMagnitude > 0.001f;
        }

        public Vector3 GetPreviewTargetPoint()
        {
            if (_controlledBrawler == null)
                return Vector3.zero;

            AimPreviewKind kind = GetPreviewKind();
            AbilityDefinition ability = null;

            switch (kind)
            {
                case AimPreviewKind.MainAttack:
                    ability = GetAbilityDefinition(BrawlerActionRequestType.MainAttack);
                    break;

                case AimPreviewKind.Super:
                    ability = GetAbilityDefinition(BrawlerActionRequestType.Super);
                    break;
            }

            if (ability == null)
                return _controlledBrawler.Position;

            return GetClampedTargetPointForAbility(ability);
        }

        public AimPreviewKind GetPreviewKind()
        {
            if (_isHoldingSuperAim)
                return AimPreviewKind.Super;

            if (_isHoldingMainAttackAim)
                return AimPreviewKind.MainAttack;

            return AimPreviewKind.None;
        }

        public Vector3 GetPreviewAimDirection()
        {
            AimPreviewKind kind = GetPreviewKind();
            return kind != AimPreviewKind.None && HasPreviewDelayElapsed(kind)
                ? ResolvePreviewAimDirection(kind)
                : Vector3.zero;
        }

        private Vector3 ResolvePreviewAimDirection(AimPreviewKind kind)
        {
            Vector3 direction = Vector3.zero;

            if (kind == AimPreviewKind.MainAttack &&
                _heldMainAttackAimDirection.sqrMagnitude > 0.001f)
            {
                direction = _heldMainAttackAimDirection;
            }
            else if (kind == AimPreviewKind.Super &&
                     _heldSuperAimDirection.sqrMagnitude > 0.001f)
            {
                direction = _heldSuperAimDirection;
            }
            else if (_hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
            {
                direction = _manualAimDirection;
            }
            else if (_lastAimDirection.sqrMagnitude > 0.001f)
            {
                direction = _lastAimDirection;
            }
            else if (_controlledBrawler != null)
            {
                direction = _controlledBrawler.transform.forward;
            }
            else
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.zero;
        }

        public Vector3 GetFireAimDirection()
        {
            if (_hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
                return _manualAimDirection;

            return ResolveRawFallbackDirection();
        }

        private Vector3 ResolveActionDirection(BrawlerActionRequestType actionType)
        {
            if (_controlledBrawler == null)
                return GetFireAimDirection();

            AbilityDefinition abilityDefinition = GetAbilityDefinition(actionType);
            bool preferManualAim = abilityDefinition == null || abilityDefinition.PreferManualAim;

            if (preferManualAim)
            {
                if (actionType == BrawlerActionRequestType.MainAttack &&
                    _heldMainAttackAimDirection.sqrMagnitude > 0.001f)
                {
                    _lastAimDirection = _heldMainAttackAimDirection;
                    return _lastAimDirection;
                }

                if (actionType == BrawlerActionRequestType.Super &&
                    _heldSuperAimDirection.sqrMagnitude > 0.001f)
                {
                    _lastAimDirection = _heldSuperAimDirection;
                    return _lastAimDirection;
                }

                if (_hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
                {
                    _lastAimDirection = _manualAimDirection;
                    return _lastAimDirection;
                }
            }

            AimAssistRequest request = BuildAimAssistRequest(actionType, abilityDefinition, ResolveAutoAimForwardSeed());
            AimAssistResult result = AimAssistResolver.Resolve(request);

            if (result.HasResult && result.AimDirection.sqrMagnitude > 0.001f)
            {
                _lastAimDirection = result.AimDirection.normalized;
                return _lastAimDirection;
            }

            Vector3 fallbackDirection = GetFireAimDirection();
            if (fallbackDirection.sqrMagnitude > 0.001f)
            {
                _lastAimDirection = fallbackDirection;
                return _lastAimDirection;
            }

            return _controlledBrawler.transform.forward;
        }

        private AimAssistRequest BuildAimAssistRequest(
            BrawlerActionRequestType actionType,
            AbilityDefinition abilityDefinition,
            Vector3 forward)
        {
            return new AimAssistRequest
            {
                Source = _controlledBrawler,
                AbilityDefinition = abilityDefinition,
                Mode = ResolveAimAssistMode(actionType, abilityDefinition),
                Origin = _controlledBrawler.Position,
                Forward = forward.sqrMagnitude > 0.001f ? forward.normalized : ResolveStableFacingDirection(),
                Range = ResolveAimRange(abilityDefinition),
                ProjectileRadius = ResolveAimProjectileRadius(abilityDefinition),
                ProjectileSpeed = ResolveAimProjectileSpeed(abilityDefinition),
                LowHealthBias = abilityDefinition != null ? abilityDefinition.AimAssistLowHealthBias : 1.35f,
                GemCarrierBias = abilityDefinition != null ? abilityDefinition.AimAssistGemCarrierBias : 0.45f,
                CloseTargetRange = abilityDefinition != null ? abilityDefinition.AimAssistCloseTargetRange : 2.25f,
                LeadStrength = abilityDefinition != null ? abilityDefinition.AimAssistLeadStrength : 0.55f,
                IncludeSelf = ShouldIncludeSelf(actionType, abilityDefinition),
                RequireAlive = true,
                RequireLineOfSight = ShouldRequireLineOfSight(abilityDefinition)
            };
        }

        private AbilityDefinition GetAbilityDefinition(BrawlerActionRequestType actionType)
        {
            if (_controlledBrawler == null || _controlledBrawler.State == null)
                return null;

            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    return _controlledBrawler.State.GetCurrentMainAttackDefinition();

                case BrawlerActionRequestType.Gadget:
                    return _controlledBrawler.State.GetCurrentGadgetDefinition();

                case BrawlerActionRequestType.Super:
                case BrawlerActionRequestType.Hypercharge:
                    return _controlledBrawler.State.GetCurrentSuperDefinition();

                default:
                    return null;
            }
        }

        private bool AbilityUsesPointTarget(AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition == null)
                return false;

            return abilityDefinition.PreviewMode == AimPreviewMode.Throwable ||
                   abilityDefinition.PreviewMode == AimPreviewMode.Placement;
        }

        private AimAssistMode ResolveAimAssistMode(BrawlerActionRequestType actionType, AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition != null && abilityDefinition.AllowAimAssist)
                return abilityDefinition.AimAssistMode;

            if (actionType == BrawlerActionRequestType.Hypercharge)
                return AimAssistMode.SelfCentered;

            return AimAssistMode.None;
        }

        private float ResolveAimRange(AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition != null && abilityDefinition.AimAssistRangeOverride > 0f)
                return abilityDefinition.AimAssistRangeOverride;

            if (abilityDefinition == null)
                return 8f;

            if (abilityDefinition is BasicProjectileAttackDefinition basicAttack)
                return basicAttack.Range;

            if (abilityDefinition is BasicSuperDefinition basicSuper)
                return basicSuper.Range;

            if (abilityDefinition is ProjectileAbilityDefinition projectile)
                return projectile.Range;

            if (abilityDefinition is VolleyProjectileAbilityDefinition volley)
                return volley.Range;

            if (abilityDefinition is AoEAbilityDefinition aoe)
                return aoe.Radius;

            if (abilityDefinition is BurstSequenceProjectileAbilityDefinition burst)
                return burst.Range;

            if (abilityDefinition is ChainProjectileAbilityDefinition chain)
                return chain.Range;

            if (abilityDefinition is ThrownHybridAoEAbilityDefinition thrown)
                return thrown.ThrowRange;

            if (abilityDefinition is ThrownVolleyAoEAbilityDefinition thrownVolley)
                return thrownVolley.ThrowRange;

            if (abilityDefinition is EffectAbilityDefinition effectAbility)
                return effectAbility.PreviewRange;

            return 8f;
        }

        private float ResolveAimProjectileRadius(AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition == null)
                return 0.5f;

            return abilityDefinition.AimPreviewWidth > 0.01f
                ? Mathf.Max(0.05f, abilityDefinition.AimPreviewWidth * 0.5f)
                : 0.5f;
        }

        private float ResolveAimProjectileSpeed(AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition == null)
                return 0f;

            if (abilityDefinition is BasicProjectileAttackDefinition basicAttack)
                return basicAttack.ProjectileSpeed;

            if (abilityDefinition is BasicSuperDefinition basicSuper)
                return basicSuper.ProjectileSpeed;

            if (abilityDefinition is ProjectileAbilityDefinition projectile)
                return projectile.Speed;

            if (abilityDefinition is VolleyProjectileAbilityDefinition volley)
                return volley.Speed;

            if (abilityDefinition is BurstSequenceProjectileAbilityDefinition burst)
                return burst.Speed;

            if (abilityDefinition is ChainProjectileAbilityDefinition chain)
                return chain.Speed;

            if (abilityDefinition is HybridProjectileAbilityDefinition hybrid)
                return hybrid.Speed;

            if (abilityDefinition is ThrownHybridAoEAbilityDefinition thrown)
                return thrown.ThrowSpeed;

            if (abilityDefinition is ThrownVolleyAoEAbilityDefinition thrownVolley)
                return thrownVolley.ThrowSpeed;

            return 0f;
        }

        private bool ShouldRequireLineOfSight(AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition == null)
                return true;

            if (abilityDefinition.PreviewMode == AimPreviewMode.Throwable ||
                abilityDefinition.PreviewMode == AimPreviewMode.Placement)
            {
                return false;
            }

            return abilityDefinition.DeliveryType == AbilityDeliveryType.Projectile ||
                   abilityDefinition is BasicProjectileAttackDefinition ||
                   abilityDefinition is BasicSuperDefinition ||
                   abilityDefinition is ProjectileAbilityDefinition ||
                   abilityDefinition is VolleyProjectileAbilityDefinition ||
                   abilityDefinition is BurstSequenceProjectileAbilityDefinition ||
                   abilityDefinition is ChainProjectileAbilityDefinition;
        }

        private bool ShouldIncludeSelf(BrawlerActionRequestType actionType, AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition != null)
                return abilityDefinition.AimAssistIncludeSelf;

            return actionType == BrawlerActionRequestType.Hypercharge;
        }

        private void UpdateManualAimState()
        {
            _hasManualAim = false;
            _manualAimDirection = Vector3.zero;

            if (_controlledBrawler == null)
                return;

            // Gamepad / stick aim
            if (_aimInput.sqrMagnitude >= (ManualAimThreshold * ManualAimThreshold))
            {
                _manualAimDirection = new Vector3(_aimInput.x, 0f, _aimInput.y).normalized;
                _hasManualAim = true;
                _lastAimDirection = _manualAimDirection;
                return;
            }

            bool allowMouseAim =
                _isHoldingMainAttackAim ||
                _isHoldingSuperAim ||
                (Mouse.current != null && Mouse.current.rightButton.isPressed);

            if (allowMouseAim && Mouse.current != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                    Ray ray = cam.ScreenPointToRay(mouseScreenPos);

                    Plane groundPlane = new Plane(Vector3.up, _controlledBrawler.Position);
                    if (groundPlane.Raycast(ray, out float enter))
                    {
                        Vector3 worldPoint = ray.GetPoint(enter);
                        Vector3 dir = worldPoint - _controlledBrawler.Position;
                        dir.y = 0f;

                        if (dir.sqrMagnitude > 0.001f)
                        {
                            _manualAimDirection = dir.normalized;
                            _hasManualAim = true;
                            _lastAimDirection = _manualAimDirection;
                            return;
                        }
                    }
                }
            }
        }

        private void UpdateHoldAimSnapshots()
        {
            if (_isHoldingMainAttackAim && _hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
            {
                _heldMainAttackAimDirection = _manualAimDirection;

                AbilityDefinition mainAttackAbility = GetAbilityDefinition(BrawlerActionRequestType.MainAttack);
                if (AbilityUsesPointTarget(mainAttackAbility))
                {
                    _heldMainAttackTargetPoint = GetClampedTargetPointForAbility(mainAttackAbility);
                }
            }

            if (_isHoldingSuperAim && _hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
            {
                _heldSuperAimDirection = _manualAimDirection;

                AbilityDefinition superAbility = GetAbilityDefinition(BrawlerActionRequestType.Super);
                if (superAbility != null)
                {
                    _heldSuperTargetPoint = GetClampedTargetPointForAbility(superAbility);
                }
            }
        }

        private void UpdateDesktopMainAttackAimReleaseFlow()
        {
            if (_controlledBrawler == null || Mouse.current == null)
                return;

            bool rightMouseHeld = Mouse.current.rightButton.isPressed;

            if (rightMouseHeld && !_wasRightMouseHeldLastFrame)
            {
                _isHoldingMainAttackAim = true;
                _heldMainAttackAimDirection = Vector3.zero;
                _heldMainAttackTargetPoint = Vector3.zero;
                _mainAttackAimHoldStartTime = Time.time;
            }
            if (!rightMouseHeld && _wasRightMouseHeldLastFrame)
            {
                Vector3 releaseDirection = ResolvePreviewAimDirection(AimPreviewKind.MainAttack);
                bool hasManualRelease = HasMainAttackPreviewDelayElapsed() &&
                                        releaseDirection.sqrMagnitude > 0.001f;

                if (_isHoldingMainAttackAim)
                {
                    if (hasManualRelease)
                    {
                        AbilityDefinition mainAttackAbility = GetAbilityDefinition(BrawlerActionRequestType.MainAttack);
                        bool hasTargetPoint = AbilityUsesPointTarget(mainAttackAbility);
                        QueueMainAttackCommand(
                            releaseDirection,
                            hasTargetPoint
                                ? ResolveHeldTargetPoint(mainAttackAbility, releaseDirection, _heldMainAttackTargetPoint)
                                : Vector3.zero,
                            hasTargetPoint);
                    }
                    else
                    {
                        QueueAutoAimCommand(BrawlerActionRequestType.MainAttack);
                    }
                }

                _isHoldingMainAttackAim = false;
                _heldMainAttackAimDirection = Vector3.zero;
                _heldMainAttackTargetPoint = Vector3.zero;
                MarkPreviewCanceled();
            }

            _wasRightMouseHeldLastFrame = rightMouseHeld;
        }

        private Vector3 ResolveRawFallbackDirection()
        {
            if (_moveInput.sqrMagnitude >= (MoveFallbackThreshold * MoveFallbackThreshold))
                return new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;

            if (_lastAimDirection.sqrMagnitude > 0.001f)
                return _lastAimDirection;

            if (_controlledBrawler != null)
                return _controlledBrawler.transform.forward;

            return transform.forward;
        }

        private Vector3 ResolveHeldTargetPoint(
            AbilityDefinition abilityDefinition,
            Vector3 direction,
            Vector3 heldTargetPoint)
        {
            if (_controlledBrawler == null)
                return heldTargetPoint;

            if (heldTargetPoint.sqrMagnitude > 0.001f)
                return heldTargetPoint;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = ResolveStableFacingDirection();

            Vector3 fallbackPoint = _controlledBrawler.Position +
                                    direction.normalized * ResolveAimRange(abilityDefinition);
            return GetClampedTargetPointForAbility(abilityDefinition, fallbackPoint);
        }

        private bool HasPreviewDelayElapsed(AimPreviewKind kind)
        {
            switch (kind)
            {
                case AimPreviewKind.MainAttack:
                    return HasMainAttackPreviewDelayElapsed();

                case AimPreviewKind.Super:
                    return HasSuperPreviewDelayElapsed();

                default:
                    return false;
            }
        }

        private bool HasMainAttackPreviewDelayElapsed()
        {
            return _isHoldingMainAttackAim &&
                   Time.time - _mainAttackAimHoldStartTime >= AimPreviewHoldDelaySeconds;
        }

        private bool HasSuperPreviewDelayElapsed()
        {
            return _isHoldingSuperAim &&
                   Time.time - _superAimHoldStartTime >= AimPreviewHoldDelaySeconds;
        }

        private void QueueAutoAimCommand(BrawlerActionRequestType actionType)
        {
            AbilityDefinition abilityDefinition = GetAbilityDefinition(actionType);
            AimAssistResult result = ResolveAutoAim(actionType, abilityDefinition);

            Vector3 direction = result.AimDirection.sqrMagnitude > 0.001f
                ? result.AimDirection.normalized
                : ResolveStableFacingDirection();

            bool hasTargetPoint = AbilityUsesPointTarget(abilityDefinition);
            Vector3 targetPoint = Vector3.zero;

            if (hasTargetPoint)
            {
                Vector3 aimPoint = result.HasResult
                    ? result.AimPoint
                    : _controlledBrawler.Position + direction * ResolveAimRange(abilityDefinition);
                targetPoint = GetClampedTargetPointForAbility(abilityDefinition, aimPoint);
            }

            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    QueueMainAttackCommand(direction, targetPoint, hasTargetPoint);
                    break;

                case BrawlerActionRequestType.Super:
                    QueueSuperCommand(direction, targetPoint, hasTargetPoint);
                    break;
            }
        }

        private AimAssistResult ResolveAutoAim(
            BrawlerActionRequestType actionType,
            AbilityDefinition abilityDefinition)
        {
            Vector3 fallbackForward = ResolveStableFacingDirection();
            AimAssistRequest request = BuildAimAssistRequest(actionType, abilityDefinition, fallbackForward);
            AimAssistResult result = AimAssistResolver.Resolve(request);

            if (result.AimDirection.sqrMagnitude <= 0.001f)
                result.AimDirection = fallbackForward;

            return result;
        }

        private Vector3 ResolveAutoAimForwardSeed()
        {
            if (_hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
                return _manualAimDirection.normalized;

            if (_lastAimDirection.sqrMagnitude > 0.001f)
                return _lastAimDirection.normalized;

            if (_controlledBrawler != null)
                return _controlledBrawler.transform.forward;

            return transform.forward;
        }

        private void QueueMainAttackCommand(Vector3 direction, Vector3 targetPoint, bool hasTargetPoint)
        {
            direction = NormalizeFireDirection(direction);
            if (direction.sqrMagnitude <= 0.001f)
                return;

            _queuedMainAttackDirection = direction;
            _queuedMainAttackTargetPoint = targetPoint;
            _queuedMainAttackHasTargetPoint = hasTargetPoint;
            _mainAttackQueued = true;
            _lastAimDirection = direction;
        }

        private void MarkPreviewCanceled()
        {
            _previewCancelSequence++;
        }

        private void CancelPreviewState(bool markCancellation)
        {
            bool wasPreviewing = _isHoldingMainAttackAim || _isHoldingSuperAim;

            _isHoldingMainAttackAim = false;
            _heldMainAttackAimDirection = Vector3.zero;
            _heldMainAttackTargetPoint = Vector3.zero;
            _wasRightMouseHeldLastFrame = false;

            _isHoldingSuperAim = false;
            _heldSuperAimDirection = Vector3.zero;
            _heldSuperTargetPoint = Vector3.zero;
            _wasSuperKeyHeldLastFrame = false;
            _wasHyperchargeKeyHeldLastFrame = false;

            if (markCancellation && wasPreviewing)
                MarkPreviewCanceled();
        }

        private void QueueSuperCommand(Vector3 direction, Vector3 targetPoint, bool hasTargetPoint)
        {
            direction = NormalizeFireDirection(direction);
            if (direction.sqrMagnitude <= 0.001f)
                return;

            _queuedSuperDirection = direction;
            _queuedSuperTargetPoint = targetPoint;
            _queuedSuperHasTargetPoint = hasTargetPoint;
            _superQueued = true;
            _lastAimDirection = direction;
        }

        private Vector3 NormalizeFireDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = ResolveStableFacingDirection();

            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.zero;
        }

        private Vector3 ResolveStableFacingDirection()
        {
            if (_lastAimDirection.sqrMagnitude > 0.001f)
                return _lastAimDirection.normalized;

            if (_controlledBrawler != null)
                return _controlledBrawler.transform.forward;

            return transform.forward;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        public void OnAim(InputAction.CallbackContext context)
        {
            _aimInput = context.ReadValue<Vector2>();
        }

        public void OnFire(InputAction.CallbackContext context)
        {
            // Main attack uses RMB / secondary click release-to-fire flow
        }

        public void OnGadget(InputAction.CallbackContext context)
        {
            if (context.performed)
                _gadgetQueued = true;
        }

        public void OnSuper(InputAction.CallbackContext context)
        {
            // Desktop super now uses E key polling hold-release flow.
        }

        public void OnHypercharge(InputAction.CallbackContext context)
        {
            if (context.performed)
                _hyperchargeQueued = true;
        }
    }
}
