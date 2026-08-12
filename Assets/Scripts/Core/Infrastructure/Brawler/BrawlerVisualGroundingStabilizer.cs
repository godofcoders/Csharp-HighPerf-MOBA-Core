using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Presentation-only grounding for authored humanoid models after pose and IK adjustments.
    /// Simulation movement stays untouched; this only removes visible foot hover.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(21000)]
    public sealed class BrawlerVisualGroundingStabilizer : MonoBehaviour
    {
        private const float BoundsEpsilon = 0.0001f;

        [SerializeField] private Transform _groundSpace;
        [SerializeField] private bool _enableGrounding = true;
        [SerializeField] private float _groundLocalY = 0f;
        [SerializeField] private float _correctionSmoothSpeed = 28f;
        [SerializeField] private float _maxGroundCorrection = 0.28f;

        [Header("Runtime Debug")]
        [SerializeField] private float _debugBottomLocalY;
        [SerializeField] private float _debugCorrectionY;
        [SerializeField] private int _debugRendererCount;

        private readonly List<Renderer> _bodyRenderers = new List<Renderer>(16);
        private Vector3 _baseLocalPosition;
        private bool _hasBasePosition;

        public static BrawlerVisualGroundingStabilizer Ensure(
            GameObject root,
            Transform groundSpace)
        {
            if (root == null || !HasHumanoidAnimator(root))
                return null;

            BrawlerVisualGroundingStabilizer stabilizer =
                root.GetComponent<BrawlerVisualGroundingStabilizer>();
            if (stabilizer == null)
                stabilizer = root.AddComponent<BrawlerVisualGroundingStabilizer>();

            stabilizer.Bind(groundSpace);
            return stabilizer;
        }

        public void Bind(Transform groundSpace)
        {
            _groundSpace = groundSpace != null ? groundSpace : transform.parent;
            _baseLocalPosition = transform.localPosition;
            _hasBasePosition = true;
            CacheBodyRenderers();
        }

        public void RefreshRenderers()
        {
            CacheBodyRenderers();
        }

        private void Awake()
        {
            if (_groundSpace == null)
                _groundSpace = transform.parent;

            _baseLocalPosition = transform.localPosition;
            _hasBasePosition = true;
            CacheBodyRenderers();
        }

        private void LateUpdate()
        {
            if (!_enableGrounding || _groundSpace == null || _bodyRenderers.Count == 0)
                return;

            if (!_hasBasePosition)
            {
                _baseLocalPosition = transform.localPosition;
                _hasBasePosition = true;
            }

            Bounds bounds;
            if (!TryCalculateBodyBounds(out bounds))
                return;

            float bottomLocalY = _groundSpace.InverseTransformPoint(bounds.min).y;
            float targetOffsetY =
                Mathf.Clamp(
                    _groundLocalY - bottomLocalY,
                    -_maxGroundCorrection,
                    _maxGroundCorrection);
            Vector3 desiredLocalPosition =
                transform.localPosition + Vector3.up * targetOffsetY;
            float deltaTime = Time.deltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            float blend = Mathf.Clamp01(deltaTime * _correctionSmoothSpeed);
            transform.localPosition =
                Vector3.Lerp(transform.localPosition, desiredLocalPosition, blend);

            _debugBottomLocalY = bottomLocalY;
            _debugCorrectionY = transform.localPosition.y - _baseLocalPosition.y;
            _debugRendererCount = _bodyRenderers.Count;
        }

        private void CacheBodyRenderers()
        {
            _bodyRenderers.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || ShouldIgnoreRenderer(renderer.transform))
                    continue;

                _bodyRenderers.Add(renderer);
            }

            _debugRendererCount = _bodyRenderers.Count;
        }

        private bool TryCalculateBodyBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;

            for (int i = _bodyRenderers.Count - 1; i >= 0; i--)
            {
                Renderer renderer = _bodyRenderers[i];
                if (renderer == null)
                {
                    _bodyRenderers.RemoveAt(i);
                    continue;
                }

                if (!renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    ShouldIgnoreRenderer(renderer.transform))
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= BoundsEpsilon)
                    continue;

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        private static bool HasHumanoidAnimator(GameObject root)
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            return animator != null && animator.isHuman;
        }

        private static bool ShouldIgnoreRenderer(Transform candidate)
        {
            Transform current = candidate;
            while (current != null)
            {
                string name = current.name;
                if (name.StartsWith("RuntimeAttachments") ||
                    name.StartsWith("Attachment_") ||
                    name.StartsWith("AttachmentSockets") ||
                    name.StartsWith("Sockets") ||
                    name.StartsWith("Weapon_") ||
                    name.StartsWith("Muzzle_") ||
                    name.StartsWith("PresentationAnchor") ||
                    name.StartsWith("GripPoint") ||
                    name.StartsWith("HoldPoint"))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
