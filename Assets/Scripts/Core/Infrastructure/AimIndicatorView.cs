using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class AimIndicatorView : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Transform _endMarker;

        private void Awake()
        {
            Hide();
        }

        public void Show(AimPreviewData data)
        {
            if (!data.IsValid)
            {
                Hide();
                return;
            }

            Vector3 endPoint = data.Origin + (data.Direction.normalized * data.Range);

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.positionCount = 2;
                _lineRenderer.SetPosition(0, data.Origin);
                _lineRenderer.SetPosition(1, endPoint);
            }

            if (_endMarker != null)
            {
                _endMarker.gameObject.SetActive(true);
                _endMarker.position = endPoint;
            }
        }

        public void Hide()
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            if (_endMarker != null)
                _endMarker.gameObject.SetActive(false);
        }
    }
}