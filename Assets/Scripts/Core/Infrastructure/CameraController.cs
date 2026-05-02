using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Top-down follow camera. SmoothDamp follow + dead zone + Shake hook.
    ///
    /// Dead zone: a horizontal box around the camera's current focus point
    /// in world space. As long as the target stays inside the box, the
    /// camera holds still — only when the target leaves the box does the
    /// follow re-engage. Reduces micro-jitter on small player movements
    /// and gives the camera a slight "anchor" feel.
    ///
    /// Shake: an additive noise offset on top of the smoothed follow
    /// position. Magnitude decays linearly over the supplied duration.
    /// Designed for game-feel work in plan Sessions 19–20: hit stop on
    /// damage, screen kick on super-cast, etc.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        // Singleton so game-feel callers (damage handlers, super-cast, etc.)
        // can do CameraController.Instance?.Shake(...) without holding a
        // serialized reference. Null-safe; non-CameraController scenes (e.g.
        // editmode tests) just no-op.
        public static CameraController Instance { get; private set; }

        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0, 10, -5);

        // Bumped from 0.05f → 0.15f. 0.05 tracks every micro-movement and
        // produces visible jitter, especially when the target moves on a
        // FixedUpdate/Rigidbody pulse and the camera reads in LateUpdate
        // (camera sees the same position for several frames, then jumps).
        // 0.15 is enough to absorb sub-frame desync without feeling laggy.
        // Designer can tune lower per-scene; this is the safe default.
        //
        // Upstream tip: if jitter persists at 0.15, set
        // Rigidbody.Interpolation = Interpolate on the brawler in the
        // inspector — that's Unity's built-in fix for physics-vs-render
        // desync.
        [SerializeField] private float _positionSmoothTime = 0.15f;

        [Header("Dead Zone")]
        [Tooltip("Half-extents of the dead zone box on the XZ plane (world units). Target moves within this box before the camera re-engages follow.")]
        [SerializeField] private Vector2 _deadZoneHalfExtents = new Vector2(0.5f, 0.5f);

        // The "anchor" — where the target was last seen at the centre of the
        // dead zone. Camera follow targets `_anchor + _offset`, NOT
        // `_target.position + _offset`, until the target leaves the box.
        private Vector3 _anchor;
        private bool _anchorInitialized;
        private Vector3 _positionVelocity;

        // Active shake state.
        private float _shakeMagnitude;
        private float _shakeRemainingSeconds;
        private float _shakeDurationSeconds;

        // Cached fixed rotation. Computed once from _offset because for a
        // fixed-offset top-down camera the look direction never changes —
        // recomputing via LookAt every frame just amplifies any positional
        // jitter into rotational jitter.
        private Quaternion _fixedRotation;
        private bool _rotationCached;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            UpdateAnchor();

            Vector3 desiredPosition = _anchor + _offset + ComputeShakeOffset();

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _positionVelocity,
                _positionSmoothTime);

            // Set rotation once: with a fixed offset the look direction
            // (-offset.normalized) doesn't change. LookAt every frame would
            // re-derive this from current camera position, which jitters
            // because SmoothDamp doesn't reach the desired position
            // instantly — so each frame's "look back at anchor" produces a
            // slightly different rotation. Cache once, ship.
            if (!_rotationCached)
            {
                _fixedRotation = Quaternion.LookRotation(-_offset.normalized, Vector3.up);
                transform.rotation = _fixedRotation;
                _rotationCached = true;
            }
        }

        private void UpdateAnchor()
        {
            Vector3 targetPos = _target.position;

            if (!_anchorInitialized)
            {
                _anchor = targetPos;
                _anchorInitialized = true;
                return;
            }

            // If target is outside the dead zone on either axis, drag the
            // anchor toward it just enough to put the target back at the
            // edge of the box. This keeps the target in-frame without
            // making the camera lurch.
            Vector3 delta = targetPos - _anchor;

            float dx = delta.x;
            if (dx > _deadZoneHalfExtents.x) _anchor.x += dx - _deadZoneHalfExtents.x;
            else if (dx < -_deadZoneHalfExtents.x) _anchor.x += dx + _deadZoneHalfExtents.x;

            float dz = delta.z;
            if (dz > _deadZoneHalfExtents.y) _anchor.z += dz - _deadZoneHalfExtents.y;
            else if (dz < -_deadZoneHalfExtents.y) _anchor.z += dz + _deadZoneHalfExtents.y;

            // Y always tracks the target — vertical movement is rare in a
            // top-down arena and the dead zone is XZ-only.
            _anchor.y = targetPos.y;
        }

        private Vector3 ComputeShakeOffset()
        {
            if (_shakeRemainingSeconds <= 0f || _shakeMagnitude <= 0f)
                return Vector3.zero;

            _shakeRemainingSeconds -= Time.deltaTime;
            if (_shakeRemainingSeconds <= 0f)
            {
                _shakeRemainingSeconds = 0f;
                return Vector3.zero;
            }

            // Linear decay: at start, full magnitude; at end, zero.
            float t = _shakeRemainingSeconds / _shakeDurationSeconds;
            float currentMag = _shakeMagnitude * t;

            // Random offset on the XZ plane. Random.insideUnitCircle is
            // non-deterministic, but camera shake is purely visual and
            // doesn't affect simulation state, so this is safe even with
            // Phase 2's fixed-point determinism plan.
            Vector2 noise = Random.insideUnitCircle * currentMag;
            return new Vector3(noise.x, 0f, noise.y);
        }

        /// <summary>Apply a screen shake. <paramref name="magnitude"/> is in
        /// world units (camera offset peaks at this value at t=0); decays
        /// linearly over <paramref name="durationSeconds"/>. Calling again
        /// before the previous shake ends overrides — useful so the
        /// strongest in-flight event wins.</summary>
        public void Shake(float magnitude, float durationSeconds)
        {
            if (magnitude <= 0f || durationSeconds <= 0f)
                return;

            // Override-vs-stack: if the new event is stronger, take it; if
            // weaker, keep the existing shake. Stops a flurry of small hits
            // from cancelling a big "got hit by super" kick.
            if (magnitude < _shakeMagnitude && _shakeRemainingSeconds > 0f)
                return;

            _shakeMagnitude = magnitude;
            _shakeDurationSeconds = durationSeconds;
            _shakeRemainingSeconds = durationSeconds;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _anchorInitialized = false;
        }
    }
}
