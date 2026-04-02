using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MOBA.Core.Infrastructure
{
    public class PlayerCommandSource : MonoBehaviour, IBrawlerCommandSource, InputSystem_Actions.IPlayerActions
    {
        [SerializeField] private BrawlerController _controlledBrawler;

        private InputSystem_Actions _input;
        private Vector2 _moveInput;
        private Vector2 _aimInput;

        private bool _mainAttackQueued;
        private bool _gadgetQueued;
        private bool _superQueued;
        private bool _hyperchargeQueued;

        private Vector3 _lastAimDirection = Vector3.forward;

        private const float ManualAimThreshold = 0.20f;
        private const float MoveFallbackThreshold = 0.20f;

        private void Awake()
        {
            _input = new InputSystem_Actions();
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

        private Vector3 ResolveActionDirection(BrawlerActionRequestType actionType)
        {
            if (_controlledBrawler == null)
                return ResolveRawFallbackDirection();

            AbilityDefinition abilityDefinition = GetAbilityDefinition(actionType);
            bool preferManualAim = abilityDefinition == null || abilityDefinition.PreferManualAim;

            Vector3 manualDirection = ResolveManualAimDirection();
            if (preferManualAim && manualDirection.sqrMagnitude > 0.001f)
            {
                _lastAimDirection = manualDirection;
                return _lastAimDirection;
            }

            AimAssistRequest request = BuildAimAssistRequest(actionType, abilityDefinition);
            AimAssistResult result = AimAssistResolver.Resolve(request);

            if (result.HasResult && result.AimDirection.sqrMagnitude > 0.001f)
            {
                _lastAimDirection = result.AimDirection.normalized;
                return _lastAimDirection;
            }

            Vector3 fallbackDirection = ResolveRawFallbackDirection();
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
                Forward = ResolveRawFallbackDirection(),
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

            return 8f;
        }

        private bool ShouldIncludeSelf(BrawlerActionRequestType actionType, AbilityDefinition abilityDefinition)
        {
            if (abilityDefinition != null)
                return abilityDefinition.AimAssistIncludeSelf;

            return actionType == BrawlerActionRequestType.Hypercharge;
        }

        private Vector3 ResolveManualAimDirection()
        {
            if (_aimInput.sqrMagnitude >= (ManualAimThreshold * ManualAimThreshold))
                return new Vector3(_aimInput.x, 0f, _aimInput.y).normalized;

            return Vector3.zero;
        }

        private Vector3 ResolveRawFallbackDirection()
        {
            if (_aimInput.sqrMagnitude >= (ManualAimThreshold * ManualAimThreshold))
                return new Vector3(_aimInput.x, 0f, _aimInput.y).normalized;

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
            Debug.Log($"OnFire called. performed={context.performed}");
            if (context.performed)
                _mainAttackQueued = true;
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