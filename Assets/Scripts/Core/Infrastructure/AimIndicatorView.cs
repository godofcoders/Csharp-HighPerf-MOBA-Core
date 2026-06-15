using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class AimIndicatorView : MonoBehaviour
    {
        [Header("Directional")]
        [SerializeField] private LineRenderer _lineRenderer;
        [Tooltip("Optional left edge line for spread-fan abilities. Active only when AimPreviewData.SpreadHalfAngleDegrees > 0.")]
        [SerializeField] private LineRenderer _spreadLeftLine;
        [Tooltip("Optional right edge line for spread-fan abilities. Active only when AimPreviewData.SpreadHalfAngleDegrees > 0.")]
        [SerializeField] private LineRenderer _spreadRightLine;

        [Header("Style")]
        [SerializeField] private Color _mainAttackColor = new Color(0.24f, 0.78f, 1f, 0.88f);
        [SerializeField] private Color _superColor = new Color(1f, 0.38f, 0.14f, 0.94f);
        [SerializeField] private Color _blockedColor = new Color(1f, 0.18f, 0.12f, 0.92f);
        [SerializeField] private float _fallbackDirectionalWidth = 0.8f;
        [SerializeField] private float _spreadEdgeWidth = 0.08f;

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

        private void OnDisable()
        {
            HideAll();
        }

        private void OnDestroy()
        {
            HideAll();
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

            Vector3 dir = data.Direction.normalized;
            Vector3 endPoint = data.Origin + (dir * data.Range);
            Color color = ResolveColor(data);
            float width = Mathf.Max(0.05f, data.Width > 0f ? data.Width : _fallbackDirectionalWidth);

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.positionCount = 2;
                _lineRenderer.widthMultiplier = width;
                ApplyColor(_lineRenderer, color);
                _lineRenderer.SetPosition(0, data.Origin);
                _lineRenderer.SetPosition(1, endPoint);
            }

            // Spread fan: render boundary lines at ±SpreadHalfAngleDegrees
            // around the centre direction. Rotation is in the XZ plane
            // (around world up) which matches the top-down camera. Lines
            // share the origin; their endpoints sit on a circular arc at
            // distance Range.
            if (data.SpreadHalfAngleDegrees > 0.01f)
            {
                Quaternion leftRot  = Quaternion.AngleAxis(-data.SpreadHalfAngleDegrees, Vector3.up);
                Quaternion rightRot = Quaternion.AngleAxis( data.SpreadHalfAngleDegrees, Vector3.up);

                Vector3 leftEnd  = data.Origin + (leftRot  * dir) * data.Range;
                Vector3 rightEnd = data.Origin + (rightRot * dir) * data.Range;

                SetEdgeLine(_spreadLeftLine, data.Origin, leftEnd, color);
                SetEdgeLine(_spreadRightLine, data.Origin, rightEnd, color);
            }

            // Directional previews should read like a lane/beam, not a point target.
            // Throwable and placement previews still own the endpoint marker below.
        }

        private void SetEdgeLine(LineRenderer lr, Vector3 a, Vector3 b, Color color)
        {
            if (lr == null) return;
            lr.enabled = true;
            lr.positionCount = 2;
            lr.widthMultiplier = Mathf.Max(0.02f, _spreadEdgeWidth);
            ApplyColor(lr, color);
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }

        private void ShowThrowable(AimPreviewData data)
        {
            HideAll();
            Color color = ResolveColor(data);

            if (_arcRenderer != null)
            {
                _arcRenderer.enabled = true;
                _arcRenderer.positionCount = _arcSegments + 1;
                ApplyColor(_arcRenderer, color);

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
                DrawRadiusRing(data.TargetPoint, data.Radius, color);
            }
        }

        private void ShowPlacement(AimPreviewData data)
        {
            HideAll();
            Color color = ResolveColor(data);

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.positionCount = 2;
                _lineRenderer.widthMultiplier = Mathf.Max(0.05f, data.Width > 0f ? data.Width : _fallbackDirectionalWidth);
                ApplyColor(_lineRenderer, color);
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
                DrawRadiusRing(data.TargetPoint, data.Radius, color);
            }
        }

        private Vector3 EvaluateArc(Vector3 start, Vector3 end, float arcHeight, float t)
        {
            Vector3 basePos = Vector3.Lerp(start, end, t);
            float arcOffset = 4f * arcHeight * t * (1f - t);
            return basePos + Vector3.up * arcOffset;
        }

        private void DrawRadiusRing(Vector3 center, float radius, Color color)
        {
            if (_radiusRingRenderer == null)
                return;

            _radiusRingRenderer.enabled = true;
            _radiusRingRenderer.positionCount = _ringSegments + 1;
            ApplyColor(_radiusRingRenderer, color);

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

        private Color ResolveColor(AimPreviewData data)
        {
            if (data.IsObstructed)
                return _blockedColor;

            return data.Kind == AimPreviewKind.Super
                ? _superColor
                : _mainAttackColor;
        }

        private void ApplyColor(LineRenderer renderer, Color color)
        {
            if (renderer == null)
                return;

            renderer.startColor = color;
            renderer.endColor = color;
        }

        public void Hide()
        {
            HideAll();
        }

        private void HideAll()
        {
            if (_lineRenderer != null)
                _lineRenderer.enabled = false;

            if (_spreadLeftLine != null)
                _spreadLeftLine.enabled = false;

            if (_spreadRightLine != null)
                _spreadRightLine.enabled = false;

            if (_arcRenderer != null)
                _arcRenderer.enabled = false;

            if (_radiusRingRenderer != null)
                _radiusRingRenderer.enabled = false;

            if (_endMarker != null)
                _endMarker.gameObject.SetActive(false);
        }
    }
}
