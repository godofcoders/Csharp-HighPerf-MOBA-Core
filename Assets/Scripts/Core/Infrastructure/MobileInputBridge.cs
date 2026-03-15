using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MOBA.Core.Infrastructure
{
    public class MobileInputBridge : MonoBehaviour
    {
        [SerializeField] private BrawlerController _targetBrawler;

        private GameInput _inputActions;
        private Vector2 _moveInput;
        private Vector2 _aimInput;

        private void Awake()
        {
            _inputActions = new GameInput();
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();

            // Subscribe to Fire (Release of the right stick usually triggers this)
            _inputActions.Player.Fire.performed += _ => BufferInput(InputCommandType.MainAttack);
            _inputActions.Player.Gadget.performed += _ => BufferInput(InputCommandType.Gadget);
            _inputActions.Player.Hypercharge.performed += _ => _targetBrawler.ActivateHypercharge();
        }

        private void OnDisable()
        {
            _inputActions.Player.Disable();
        }

        public void SetTarget(BrawlerController brawler)
        {
            _targetBrawler = brawler;
            Debug.Log($"[INPUT] Mobile Input Bridge linked to {brawler.name}");
        }

        private void Update()
        {
            // 1. Read Movement
            _moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 moveDir = new Vector3(_moveInput.x, 0, _moveInput.y);
                _targetBrawler.SetMoveInput(moveDir);
            }
            else
            {
                _targetBrawler.SetMoveInput(Vector3.zero);
            }

            // 2. Read Aiming (Visual only for now, like showing a trajectory)
            _aimInput = _inputActions.Player.Aim.ReadValue<Vector2>();
            if (_aimInput.sqrMagnitude > 0.1f)
            {
                Vector3 aimDir = new Vector3(_aimInput.x, 0, _aimInput.y);
                // We could tell the brawler to show an aiming reticle here
            }
        }

        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            Vector3 fireDir = _aimInput.sqrMagnitude > 0.1f
                ? new Vector3(_aimInput.x, 0, _aimInput.y)
                : _targetBrawler.transform.forward;

            // We send it to the BUFFER instead of executing immediately
            _targetBrawler.BufferAttack(InputCommandType.MainAttack, fireDir);
        }

        private void BufferInput(InputCommandType type)
        {
            // For Gadget and Attack, we need a direction. 
            // We use the last known aim input or the character's current forward.
            Vector2 aim = _inputActions.Player.Aim.ReadValue<Vector2>();
            Vector3 dir = aim.sqrMagnitude > 0.1f
                ? new Vector3(aim.x, 0, aim.y)
                : _targetBrawler.transform.forward;

            _targetBrawler.BufferAttack(type, dir);
        }
    }
}