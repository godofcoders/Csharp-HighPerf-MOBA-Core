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

        private bool _hasManualAim;
        private Vector3 _manualAimDirection = Vector3.zero;

        // Brawl Stars style release-to-fire state
        private bool _isHoldingMainAttackAim;
        private bool _wasRightMouseHeldLastFrame;
        private bool _mainAttackAimWasValidDuringHold;
        private Vector3 _heldAimDirection = Vector3.zero;

        private void Awake()
        {
            _input = new GameInput();
            _input.Player.AddCallbacks(this);

            if (_controlledBrawler == null)
                _controlledBrawler = GetComponent<BrawlerController>();
        }

        private void OnEnable()
        {
            _input.Player.Enable();
        }

        private void OnDisable()
        {
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
            UpdateDesktopMainAttackAimReleaseFlow();
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
                    Tick = currentTick
                });
            }

            if (_mainAttackQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.MainAttack,
                    Direction = ResolveActionDirection(BrawlerActionRequestType.MainAttack),
                    Tick = currentTick
                });

                _mainAttackQueued = false;
            }

            if (_gadgetQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Gadget,
                    Direction = ResolveActionDirection(BrawlerActionRequestType.Gadget),
                    Tick = currentTick
                });

                _gadgetQueued = false;
            }

            if (_superQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Super,
                    Direction = ResolveActionDirection(BrawlerActionRequestType.Super),
                    Tick = currentTick
                });

                _superQueued = false;
            }

            if (_hyperchargeQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Hypercharge,
                    Direction = ResolveActionDirection(BrawlerActionRequestType.Hypercharge),
                    Tick = currentTick
                });

                _hyperchargeQueued = false;
            }
        }

        public bool HasPreviewAim()
        {
            return _isHoldingMainAttackAim && _hasManualAim;
        }

        public Vector3 GetPreviewAimDirection()
        {
            return (_isHoldingMainAttackAim && _hasManualAim) ? _manualAimDirection : Vector3.zero;
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
                if (_heldAimDirection.sqrMagnitude > 0.001f)
                {
                    _lastAimDirection = _heldAimDirection;
                    return _lastAimDirection;
                }

                if (_hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
                {
                    _lastAimDirection = _manualAimDirection;
                    return _lastAimDirection;
                }
            }

            AimAssistRequest request = BuildAimAssistRequest(actionType, abilityDefinition);
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

        private AimAssistRequest BuildAimAssistRequest(BrawlerActionRequestType actionType, AbilityDefinition abilityDefinition)
        {
            return new AimAssistRequest
            {
                Source = _controlledBrawler,
                AbilityDefinition = abilityDefinition,
                Mode = ResolveAimAssistMode(actionType, abilityDefinition),
                Origin = _controlledBrawler.Position,
                Forward = GetFireAimDirection(),
                Range = ResolveAimRange(abilityDefinition),
                IncludeSelf = ShouldIncludeSelf(actionType, abilityDefinition),
                RequireAlive = true
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

            if (abilityDefinition is ProjectileAbilityDefinition projectile)
                return projectile.Range;

            if (abilityDefinition is AoEAbilityDefinition aoe)
                return aoe.Radius;

            if (abilityDefinition is BurstSequenceProjectileAbilityDefinition burst)
                return burst.Range;

            if (abilityDefinition is ChainProjectileAbilityDefinition chain)
                return chain.Range;

            if (abilityDefinition is ThrownHybridAoEAbilityDefinition thrown)
                return thrown.ThrowRange;

            return 8f;
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

            if (_aimInput.sqrMagnitude >= (ManualAimThreshold * ManualAimThreshold))
            {
                _manualAimDirection = new Vector3(_aimInput.x, 0f, _aimInput.y).normalized;
                _hasManualAim = true;
                _lastAimDirection = _manualAimDirection;
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
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

        private void UpdateDesktopMainAttackAimReleaseFlow()
        {
            if (_controlledBrawler == null || Mouse.current == null)
                return;

            bool rightMouseHeld = Mouse.current.rightButton.isPressed;

            if (rightMouseHeld && !_wasRightMouseHeldLastFrame)
            {
                _isHoldingMainAttackAim = true;
                _mainAttackAimWasValidDuringHold = false;
                _heldAimDirection = Vector3.zero;
            }

            if (_isHoldingMainAttackAim && rightMouseHeld)
            {
                if (_hasManualAim && _manualAimDirection.sqrMagnitude > 0.001f)
                {
                    _mainAttackAimWasValidDuringHold = true;
                    _heldAimDirection = _manualAimDirection;
                }
            }

            if (!rightMouseHeld && _wasRightMouseHeldLastFrame)
            {
                bool shouldFire = _isHoldingMainAttackAim &&
                                  _mainAttackAimWasValidDuringHold &&
                                  _heldAimDirection.sqrMagnitude > 0.001f;

                if (shouldFire)
                {
                    _mainAttackQueued = true;
                    _lastAimDirection = _heldAimDirection;
                }

                _isHoldingMainAttackAim = false;
                _mainAttackAimWasValidDuringHold = false;
                _heldAimDirection = Vector3.zero;
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
        }

        public void OnGadget(InputAction.CallbackContext context)
        {
            if (context.performed)
                _gadgetQueued = true;
        }

        public void OnSuper(InputAction.CallbackContext context)
        {
            if (context.performed)
                _superQueued = true;
        }

        public void OnHypercharge(InputAction.CallbackContext context)
        {
            if (context.performed)
                _hyperchargeQueued = true;
        }
    }
}