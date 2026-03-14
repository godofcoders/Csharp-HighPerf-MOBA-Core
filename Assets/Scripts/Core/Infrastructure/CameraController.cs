using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class CameraController : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0, 10, -5);

        [Header("Smoothing")]
        [SerializeField] private float _smoothSpeed = 0.125f;
        [SerializeField] private bool _useFixedUpdate = false;

        [Header("Look Ahead (Optional)")]
        [SerializeField] private float _lookAheadDistance = 2f;
        [SerializeField] private float _lookAheadSmooth = 5f;

        private Vector3 _currentLookAhead;

        private void LateUpdate()
        {
            if (_target == null) return;

            // 1. Calculate the base target position
            Vector3 desiredPosition = _target.position + _offset;

            // 2. Add Look-Ahead logic (shifts camera based on player's forward direction)
            Vector3 lookAheadTarget = _target.forward * _lookAheadDistance;
            _currentLookAhead = Vector3.Lerp(_currentLookAhead, lookAheadTarget, Time.deltaTime * _lookAheadSmooth);

            desiredPosition += _currentLookAhead;

            // 3. Smoothly interpolate the camera position
            // Using SmoothDamp or Lerp ensures no "jitter" between simulation ticks
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed);

            transform.position = smoothedPosition;

            // 4. Always look at the player's general area
            transform.LookAt(_target.position + _currentLookAhead * 0.5f);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }
    }
}