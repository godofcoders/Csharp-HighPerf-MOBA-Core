using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Presentation-only two-bone leg IK for authored humanoid brawler models.
    /// Runs after the procedural pose layer so imported models step instead of sliding.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20000)]
    public sealed class BrawlerLegIkRuntime : MonoBehaviour
    {
        private const float MinimumBoneLength = 0.001f;

        [SerializeField] private BrawlerController _owner;
        [SerializeField] private Animator _animator;
        [SerializeField] private BrawlerAnimationRuntime _runtime;
        [SerializeField] private bool _enableLegIk = true;
        [SerializeField, Range(0f, 1f)] private float _ikWeight = 0.82f;
        [SerializeField] private float _walkStrideDistance = 0.12f;
        [SerializeField] private float _runStrideDistance = 0.26f;
        [SerializeField] private float _walkFootLift = 0.025f;
        [SerializeField] private float _runFootLift = 0.095f;
        [SerializeField] private float _sideSwayDistance = 0.025f;

        [Header("Runtime Debug")]
        [SerializeField] private float _debugMove01;
        [SerializeField] private float _debugRun01;
        [SerializeField] private float _debugIkWeight;

        private readonly LegState _leftLeg = new LegState();
        private readonly LegState _rightLeg = new LegState();
        private Transform _space;
        private bool _hasLegs;

        public static BrawlerLegIkRuntime Ensure(GameObject root, BrawlerController owner)
        {
            if (root == null)
                return null;

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
                return null;

            BrawlerLegIkRuntime runtime = root.GetComponent<BrawlerLegIkRuntime>();
            if (runtime == null)
                runtime = root.AddComponent<BrawlerLegIkRuntime>();

            runtime.Bind(owner, animator);
            return runtime;
        }

        public void Bind(BrawlerController owner, Animator animator)
        {
            _owner = owner != null ? owner : GetComponentInParent<BrawlerController>();
            _animator = animator != null ? animator : GetComponentInChildren<Animator>(true);
            _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);
            CacheLegs();
        }

        private void Awake()
        {
            if (_owner == null)
                _owner = GetComponentInParent<BrawlerController>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_runtime == null)
                _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);

            CacheLegs();
        }

        private void LateUpdate()
        {
            if (!_enableLegIk || !_hasLegs || _animator == null || !_animator.isHuman)
                return;

            if (_runtime == null)
                _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);

            if (_runtime != null && _runtime.IsDead)
                return;

            float move01 = _runtime != null ? _runtime.Move01 : 0f;
            float run01 = _runtime != null ? _runtime.Run01 : 0f;
            float stridePhase = _runtime != null ? _runtime.StridePhase : Time.unscaledTime * 5.5f;
            float activeWeight = Mathf.Clamp01(_ikWeight * Mathf.Clamp01(0.18f + move01 * 1.15f));

            _debugMove01 = move01;
            _debugRun01 = run01;
            _debugIkWeight = activeWeight;

            Vector3 moveDirection = ResolveMoveDirection();
            Vector3 poleDirection = ResolvePoleDirection(moveDirection);
            SolveLeg(_rightLeg, stridePhase, move01, run01, activeWeight, moveDirection, poleDirection);
            SolveLeg(_leftLeg, stridePhase + Mathf.PI, move01, run01, activeWeight, moveDirection, poleDirection);
        }

        private void CacheLegs()
        {
            _space = _animator != null ? _animator.transform : transform;
            CacheLeg(
                _leftLeg,
                Bone(HumanBodyBones.LeftUpperLeg),
                Bone(HumanBodyBones.LeftLowerLeg),
                Bone(HumanBodyBones.LeftFoot),
                -1f);
            CacheLeg(
                _rightLeg,
                Bone(HumanBodyBones.RightUpperLeg),
                Bone(HumanBodyBones.RightLowerLeg),
                Bone(HumanBodyBones.RightFoot),
                1f);

            _hasLegs = _leftLeg.IsValid && _rightLeg.IsValid;
        }

        private void CacheLeg(
            LegState leg,
            Transform upper,
            Transform lower,
            Transform foot,
            float side)
        {
            leg.Upper = upper;
            leg.Lower = lower;
            leg.Foot = foot;
            leg.Side = side;
            leg.IsValid = upper != null && lower != null && foot != null && _space != null;

            if (!leg.IsValid)
                return;

            leg.RestFootLocalPosition = _space.InverseTransformPoint(foot.position);
            leg.UpperLength = Mathf.Max(MinimumBoneLength, Vector3.Distance(upper.position, lower.position));
            leg.LowerLength = Mathf.Max(MinimumBoneLength, Vector3.Distance(lower.position, foot.position));
        }

        private Transform Bone(HumanBodyBones bone)
        {
            return _animator != null && _animator.isHuman
                ? _animator.GetBoneTransform(bone)
                : null;
        }

        private void SolveLeg(
            LegState leg,
            float phase,
            float move01,
            float run01,
            float activeWeight,
            Vector3 moveDirection,
            Vector3 poleDirection)
        {
            if (!leg.IsValid || activeWeight <= 0f)
                return;

            float strideSin = Mathf.Sin(phase);
            float strideCos = Mathf.Cos(phase);
            float strideDistance = Mathf.Lerp(_walkStrideDistance, _runStrideDistance, run01) * move01;
            float liftDistance = Mathf.Lerp(_walkFootLift, _runFootLift, run01) * move01;
            float footLift = Mathf.Pow(Mathf.Max(0f, strideSin), 0.72f) * liftDistance;
            float sideSway = strideCos * _sideSwayDistance * move01 * leg.Side;

            Vector3 target =
                _space.TransformPoint(leg.RestFootLocalPosition) +
                moveDirection * (strideSin * strideDistance) +
                ResolveRight(moveDirection) * sideSway +
                Vector3.up * footLift;

            SolveTwoBone(
                leg.Upper,
                leg.Lower,
                leg.Foot,
                target,
                leg.UpperLength,
                leg.LowerLength,
                poleDirection,
                activeWeight);
        }

        private Vector3 ResolveMoveDirection()
        {
            Vector3 direction = _runtime != null && _runtime.IsMoving
                ? _runtime.MoveDirection
                : (_runtime != null ? _runtime.FacingDirection : _space.forward);

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                direction = _space.forward;

            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private Vector3 ResolvePoleDirection(Vector3 moveDirection)
        {
            Vector3 direction = _runtime != null ? _runtime.FacingDirection : _space.forward;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                direction = moveDirection;

            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private static Vector3 ResolveRight(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
        }

        private static void SolveTwoBone(
            Transform upper,
            Transform lower,
            Transform tip,
            Vector3 target,
            float upperLength,
            float lowerLength,
            Vector3 poleDirection,
            float weight)
        {
            if (upper == null || lower == null || tip == null || weight <= 0f)
                return;

            Vector3 rootPosition = upper.position;
            Vector3 targetVector = target - rootPosition;
            if (targetVector.sqrMagnitude <= 0.000001f)
                return;

            float maxReach = Mathf.Max(MinimumBoneLength, upperLength + lowerLength - 0.001f);
            float targetDistance = Mathf.Clamp(targetVector.magnitude, MinimumBoneLength, maxReach);
            Vector3 targetDirection = targetVector.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(poleDirection, targetDirection);

            if (pole.sqrMagnitude <= 0.0001f)
                pole = Vector3.ProjectOnPlane(lower.position - rootPosition, targetDirection);

            if (pole.sqrMagnitude <= 0.0001f)
                pole = Vector3.ProjectOnPlane(Vector3.forward, targetDirection);

            if (pole.sqrMagnitude <= 0.0001f)
                pole = Vector3.right;

            pole.Normalize();

            float rootCos =
                ((upperLength * upperLength) + (targetDistance * targetDistance) - (lowerLength * lowerLength)) /
                (2f * upperLength * targetDistance);
            float rootAngle = Mathf.Acos(Mathf.Clamp(rootCos, -1f, 1f));
            Vector3 desiredKneeDirection =
                (targetDirection * Mathf.Cos(rootAngle) + pole * Mathf.Sin(rootAngle)).normalized;

            RotateBoneToward(upper, lower.position - rootPosition, desiredKneeDirection, weight);

            Vector3 lowerPosition = lower.position;
            Vector3 lowerTargetVector = target - lowerPosition;
            if (lowerTargetVector.sqrMagnitude <= 0.000001f)
                return;

            RotateBoneToward(lower, tip.position - lowerPosition, lowerTargetVector.normalized, weight);
        }

        private static void RotateBoneToward(
            Transform bone,
            Vector3 currentDirection,
            Vector3 desiredDirection,
            float weight)
        {
            if (bone == null ||
                currentDirection.sqrMagnitude <= 0.000001f ||
                desiredDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion delta = Quaternion.FromToRotation(currentDirection.normalized, desiredDirection.normalized);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
        }

        private sealed class LegState
        {
            public Transform Upper;
            public Transform Lower;
            public Transform Foot;
            public Vector3 RestFootLocalPosition;
            public float UpperLength;
            public float LowerLength;
            public float Side;
            public bool IsValid;
        }
    }
}
