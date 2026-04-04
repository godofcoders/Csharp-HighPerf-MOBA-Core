using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerPresentationAnchors : MonoBehaviour
    {
        [SerializeField] private Transform _primaryFirePoint;
        [SerializeField] private Transform _secondaryFirePoint;
        [SerializeField] private Transform _castPoint;

        public Transform PrimaryFirePoint => _primaryFirePoint;
        public Transform SecondaryFirePoint => _secondaryFirePoint;
        public Transform CastPoint => _castPoint;

        public Vector3 GetPrimaryFirePosition(Transform fallback)
        {
            return _primaryFirePoint != null ? _primaryFirePoint.position : fallback.position;
        }

        public Vector3 GetSecondaryFirePosition(Transform fallback)
        {
            return _secondaryFirePoint != null ? _secondaryFirePoint.position : GetPrimaryFirePosition(fallback);
        }

        public Vector3 GetCastPosition(Transform fallback)
        {
            if (_castPoint != null)
                return _castPoint.position;

            if (_primaryFirePoint != null)
                return _primaryFirePoint.position;

            return fallback.position;
        }
    }
}