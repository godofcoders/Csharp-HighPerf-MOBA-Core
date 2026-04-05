using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public class ProjectileVisualController : MonoBehaviour
    {
        [SerializeField] private Transform _visualRoot;

        private GameObject _currentVisualInstance;
        private ProjectilePresentationProfile _currentProfile;

        private Vector3 _spinEulerPerSecond;
        private bool _useSpin;

        private void Awake()
        {
            if (_visualRoot == null)
                _visualRoot = transform;
        }

        public void ApplyProfile(ProjectilePresentationProfile profile)
        {
            _currentProfile = profile;
            _spinEulerPerSecond = Vector3.zero;
            _useSpin = false;

            ClearVisual();

            if (_currentProfile == null || _currentProfile.VisualPrefab == null)
                return;

            _currentVisualInstance = Instantiate(_currentProfile.VisualPrefab, _visualRoot);
            _currentVisualInstance.transform.localPosition = _currentProfile.LocalPosition;
            _currentVisualInstance.transform.localRotation = Quaternion.Euler(_currentProfile.LocalRotationEuler);
            _currentVisualInstance.transform.localScale = _currentProfile.LocalScale;

            _useSpin = _currentProfile.UseSpin;
            _spinEulerPerSecond = _currentProfile.SpinEulerPerSecond;
        }

        public void TickVisual(float deltaTime)
        {
            if (_useSpin && _currentVisualInstance != null)
            {
                _currentVisualInstance.transform.Rotate(_spinEulerPerSecond * deltaTime, Space.Self);
            }
        }

        public bool ShouldFaceMovementDirection()
        {
            return _currentProfile != null && _currentProfile.FaceMovementDirection;
        }

        public void ClearVisual()
        {
            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_visualRoot.GetChild(i).gameObject);
            }

            _currentVisualInstance = null;
        }
    }
}