using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class CameraController : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0, 10, -5);

        [Header("Smoothing")]
        [SerializeField] private float _positionSmoothTime = 0.18f;

        private Vector3 _positionVelocity;

        private void LateUpdate()
        {
            if (_target == null)
                return;

            Vector3 desiredPosition = _target.position + _offset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _positionVelocity,
                _positionSmoothTime);

            transform.LookAt(_target.position);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }
    }
}