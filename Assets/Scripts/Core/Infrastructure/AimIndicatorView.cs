using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class AimIndicatorView : MonoBehaviour
    {
        [Header("Directional")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Header("Throwable / Placement")]
        [SerializeField] private LineRenderer _arcRenderer;
        [SerializeField] private Transform _endMarker;
        [SerializeField] private LineRenderer _radiusRingRenderer;

        [Header("Arc")]
        [SerializeField] private int _arcSegments = 16;

        [Header("Radius Ring")]
        [SerializeField] private int _ringSegments = 32;

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

            switch (data.Mode)
            {
                case AimPreviewMode.Directional:
                    ShowDirectional(data);
                    break;

                case AimPreviewMode.Throwable:
                    ShowThrowable(data);
                    break;

                case AimPreviewMode.Placement:
                    ShowPlacement(data);
                    break;

                default:
                    Hide();
                    break;
            }
        }

        private void ShowDirectional(AimPreviewData data)
        {
            HideAll();

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

        private void ShowThrowable(AimPreviewData data)
        {
            HideAll();

            if (_arcRenderer != null)
            {
                _arcRenderer.enabled = true;
                _arcRenderer.positionCount = _arcSegments + 1;

                for (int i = 0; i <= _arcSegments; i++)
                {
                    float t = i / (float)_arcSegments;
                    Vector3 point = EvaluateArc(data.Origin, data.TargetPoint, data.ArcHeight, t);
                    _arcRenderer.SetPosition(i, point);
                }
            }

            if (_endMarker != null)
            {
                _endMarker.gameObject.SetActive(true);
                _endMarker.position = data.TargetPoint;
            }

            if (_radiusRingRenderer != null && data.Radius > 0.01f)
            {
                DrawRadiusRing(data.TargetPoint, data.Radius);
            }
        }

        private void ShowPlacement(AimPreviewData data)
        {
            HideAll();

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.positionCount = 2;
                _lineRenderer.SetPosition(0, data.Origin);
                _lineRenderer.SetPosition(1, data.TargetPoint);
            }

            if (_endMarker != null)
            {
                _endMarker.gameObject.SetActive(true);
                _endMarker.position = data.TargetPoint;
            }

            if (_radiusRingRenderer != null && data.Radius > 0.01f)
            {
                DrawRadiusRing(data.TargetPoint, data.Radius);
            }
        }

        private Vector3 EvaluateArc(Vector3 start, Vector3 end, float arcHeight, float t)
        {
            Vector3 basePos = Vector3.Lerp(start, end, t);
            float arcOffset = 4f * arcHeight * t * (1f - t);
            return basePos + Vector3.up * arcOffset;
        }

        private void DrawRadiusRing(Vector3 center, float radius)
        {
            if (_radiusRingRenderer == null)
                return;

            _radiusRingRenderer.enabled = true;
            _radiusRingRenderer.positionCount = _ringSegments + 1;

            for (int i = 0; i <= _ringSegments; i++)
            {
                float t = i / (float)_ringSegments;
                float angle = t * Mathf.PI * 2f;

                Vector3 point = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.02f,
                    Mathf.Sin(angle) * radius
                );

                _radiusRingRenderer.SetPosition(i, point);
            }
        }

        public void Hide()
        {
            HideAll();
        }

        private void HideAll()
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            if (_arcRenderer != null)
                _arcRenderer.enabled = false;

            if (_radiusRingRenderer != null)
                _radiusRingRenderer.enabled = false;

            if (_endMarker != null)
                _endMarker.gameObject.SetActive(false);
        }
    }
}