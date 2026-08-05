using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Lightweight presentation pose layer for imported humanoid brawler models.
    /// Gameplay still owns movement; this only bends the authored skeleton out of raw import pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BrawlerAuthoredModelAnimator : MonoBehaviour
    {
        [SerializeField] private BrawlerController _owner;
        [SerializeField] private Animator _animator;
        [SerializeField] private BrawlerAnimationRuntime _runtime;
        [SerializeField] private float _poseWeight = 1f;

        private Transform _hips;
        private Transform _spine;
        private Transform _chest;
        private Transform _head;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;
        private Transform _leftUpperLeg;
        private Transform _leftLowerLeg;
        private Transform _leftFoot;
        private Transform _rightUpperLeg;
        private Transform _rightLowerLeg;
        private Transform _rightFoot;

        private Quaternion _hipsBase;
        private Quaternion _spineBase;
        private Quaternion _chestBase;
        private Quaternion _headBase;
        private Quaternion _leftUpperArmBase;
        private Quaternion _leftLowerArmBase;
        private Quaternion _leftHandBase;
        private Quaternion _rightUpperArmBase;
        private Quaternion _rightLowerArmBase;
        private Quaternion _rightHandBase;
        private Quaternion _leftUpperLegBase;
        private Quaternion _leftLowerLegBase;
        private Quaternion _leftFootBase;
        private Quaternion _rightUpperLegBase;
        private Quaternion _rightLowerLegBase;
        private Quaternion _rightFootBase;
        private bool _hasBasePose;

        public static BrawlerAuthoredModelAnimator Ensure(GameObject root, BrawlerController owner)
        {
            if (root == null)
                return null;

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
                return null;

            BrawlerAuthoredModelAnimator pose =
                root.GetComponent<BrawlerAuthoredModelAnimator>();
            if (pose == null)
                pose = root.AddComponent<BrawlerAuthoredModelAnimator>();

            pose.Bind(owner, animator);
            return pose;
        }

        public void Bind(BrawlerController owner, Animator animator)
        {
            _owner = owner != null ? owner : GetComponentInParent<BrawlerController>();
            _animator = animator != null ? animator : GetComponentInChildren<Animator>(true);
            _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);
            CacheBones();
            CaptureBasePose();
        }

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_owner == null)
                _owner = GetComponentInParent<BrawlerController>();

            if (_runtime == null)
                _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);

            CacheBones();
            CaptureBasePose();
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || !_hasBasePose)
                return;

            ResetToBasePose();
            ApplyPresentationPose();
        }

        private void CacheBones()
        {
            if (_animator == null || !_animator.isHuman)
                return;

            _hips = Bone(HumanBodyBones.Hips);
            _spine = Bone(HumanBodyBones.Spine);
            _chest = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.UpperChest);
            _head = Bone(HumanBodyBones.Head);
            _leftUpperArm = Bone(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = Bone(HumanBodyBones.LeftLowerArm);
            _leftHand = Bone(HumanBodyBones.LeftHand);
            _rightUpperArm = Bone(HumanBodyBones.RightUpperArm);
            _rightLowerArm = Bone(HumanBodyBones.RightLowerArm);
            _rightHand = Bone(HumanBodyBones.RightHand);
            _leftUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
            _leftLowerLeg = Bone(HumanBodyBones.LeftLowerLeg);
            _leftFoot = Bone(HumanBodyBones.LeftFoot);
            _rightUpperLeg = Bone(HumanBodyBones.RightUpperLeg);
            _rightLowerLeg = Bone(HumanBodyBones.RightLowerLeg);
            _rightFoot = Bone(HumanBodyBones.RightFoot);
        }

        private Transform Bone(HumanBodyBones bone)
        {
            return _animator != null && _animator.isHuman
                ? _animator.GetBoneTransform(bone)
                : null;
        }

        private void CaptureBasePose()
        {
            if (_animator == null || !_animator.isHuman)
                return;

            _hipsBase = LocalRotation(_hips);
            _spineBase = LocalRotation(_spine);
            _chestBase = LocalRotation(_chest);
            _headBase = LocalRotation(_head);
            _leftUpperArmBase = LocalRotation(_leftUpperArm);
            _leftLowerArmBase = LocalRotation(_leftLowerArm);
            _leftHandBase = LocalRotation(_leftHand);
            _rightUpperArmBase = LocalRotation(_rightUpperArm);
            _rightLowerArmBase = LocalRotation(_rightLowerArm);
            _rightHandBase = LocalRotation(_rightHand);
            _leftUpperLegBase = LocalRotation(_leftUpperLeg);
            _leftLowerLegBase = LocalRotation(_leftLowerLeg);
            _leftFootBase = LocalRotation(_leftFoot);
            _rightUpperLegBase = LocalRotation(_rightUpperLeg);
            _rightLowerLegBase = LocalRotation(_rightLowerLeg);
            _rightFootBase = LocalRotation(_rightFoot);
            _hasBasePose = true;
        }

        private static Quaternion LocalRotation(Transform bone)
        {
            return bone != null ? bone.localRotation : Quaternion.identity;
        }

        private void ResetToBasePose()
        {
            SetLocalRotation(_hips, _hipsBase);
            SetLocalRotation(_spine, _spineBase);
            SetLocalRotation(_chest, _chestBase);
            SetLocalRotation(_head, _headBase);
            SetLocalRotation(_leftUpperArm, _leftUpperArmBase);
            SetLocalRotation(_leftLowerArm, _leftLowerArmBase);
            SetLocalRotation(_leftHand, _leftHandBase);
            SetLocalRotation(_rightUpperArm, _rightUpperArmBase);
            SetLocalRotation(_rightLowerArm, _rightLowerArmBase);
            SetLocalRotation(_rightHand, _rightHandBase);
            SetLocalRotation(_leftUpperLeg, _leftUpperLegBase);
            SetLocalRotation(_leftLowerLeg, _leftLowerLegBase);
            SetLocalRotation(_leftFoot, _leftFootBase);
            SetLocalRotation(_rightUpperLeg, _rightUpperLegBase);
            SetLocalRotation(_rightLowerLeg, _rightLowerLegBase);
            SetLocalRotation(_rightFoot, _rightFootBase);
        }

        private static void SetLocalRotation(Transform bone, Quaternion rotation)
        {
            if (bone != null)
                bone.localRotation = rotation;
        }

        private void ApplyPresentationPose()
        {
            float weight = Mathf.Clamp01(_poseWeight);
            float time = _runtime != null ? _runtime.PoseTime : Time.unscaledTime;
            float move01 = _runtime != null ? _runtime.Move01 : 0f;
            float run01 = _runtime != null ? _runtime.Run01 : 0f;
            float action = _runtime != null ? _runtime.ActionWeight : 0f;
            float attack = _runtime != null ? _runtime.MainAttackWeight : 0f;
            float super = _runtime != null ? _runtime.SuperWeight : 0f;
            float hit = _runtime != null ? _runtime.HitReactWeight : 0f;
            float hyper = _runtime != null ? _runtime.HyperchargeWeight : 0f;
            float ready = Mathf.Clamp01(0.35f + action * 0.9f + move01 * 0.25f);
            float stride = time * Mathf.Lerp(5.5f, 10.5f, run01);
            float strideSin = Mathf.Sin(stride);
            float strideCos = Mathf.Cos(stride);
            float idleBreath = Mathf.Sin(time * 1.7f) * (1f - move01);
            Vector3 forward = ResolveForward();
            Vector3 right = ResolveRight(forward);
            Vector3 up = Vector3.up;
            Vector3 aim = ResolveAimDirection(forward, Mathf.Clamp01(action + attack + super));

            AddLocal(_hips, _hipsBase, idleBreath * 1.4f - move01 * 4.0f, 0f, strideCos * move01 * 2.2f, weight);
            AddLocal(_spine, _spineBase, idleBreath * -1.2f + move01 * 3.0f - hit * 6.0f, 0f, -strideCos * move01 * 2.0f, weight);
            AddLocal(_chest, _chestBase, idleBreath * -0.8f + super * -4.0f, 0f, strideCos * move01 * 1.4f + hyper * 2.0f, weight);
            AddLocal(_head, _headBase, idleBreath * 0.9f - attack * 2.5f, 0f, -strideCos * move01 * 0.8f, weight);

            PoseArm(
                _rightUpperArm,
                _rightLowerArm,
                _rightHand,
                1f,
                ready,
                attack,
                super,
                strideSin,
                move01,
                forward,
                right,
                up,
                aim,
                weight);
            PoseArm(
                _leftUpperArm,
                _leftLowerArm,
                _leftHand,
                -1f,
                ready,
                attack * 0.65f,
                super,
                -strideSin,
                move01,
                forward,
                right,
                up,
                aim,
                weight);

            PoseLeg(_rightUpperLeg, _rightLowerLeg, _rightFoot, strideSin, move01, run01, forward, up, weight);
            PoseLeg(_leftUpperLeg, _leftLowerLeg, _leftFoot, -strideSin, move01, run01, forward, up, weight);
        }

        private void PoseArm(
            Transform upper,
            Transform lower,
            Transform hand,
            float side,
            float ready,
            float attack,
            float super,
            float swing,
            float move01,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            if (upper == null || lower == null)
                return;

            Vector3 relaxedUpper =
                (-up * 0.78f + forward * 0.22f + right * side * 0.22f).normalized;
            Vector3 readyUpper =
                (aim * 0.72f + up * 0.06f + right * side * 0.32f).normalized;
            Vector3 desiredUpper = Vector3.Slerp(relaxedUpper, readyUpper, ready);
            desiredUpper = Vector3.Slerp(desiredUpper, aim, attack * 0.8f + super * 0.55f);
            desiredUpper = (desiredUpper + forward * swing * move01 * 0.10f).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight);

            if (hand == null)
                return;

            Vector3 relaxedLower =
                (-up * 0.48f + forward * 0.62f + right * side * 0.12f).normalized;
            Vector3 readyLower =
                (aim * 0.88f - up * 0.10f + right * side * 0.08f).normalized;
            Vector3 desiredLower = Vector3.Slerp(relaxedLower, readyLower, ready);
            desiredLower = Vector3.Slerp(desiredLower, aim, attack * 0.9f + super * 0.65f);
            RotateBoneToward(lower, hand, desiredLower, weight);

            AddLocal(
                hand,
                hand.localRotation,
                -attack * 6.0f - super * 10.0f,
                side * (8.0f + attack * 3.0f),
                side * -4.0f,
                weight * Mathf.Clamp01(ready + attack + super));
        }

        private void PoseLeg(
            Transform upper,
            Transform lower,
            Transform foot,
            float swing,
            float move01,
            float run01,
            Vector3 forward,
            Vector3 up,
            float weight)
        {
            if (upper == null || lower == null)
                return;

            float strideAmount = Mathf.Lerp(0.10f, 0.28f, run01) * move01;
            Vector3 desiredUpper =
                (-up * 0.96f + forward * swing * strideAmount).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight * Mathf.Clamp01(move01 * 1.2f));

            if (foot == null)
                return;

            Vector3 desiredLower =
                (-up * 0.98f - forward * swing * strideAmount * 0.45f).normalized;
            RotateBoneToward(lower, foot, desiredLower, weight * Mathf.Clamp01(move01 * 1.1f));
        }

        private Vector3 ResolveForward()
        {
            Vector3 forward = _animator != null
                ? _animator.transform.forward
                : transform.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 ResolveRight(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        private Vector3 ResolveAimDirection(Vector3 fallbackForward, float actionSignal)
        {
            if (_runtime == null || _owner == null || actionSignal < 0.05f)
                return fallbackForward;

            Vector3 aim = _runtime != null ? _runtime.AimDirection : fallbackForward;
            aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : fallbackForward;
        }

        private static void RotateBoneToward(
            Transform bone,
            Transform child,
            Vector3 desiredWorldDirection,
            float weight)
        {
            if (bone == null || child == null || weight <= 0f)
                return;

            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 0.000001f || desiredWorldDirection.sqrMagnitude < 0.000001f)
                return;

            Quaternion delta =
                Quaternion.FromToRotation(current.normalized, desiredWorldDirection.normalized);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
        }

        private static void AddLocal(
            Transform bone,
            Quaternion baseRotation,
            float pitch,
            float yaw,
            float roll,
            float weight)
        {
            if (bone == null || weight <= 0f)
                return;

            Quaternion target =
                baseRotation * Quaternion.Euler(pitch, yaw, roll);
            bone.localRotation =
                Quaternion.Slerp(baseRotation, target, Mathf.Clamp01(weight));
        }
    }
}
