using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    [DefaultExecutionOrder(50)]
    public sealed class BrawlerAttachmentFollower : MonoBehaviour
    {
        [SerializeField] private Transform _socket;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Vector3 _localPositionOffset;
        [SerializeField] private Vector3 _localEulerOffset;
        [SerializeField] private bool _useStableCharacterRotation;
        [SerializeField] private bool _alignGripPoint;
        [SerializeField] private Vector3 _gripLocalPosition;
        [SerializeField] private Quaternion _gripLocalRotation = Quaternion.identity;

        public void Configure(
            Transform socket,
            Transform characterRoot,
            Vector3 localPositionOffset,
            Vector3 localEulerOffset,
            bool useStableCharacterRotation,
            bool alignGripPoint,
            Vector3 gripLocalPosition,
            Quaternion gripLocalRotation)
        {
            _socket = socket;
            _characterRoot = characterRoot;
            _localPositionOffset = localPositionOffset;
            _localEulerOffset = localEulerOffset;
            _useStableCharacterRotation = useStableCharacterRotation;
            _alignGripPoint = alignGripPoint;
            _gripLocalPosition = gripLocalPosition;
            _gripLocalRotation = gripLocalRotation;
            ApplyNow();
        }

        private void LateUpdate()
        {
            ApplyNow();
        }

        public void ApplyNow()
        {
            if (_socket == null)
                return;

            Quaternion rotationFrame = _useStableCharacterRotation
                ? ResolveCharacterFrame()
                : _socket.rotation;
            Quaternion targetRotation = rotationFrame * Quaternion.Euler(_localEulerOffset);
            Vector3 targetPosition = _socket.TransformPoint(_localPositionOffset);

            if (_alignGripPoint)
            {
                transform.rotation = targetRotation * Quaternion.Inverse(_gripLocalRotation);
                Vector3 gripWorldPosition = transform.TransformPoint(_gripLocalPosition);
                transform.position += targetPosition - gripWorldPosition;
                return;
            }

            transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private Quaternion ResolveCharacterFrame()
        {
            Transform root = _characterRoot != null ? _characterRoot : _socket;
            Vector3 forward = root.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
