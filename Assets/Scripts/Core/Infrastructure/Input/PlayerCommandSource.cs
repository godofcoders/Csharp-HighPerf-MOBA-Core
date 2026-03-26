using System.Collections.Generic;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MOBA.Core.Infrastructure
{
    public class PlayerCommandSource : MonoBehaviour, IBrawlerCommandSource, GameInput.IPlayerActions
    {
        private GameInput _input;
        private Vector2 _moveInput;
        private Vector2 _aimInput;

        private bool _mainAttackQueued;
        private bool _gadgetQueued;
        private bool _superQueued;
        private bool _hyperchargeQueued;

        private void Awake()
        {
            _input = new GameInput();
            _input.Player.AddCallbacks(this);
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

            Vector3 aimDirection = ResolveAimDirection();

            if (_mainAttackQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.MainAttack,
                    Direction = aimDirection,
                    Tick = currentTick
                });
                _mainAttackQueued = false;
            }

            if (_gadgetQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Gadget,
                    Direction = aimDirection,
                    Tick = currentTick
                });
                _gadgetQueued = false;
            }

            if (_superQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Super,
                    Direction = aimDirection,
                    Tick = currentTick
                });
                _superQueued = false;
            }

            if (_hyperchargeQueued)
            {
                output.Add(new BrawlerCommand
                {
                    Type = BrawlerCommandType.Hypercharge,
                    Direction = aimDirection,
                    Tick = currentTick
                });
                _hyperchargeQueued = false;
            }
        }

        private Vector3 ResolveAimDirection()
        {
            if (_aimInput.sqrMagnitude > 0.01f)
                return new Vector3(_aimInput.x, 0f, _aimInput.y).normalized;

            if (_moveInput.sqrMagnitude > 0.01f)
                return new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;

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