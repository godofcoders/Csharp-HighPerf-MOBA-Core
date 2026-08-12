using MOBA.Core.Definitions;
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
        private const int FingerBoneCount = 15;
        private const float GripIdleHold = 0.96f;
        private const float GripActionBoost = 0.12f;
        private const float PoseRiseSpeed = 16f;
        private const float PoseFallSpeed = 9f;

        private static readonly HumanBodyBones[] LeftFingerBoneMap =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftIndexIntermediate,
            HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal,
            HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftRingIntermediate,
            HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.LeftLittleIntermediate,
            HumanBodyBones.LeftLittleDistal
        };

        private static readonly HumanBodyBones[] RightFingerBoneMap =
        {
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal,
            HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightMiddleIntermediate,
            HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal,
            HumanBodyBones.RightRingIntermediate,
            HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal,
            HumanBodyBones.RightLittleIntermediate,
            HumanBodyBones.RightLittleDistal
        };

        [SerializeField] private BrawlerController _owner;
        [SerializeField] private BrawlerDefinition _definition;
        [SerializeField] private Animator _animator;
        [SerializeField] private BrawlerAnimationRuntime _runtime;
        [SerializeField] private float _poseWeight = 1f;
        [SerializeField] private BrawlerAttachmentGripPose _gripPose =
            BrawlerAttachmentGripPose.Auto;
        [SerializeField, Range(0f, 1f)] private float _gripPoseWeight = 1f;
        [SerializeField] private bool _useShowcasePose;

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

        private readonly Transform[] _leftFingerBones = new Transform[FingerBoneCount];
        private readonly Transform[] _rightFingerBones = new Transform[FingerBoneCount];
        private readonly Quaternion[] _leftFingerBase = new Quaternion[FingerBoneCount];
        private readonly Quaternion[] _rightFingerBase = new Quaternion[FingerBoneCount];
        private BrawlerAttachmentFollower[] _attachmentFollowers =
            new BrawlerAttachmentFollower[0];
        private BrawlerRuntimeAttachmentGrip[] _runtimeAttachmentGrips =
            new BrawlerRuntimeAttachmentGrip[0];

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
        private float _smoothedMove01;
        private float _smoothedRun01;
        private float _smoothedAction;
        private float _smoothedAttack;
        private float _smoothedSuper;
        private float _smoothedHit;
        private float _smoothedHyper;
        private float _gaitPhaseOffset;
        private float _gaitTempoScale = 1f;
        private float _gaitAmplitudeScale = 1f;
        private float _strideReachScale = 1f;
        private float _footLiftScale = 1f;
        private float _armSwingScale = 1f;

        public static BrawlerAuthoredModelAnimator Ensure(
            GameObject root,
            BrawlerController owner,
            BrawlerDefinition definition = null)
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

            pose.Bind(owner, animator, definition);
            return pose;
        }

        public void Bind(
            BrawlerController owner,
            Animator animator,
            BrawlerDefinition definition = null)
        {
            _owner = owner != null ? owner : GetComponentInParent<BrawlerController>();
            _definition =
                definition != null
                    ? definition
                    : (_owner != null ? _owner.Definition : _definition);
            _animator = animator != null ? animator : GetComponentInChildren<Animator>(true);
            _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);
            RefreshGripProfile();
            ConfigureGaitProfile();
            CacheBones();
            CaptureBasePose();
        }

        public void SetShowcasePose(bool useShowcasePose)
        {
            _useShowcasePose = useShowcasePose;
        }

        public void RefreshRuntimeGripTargets()
        {
            _attachmentFollowers =
                GetComponentsInChildren<BrawlerAttachmentFollower>(true);
            _runtimeAttachmentGrips =
                GetComponentsInChildren<BrawlerRuntimeAttachmentGrip>(true);
        }

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);

            if (_owner == null)
                _owner = GetComponentInParent<BrawlerController>();

            if (_definition == null && _owner != null)
                _definition = _owner.Definition;

            if (_runtime == null)
                _runtime = BrawlerAnimationRuntime.Ensure(gameObject, _owner);

            RefreshGripProfile();
            ConfigureGaitProfile();
            CacheBones();
            CaptureBasePose();
            RefreshRuntimeGripTargets();
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || !_hasBasePose)
                return;

            SmoothPoseSignals(_owner == null ? Time.unscaledDeltaTime : Time.deltaTime);
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

            CacheFingerBones(_leftFingerBones, LeftFingerBoneMap);
            CacheFingerBones(_rightFingerBones, RightFingerBoneMap);
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
            CaptureFingerBasePose(_leftFingerBones, _leftFingerBase);
            CaptureFingerBasePose(_rightFingerBones, _rightFingerBase);
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
            ResetFingerPose(_leftFingerBones, _leftFingerBase);
            ResetFingerPose(_rightFingerBones, _rightFingerBase);
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
            float move01 = _smoothedMove01;
            float run01 = _smoothedRun01;
            float action = _smoothedAction;
            float attack = _smoothedAttack;
            float super = _smoothedSuper;
            float hit = _smoothedHit;
            float hyper = _smoothedHyper;
            BrawlerAttachmentGripPose gripPose = ResolveGripPose();
            float ready = Mathf.Clamp01(action * 1.24f + super * 0.18f + move01 * 0.05f);
            if (gripPose != BrawlerAttachmentGripPose.None)
                ready = Mathf.Clamp01(ready + 0.02f);

            if (gripPose == BrawlerAttachmentGripPose.None)
                ready = Mathf.Clamp01(action * 1.05f + move01 * 0.04f);

            float showcase = ResolveShowcaseReady(gripPose);
            if (showcase > 0f)
            {
                ready = Mathf.Max(ready, showcase);
                attack = Mathf.Max(attack, ResolveShowcaseAction(gripPose));
                action = Mathf.Max(action, attack);
            }

            float stridePhase =
                (_runtime != null ? _runtime.StridePhase : time * 4.2f) * _gaitTempoScale +
                _gaitPhaseOffset;
            float strideSin = Mathf.Sin(stridePhase);
            float strideCos = Mathf.Cos(stridePhase);
            float gaitMove01 = Mathf.Clamp01(move01 * _gaitAmplitudeScale);
            float gaitRun01 = Mathf.Clamp01(run01 * _gaitAmplitudeScale);
            float idleBreath = Mathf.Sin(time * 1.7f) * (1f - move01);
            float idleLook = Mathf.Sin(time * 0.73f) * (1f - move01);
            Vector3 forward = ResolveForward();
            Vector3 right = ResolveRight(forward);
            Vector3 up = Vector3.up;
            Vector3 aim = ResolveAimDirection(forward, Mathf.Clamp01(action + attack + super));

            AddLocal(_hips, _hipsBase, idleBreath * 1.1f - gaitMove01 * 6.0f, idleLook * 0.7f, strideCos * gaitMove01 * 3.8f, weight);
            AddLocal(_spine, _spineBase, idleBreath * -1.0f + gaitMove01 * 4.8f - hit * 7.0f, idleLook * 0.8f, -strideCos * gaitMove01 * 3.0f, weight);
            AddLocal(_chest, _chestBase, idleBreath * -0.7f + super * -5.5f, idleLook * 1.1f, strideCos * gaitMove01 * 2.2f + hyper * 2.0f, weight);
            AddLocal(_head, _headBase, idleBreath * 0.7f - attack * 3.0f, idleLook * 2.2f, -strideCos * gaitMove01 * 1.0f, weight);

            PoseArm(
                _rightUpperArm,
                _rightLowerArm,
                _rightHand,
                1f,
                ready,
                attack,
                super,
                strideSin,
                gaitMove01 * _armSwingScale,
                forward,
                right,
                up,
                aim,
                gripPose,
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
                gaitMove01 * _armSwingScale,
                forward,
                right,
                up,
                aim,
                gripPose,
                weight);

            PoseLeg(
                _rightUpperLeg,
                _rightLowerLeg,
                _rightFoot,
                strideSin,
                gaitMove01,
                gaitRun01,
                forward,
                up,
                weight);
            PoseLeg(
                _leftUpperLeg,
                _leftLowerLeg,
                _leftFoot,
                -strideSin,
                gaitMove01,
                gaitRun01,
                forward,
                up,
                weight);
            PoseHandsForGrip(gripPose, attack, super, weight);
            ApplyActionPoseAccent(
                gripPose,
                attack,
                super,
                hit,
                hyper,
                forward,
                right,
                up,
                aim,
                weight);
            ApplyAttachmentFollowersNow();
            ApplyRuntimeGripTargets(weight);
        }

        private void ApplyAttachmentFollowersNow()
        {
            if (_attachmentFollowers == null || _attachmentFollowers.Length == 0)
                return;

            for (int i = 0; i < _attachmentFollowers.Length; i++)
            {
                BrawlerAttachmentFollower follower = _attachmentFollowers[i];
                if (follower != null)
                    follower.ApplyNow();
            }
        }

        private void ApplyRuntimeGripTargets(float weight)
        {
            if (_runtimeAttachmentGrips == null || _runtimeAttachmentGrips.Length == 0)
                return;

            for (int i = 0; i < _runtimeAttachmentGrips.Length; i++)
            {
                BrawlerRuntimeAttachmentGrip grip = _runtimeAttachmentGrips[i];
                if (grip == null ||
                    !grip.TryGetSecondaryGrip(
                        out BrawlerAttachmentSocket socket,
                        out Transform target,
                        out float gripWeight))
                {
                    continue;
                }

                if (!TryResolveArmForSocket(
                        socket,
                        out Transform upper,
                        out Transform lower,
                        out Transform hand))
                {
                    continue;
                }

                ApplyArmGripIk(
                    upper,
                    lower,
                    hand,
                    target,
                    Mathf.Clamp01(weight * gripWeight));
            }
        }

        private float ResolveShowcaseReady(BrawlerAttachmentGripPose gripPose)
        {
            if (!_useShowcasePose)
                return 0f;

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.DualSidearm:
                    return 0.58f;
                case BrawlerAttachmentGripPose.Sidearm:
                case BrawlerAttachmentGripPose.LongGun:
                    return 0.54f;
                case BrawlerAttachmentGripPose.Bow:
                    return 0.72f;
                case BrawlerAttachmentGripPose.Umbrella:
                    return 0.62f;
                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Bottle:
                    return 0.46f;
                case BrawlerAttachmentGripPose.ThrowingStars:
                    return 0.42f;
                default:
                    return 0f;
            }
        }

        private float ResolveShowcaseAction(BrawlerAttachmentGripPose gripPose)
        {
            if (!_useShowcasePose)
                return 0f;

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Bow:
                    return 0.18f;
                case BrawlerAttachmentGripPose.Umbrella:
                case BrawlerAttachmentGripPose.LongGun:
                    return 0.10f;
                case BrawlerAttachmentGripPose.Sidearm:
                case BrawlerAttachmentGripPose.DualSidearm:
                case BrawlerAttachmentGripPose.ThrowingStars:
                    return 0.08f;
                default:
                    return 0f;
            }
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
            BrawlerAttachmentGripPose gripPose,
            float weight)
        {
            if (upper == null || lower == null)
                return;

            float action = Mathf.Clamp01(attack + super);
            float runSwing = Mathf.Lerp(0.7f, 1.35f, Mathf.Clamp01(move01));
            Vector3 relaxedUpper =
                (-up * 0.92f + forward * 0.08f + right * side * 0.26f).normalized;
            Vector3 readyUpper =
                (aim * 0.78f + up * 0.04f + right * side * 0.30f).normalized;
            ApplyGripUpperBias(gripPose, side, aim, right, up, ref readyUpper);
            Vector3 desiredUpper = Vector3.Slerp(relaxedUpper, readyUpper, ready);
            desiredUpper = Vector3.Slerp(desiredUpper, aim, attack * 0.92f + super * 0.66f);
            ApplyGripActionBias(gripPose, side, action, aim, right, up, ref desiredUpper);
            desiredUpper = (desiredUpper + forward * swing * move01 * 0.13f * runSwing).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight);

            if (hand == null)
                return;

            Vector3 relaxedLower =
                (-up * 0.62f + forward * 0.46f + right * side * 0.12f).normalized;
            Vector3 readyLower =
                (aim * 0.92f - up * 0.06f + right * side * 0.08f).normalized;
            ApplyGripLowerBias(gripPose, side, aim, right, up, ref readyLower);
            Vector3 desiredLower = Vector3.Slerp(relaxedLower, readyLower, ready);
            desiredLower = Vector3.Slerp(desiredLower, aim, attack * 0.96f + super * 0.72f);
            ApplyGripActionBias(gripPose, side, action, aim, right, up, ref desiredLower);
            RotateBoneToward(lower, hand, desiredLower, weight);

            AddLocal(
                hand,
                hand.localRotation,
                -action * 10.0f - super * 6.0f,
                side * (5.0f + attack * 5.0f),
                side * (-5.0f - super * 3.0f),
                weight * Mathf.Clamp01(ready + action));
        }

        private static void ApplyGripUpperBias(
            BrawlerAttachmentGripPose gripPose,
            float side,
            Vector3 aim,
            Vector3 right,
            Vector3 up,
            ref Vector3 readyUpper)
        {
            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                    if (side > 0f)
                        readyUpper = (aim * 0.92f + right * 0.24f - up * 0.02f).normalized;
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    readyUpper = (aim * 0.90f + right * side * 0.28f - up * 0.02f).normalized;
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                    readyUpper = side > 0f
                        ? (aim * 0.72f + right * 0.22f - up * 0.08f).normalized
                        : (aim * 0.84f - right * 0.24f + up * 0.06f).normalized;
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    readyUpper = side > 0f
                        ? (aim * 0.66f + right * 0.18f - up * 0.08f).normalized
                        : (aim * 0.48f - right * 0.22f - up * 0.10f).normalized;
                    break;

                case BrawlerAttachmentGripPose.Bottle:
                    readyUpper = side > 0f
                        ? (aim * 0.52f + right * 0.20f + up * 0.42f).normalized
                        : (-up * 0.75f - right * 0.18f + aim * 0.18f).normalized;
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    readyUpper = side > 0f
                        ? (aim * 0.34f + right * 0.14f - up * 0.18f).normalized
                        : (aim * 0.94f - right * 0.26f + up * 0.05f).normalized;
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    readyUpper = side > 0f
                        ? (aim * 0.72f + right * 0.30f + up * 0.10f).normalized
                        : (-up * 0.82f - right * 0.18f + aim * 0.12f).normalized;
                    break;
            }
        }

        private static void ApplyGripLowerBias(
            BrawlerAttachmentGripPose gripPose,
            float side,
            Vector3 aim,
            Vector3 right,
            Vector3 up,
            ref Vector3 readyLower)
        {
            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                    if (side > 0f)
                        readyLower = (aim * 0.98f + right * 0.08f - up * 0.04f).normalized;
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    readyLower = (aim * 0.98f + right * side * 0.10f - up * 0.04f).normalized;
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    readyLower = side > 0f
                        ? (aim * 0.86f + right * 0.08f - up * 0.10f).normalized
                        : (aim * 0.92f - right * 0.10f - up * 0.02f).normalized;
                    break;

                case BrawlerAttachmentGripPose.Bottle:
                    readyLower = side > 0f
                        ? (aim * 0.56f + right * 0.10f + up * 0.58f).normalized
                        : (-up * 0.86f - right * 0.08f).normalized;
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    readyLower = side > 0f
                        ? (-right * 0.42f + aim * 0.22f - up * 0.06f).normalized
                        : (aim * 0.98f - right * 0.06f).normalized;
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    readyLower = side > 0f
                        ? (aim * 0.78f + right * 0.18f + up * 0.22f).normalized
                        : (-up * 0.86f - right * 0.04f).normalized;
                    break;
            }
        }

        private static void ApplyGripActionBias(
            BrawlerAttachmentGripPose gripPose,
            float side,
            float action,
            Vector3 aim,
            Vector3 right,
            Vector3 up,
            ref Vector3 desired)
        {
            if (action <= 0f)
                return;

            Vector3 actionPose = desired;
            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Bottle:
                    if (side > 0f)
                        actionPose = (aim * 0.68f + up * 0.46f + right * 0.12f).normalized;
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    actionPose = side > 0f
                        ? (-right * 0.55f + aim * 0.22f - up * 0.02f).normalized
                        : (aim * 0.98f - right * 0.08f + up * 0.05f).normalized;
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    if (side > 0f)
                        actionPose = (aim * 0.90f + right * 0.12f + up * 0.18f).normalized;
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    actionPose = (aim * 0.82f + right * side * 0.10f - up * 0.04f).normalized;
                    break;
            }

            desired = Vector3.Slerp(desired, actionPose, Mathf.Clamp01(action));
        }

        private void PoseHandsForGrip(
            BrawlerAttachmentGripPose gripPose,
            float attack,
            float super,
            float weight)
        {
            float poseWeight = weight * Mathf.Clamp01(_gripPoseWeight);

            if (poseWeight <= 0f)
                return;

            float action = Mathf.Clamp01(attack + super);
            float hold = Mathf.Clamp01(GripIdleHold + action * GripActionBoost);

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                    PoseGripHands(
                        rightFingerCurl: 1f,
                        rightThumbCurl: 0.92f,
                        leftFingerCurl: 0f,
                        leftThumbCurl: 0f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    PoseGripHands(
                        rightFingerCurl: 1f,
                        rightThumbCurl: 0.92f,
                        leftFingerCurl: 1f,
                        leftThumbCurl: 0.92f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                    PoseGripHands(
                        rightFingerCurl: 1f,
                        rightThumbCurl: 0.94f,
                        leftFingerCurl: 0.78f,
                        leftThumbCurl: 0.68f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.Umbrella:
                    PoseGripHands(
                        rightFingerCurl: 1f,
                        rightThumbCurl: 0.90f,
                        leftFingerCurl: 0.18f,
                        leftThumbCurl: 0.14f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    PoseGripHands(
                        rightFingerCurl: 0.94f,
                        rightThumbCurl: 0.74f,
                        leftFingerCurl: 0.82f,
                        leftThumbCurl: 0.64f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    PoseGripHands(
                        rightFingerCurl: 0.88f,
                        rightThumbCurl: 1f,
                        leftFingerCurl: 0.18f,
                        leftThumbCurl: 0.20f,
                        hold,
                        poseWeight);
                    break;
            }
        }

        private void ApplyActionPoseAccent(
            BrawlerAttachmentGripPose gripPose,
            float attack,
            float super,
            float hit,
            float hyper,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            float shot = Mathf.Clamp01(attack);
            float ability = Mathf.Clamp01(super);
            float impact = Mathf.Clamp01(hit);
            float action = Mathf.Clamp01(shot + ability);
            float accent = Mathf.Clamp01(action + impact + hyper * 0.35f);
            if (accent <= 0.001f || weight <= 0f)
                return;

            float yawToAim = SignedPlanarAngle(forward, aim, up);
            float torsoAim = Mathf.Clamp(yawToAim, -36f, 36f) * action;
            float recoil = ResolveRecoilAmount(gripPose, shot, ability);

            AddLocal(
                _spine,
                _spine != null ? _spine.localRotation : Quaternion.identity,
                impact * 5.5f - recoil * 1.2f,
                torsoAim * 0.10f,
                -torsoAim * 0.04f + hyper * 1.6f,
                weight * accent);
            AddLocal(
                _chest,
                _chest != null ? _chest.localRotation : Quaternion.identity,
                impact * 4.0f - recoil * 1.8f - ability * 3.0f,
                torsoAim * 0.16f,
                -torsoAim * 0.07f + hyper * 2.0f,
                weight * accent);
            AddLocal(
                _head,
                _head != null ? _head.localRotation : Quaternion.identity,
                -recoil * 0.55f + impact * 2.0f,
                torsoAim * 0.08f,
                -torsoAim * 0.025f,
                weight * accent);

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                    ApplySidearmActionAccent(_rightUpperArm, _rightLowerArm, _rightHand, 1f, shot, ability, recoil, weight);
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    ApplySidearmActionAccent(_rightUpperArm, _rightLowerArm, _rightHand, 1f, shot, ability, recoil, weight);
                    ApplySidearmActionAccent(_leftUpperArm, _leftLowerArm, _leftHand, -1f, shot * 0.92f, ability, recoil, weight);
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                    ApplyLongWeaponActionAccent(shot, ability, recoil, weight, primarySide: 1f);
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    ApplyLongToolActionAccent(shot, ability, recoil, weight);
                    break;

                case BrawlerAttachmentGripPose.Bottle:
                    ApplyBottleActionAccent(shot, ability, weight);
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    ApplyBowActionAccent(shot, ability, weight);
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    ApplyThrowingStarActionAccent(shot, ability, weight);
                    break;
            }
        }

        private static float ResolveRecoilAmount(
            BrawlerAttachmentGripPose gripPose,
            float shot,
            float ability)
        {
            float baseRecoil;
            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.Bow:
                case BrawlerAttachmentGripPose.ThrowingStars:
                    baseRecoil = 4.5f;
                    break;
                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    baseRecoil = 3.2f;
                    break;
                case BrawlerAttachmentGripPose.LongGun:
                    baseRecoil = 5.0f;
                    break;
                default:
                    baseRecoil = 6.0f;
                    break;
            }

            return baseRecoil * Mathf.Clamp01(shot + ability * 0.85f);
        }

        private void ApplySidearmActionAccent(
            Transform upper,
            Transform lower,
            Transform hand,
            float side,
            float shot,
            float ability,
            float recoil,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            AddLocal(
                upper,
                upper != null ? upper.localRotation : Quaternion.identity,
                -recoil * 0.55f,
                side * 2.5f * action,
                side * -2.0f * action,
                weight * action);
            AddLocal(
                lower,
                lower != null ? lower.localRotation : Quaternion.identity,
                -recoil * 0.75f,
                side * 2.0f * action,
                side * -2.5f * action,
                weight * action);
            AddLocal(
                hand,
                hand != null ? hand.localRotation : Quaternion.identity,
                -recoil * 1.35f,
                side * 3.5f * action,
                side * -4.0f * action,
                weight * action);
        }

        private void ApplyLongWeaponActionAccent(
            float shot,
            float ability,
            float recoil,
            float weight,
            float primarySide)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            ApplySidearmActionAccent(
                _rightUpperArm,
                _rightLowerArm,
                _rightHand,
                primarySide,
                shot * 0.75f,
                ability,
                recoil * 0.70f,
                weight);
            AddLocal(
                _leftLowerArm,
                _leftLowerArm != null ? _leftLowerArm.localRotation : Quaternion.identity,
                -recoil * 0.24f,
                -primarySide * 2.5f * action,
                primarySide * 2.0f * action,
                weight * action);
        }

        private void ApplyLongToolActionAccent(
            float shot,
            float ability,
            float recoil,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                -recoil * 0.35f - ability * 2.0f,
                1.5f * action,
                -2.2f * action,
                weight * action);
            AddLocal(
                _rightHand,
                _rightHand != null ? _rightHand.localRotation : Quaternion.identity,
                -recoil * 0.9f,
                2.0f * action,
                -3.0f * action,
                weight * action);
        }

        private void ApplyBottleActionAccent(
            float shot,
            float ability,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                -10.5f * action,
                5.0f * action,
                -8.0f * action,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                -13.0f * action,
                3.0f * action,
                -6.0f * action,
                weight * action);
            AddLocal(
                _rightHand,
                _rightHand != null ? _rightHand.localRotation : Quaternion.identity,
                -18.0f * action,
                4.0f * action,
                -9.0f * action,
                weight * action);
        }

        private void ApplyBowActionAccent(
            float shot,
            float ability,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                5.0f * action,
                6.0f * action,
                -5.5f * action,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                8.0f * action,
                5.0f * action,
                -7.0f * action,
                weight * action);
            AddLocal(
                _leftLowerArm,
                _leftLowerArm != null ? _leftLowerArm.localRotation : Quaternion.identity,
                -3.0f * action,
                -2.0f * action,
                3.0f * action,
                weight * action);
        }

        private void ApplyThrowingStarActionAccent(
            float shot,
            float ability,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                -8.0f * action,
                8.0f * action,
                -10.0f * action,
                weight * action);
            AddLocal(
                _rightHand,
                _rightHand != null ? _rightHand.localRotation : Quaternion.identity,
                -14.0f * action,
                12.0f * action,
                -18.0f * action,
                weight * action);
        }

        private void PoseGripHands(
            float rightFingerCurl,
            float rightThumbCurl,
            float leftFingerCurl,
            float leftThumbCurl,
            float hold,
            float poseWeight)
        {
            PoseGripHand(
                _rightHand,
                _rightFingerBones,
                _rightFingerBase,
                rightFingerCurl,
                rightThumbCurl,
                1f,
                hold,
                poseWeight);
            PoseGripHand(
                _leftHand,
                _leftFingerBones,
                _leftFingerBase,
                leftFingerCurl,
                leftThumbCurl,
                -1f,
                hold,
                poseWeight);
        }

        private static void PoseGripHand(
            Transform hand,
            Transform[] fingerBones,
            Quaternion[] fingerBase,
            float fingerCurl,
            float thumbCurl,
            float side,
            float hold,
            float poseWeight)
        {
            float weight = Mathf.Clamp01(hold * poseWeight);
            if (weight <= 0f)
                return;

            AddLocal(
                hand,
                hand != null ? hand.localRotation : Quaternion.identity,
                -7f * fingerCurl,
                side * 2f,
                side * -5f,
                weight);

            ApplyFingerCurl(fingerBones, fingerBase, 0, thumbCurl, side * -14f, weight);
            ApplyFingerCurl(fingerBones, fingerBase, 3, fingerCurl, side * 2f, weight);
            ApplyFingerCurl(fingerBones, fingerBase, 6, fingerCurl, 0f, weight);
            ApplyFingerCurl(fingerBones, fingerBase, 9, fingerCurl * 0.92f, side * -2f, weight);
            ApplyFingerCurl(fingerBones, fingerBase, 12, fingerCurl * 0.84f, side * -4f, weight);
        }

        private static void ApplyFingerCurl(
            Transform[] bones,
            Quaternion[] bases,
            int start,
            float curl,
            float yaw,
            float weight)
        {
            if (bones == null || bases == null || start < 0 || start + 2 >= bones.Length)
                return;

            SetFingerRotation(bones[start], bases[start], 40f * curl, yaw, 0f, weight);
            SetFingerRotation(bones[start + 1], bases[start + 1], 52f * curl, 0f, 0f, weight);
            SetFingerRotation(bones[start + 2], bases[start + 2], 36f * curl, 0f, 0f, weight);
        }

        private static void SetFingerRotation(
            Transform bone,
            Quaternion baseRotation,
            float pitch,
            float yaw,
            float roll,
            float weight)
        {
            if (bone == null || weight <= 0f)
                return;

            Quaternion target = baseRotation * Quaternion.Euler(pitch, yaw, roll);
            bone.localRotation = Quaternion.Slerp(baseRotation, target, Mathf.Clamp01(weight));
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

            float strideAmount =
                Mathf.Lerp(0.16f, 0.56f, run01) *
                move01 *
                _strideReachScale;
            float stepLift =
                Mathf.Pow(Mathf.Max(0f, swing), 0.72f) *
                Mathf.Lerp(0.018f, 0.115f, run01) *
                move01 *
                _footLiftScale;
            Vector3 desiredUpper =
                (-up * 0.94f + forward * swing * strideAmount + up * stepLift * 0.22f).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight * Mathf.Clamp01(move01 * 1.2f));

            if (foot == null)
                return;

            Vector3 desiredLower =
                (-up * 0.96f - forward * swing * strideAmount * 0.68f + up * stepLift * 0.18f).normalized;
            RotateBoneToward(lower, foot, desiredLower, weight * Mathf.Clamp01(move01 * 1.1f));

            AddLocal(
                foot,
                foot.localRotation,
                -swing * Mathf.Lerp(5f, 16f, run01) * move01,
                0f,
                swing * Mathf.Lerp(1.2f, 3.8f, run01) * move01,
                weight * Mathf.Clamp01(move01 * 1.2f));
        }

        private void SmoothPoseSignals(float deltaTime)
        {
            if (_runtime == null)
            {
                _smoothedMove01 = Approach(_smoothedMove01, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                _smoothedRun01 = Approach(_smoothedRun01, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                _smoothedAction = Approach(_smoothedAction, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                _smoothedAttack = Approach(_smoothedAttack, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                _smoothedSuper = Approach(_smoothedSuper, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                _smoothedHit = Approach(_smoothedHit, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                _smoothedHyper = Approach(_smoothedHyper, 0f, deltaTime, PoseRiseSpeed, PoseFallSpeed);
                return;
            }

            _smoothedMove01 = Approach(_smoothedMove01, _runtime.Move01, deltaTime, 9f, 7f);
            _smoothedRun01 = Approach(_smoothedRun01, _runtime.Run01, deltaTime, 10f, 8f);
            _smoothedAction = Approach(_smoothedAction, _runtime.ActionWeight, deltaTime, PoseRiseSpeed, PoseFallSpeed);
            _smoothedAttack = Approach(_smoothedAttack, _runtime.MainAttackWeight, deltaTime, 18f, 10f);
            _smoothedSuper = Approach(_smoothedSuper, _runtime.SuperWeight, deltaTime, 14f, 8f);
            _smoothedHit = Approach(_smoothedHit, _runtime.HitReactWeight, deltaTime, 18f, 12f);
            _smoothedHyper = Approach(_smoothedHyper, _runtime.HyperchargeWeight, deltaTime, 8f, 5f);
        }

        private static float Approach(
            float current,
            float target,
            float deltaTime,
            float riseSpeed,
            float fallSpeed)
        {
            float speed = target > current ? riseSpeed : fallSpeed;
            return Mathf.MoveTowards(
                current,
                Mathf.Clamp01(target),
                Mathf.Max(0f, deltaTime) * Mathf.Max(0f, speed));
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

        private void RefreshGripProfile()
        {
            BrawlerAttachmentProfile profile = ResolveAttachmentProfile();
            if (profile == null)
            {
                _gripPose = BrawlerAttachmentGripPose.None;
                _gripPoseWeight = 0f;
                return;
            }

            _gripPose = profile.GripPose;
            _gripPoseWeight = Mathf.Clamp01(profile.GripPoseWeight);
        }

        private void ConfigureGaitProfile()
        {
            string seed = ResolveBrawlerName();
            _gaitPhaseOffset = Stable01(seed + "_phase") * Mathf.PI * 2f;
            _gaitTempoScale = Mathf.Lerp(0.94f, 1.10f, Stable01(seed + "_tempo"));
            _gaitAmplitudeScale = Mathf.Lerp(0.92f, 1.08f, Stable01(seed + "_amplitude"));
            _strideReachScale = 1f;
            _footLiftScale = 1f;
            _armSwingScale = 1f;

            if (Contains(seed, "El Primo"))
            {
                _gaitTempoScale *= 0.90f;
                _gaitAmplitudeScale *= 1.10f;
                _strideReachScale = 1.10f;
                _footLiftScale = 0.92f;
                _armSwingScale = 0.86f;
            }
            else if (Contains(seed, "Barley"))
            {
                _gaitTempoScale *= 1.10f;
                _gaitAmplitudeScale *= 0.82f;
                _strideReachScale = 0.78f;
                _footLiftScale = 0.64f;
                _armSwingScale = 0.62f;
            }
            else if (Contains(seed, "Jessie") || Contains(seed, "Leon"))
            {
                _gaitTempoScale *= 1.12f;
                _gaitAmplitudeScale *= 0.98f;
                _strideReachScale = 0.94f;
                _footLiftScale = 1.12f;
                _armSwingScale = 1.05f;
            }
            else if (Contains(seed, "Byron") || Contains(seed, "Piper"))
            {
                _gaitTempoScale *= 0.96f;
                _gaitAmplitudeScale *= 0.88f;
                _strideReachScale = 0.90f;
                _footLiftScale = 0.86f;
                _armSwingScale = 0.76f;
            }
        }

        private string ResolveBrawlerName()
        {
            BrawlerDefinition definition = _definition != null
                ? _definition
                : (_owner != null ? _owner.Definition : null);

            if (definition == null)
                return gameObject.name;

            return !string.IsNullOrWhiteSpace(definition.BrawlerName)
                ? definition.BrawlerName
                : definition.name;
        }

        private static bool Contains(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(expected) &&
                   value.IndexOf(expected, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float Stable01(string seedText)
        {
            unchecked
            {
                string value = seedText ?? string.Empty;
                int hash = 23;

                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];

                return (hash & 0x7fffffff) / 2147483647f;
            }
        }

        private BrawlerAttachmentGripPose ResolveGripPose()
        {
            if (_gripPose != BrawlerAttachmentGripPose.Auto)
                return _gripPose;

            BrawlerAttachmentProfile profile = ResolveAttachmentProfile();
            if (profile == null || profile.Attachments == null)
                return BrawlerAttachmentGripPose.None;

            int pistolCount = 0;
            for (int i = 0; i < profile.Attachments.Length; i++)
            {
                BrawlerAttachmentBinding binding = profile.Attachments[i];
                if (binding == null)
                    continue;

                switch (binding.GeneratedAttachment)
                {
                    case BrawlerGeneratedAttachmentType.Pistol:
                        pistolCount++;
                        break;
                    case BrawlerGeneratedAttachmentType.Bottle:
                        return BrawlerAttachmentGripPose.Bottle;
                    case BrawlerGeneratedAttachmentType.Staff:
                        return BrawlerAttachmentGripPose.LongTool;
                    case BrawlerGeneratedAttachmentType.ShockGun:
                        return BrawlerAttachmentGripPose.LongGun;
                    case BrawlerGeneratedAttachmentType.Bow:
                        return BrawlerAttachmentGripPose.Bow;
                    case BrawlerGeneratedAttachmentType.NinjaStars:
                        return BrawlerAttachmentGripPose.ThrowingStars;
                    case BrawlerGeneratedAttachmentType.Umbrella:
                        return BrawlerAttachmentGripPose.Umbrella;
                }
            }

            if (pistolCount > 1)
                return BrawlerAttachmentGripPose.DualSidearm;

            return pistolCount == 1
                ? BrawlerAttachmentGripPose.Sidearm
                : BrawlerAttachmentGripPose.None;
        }

        private BrawlerAttachmentProfile ResolveAttachmentProfile()
        {
            BrawlerDefinition definition = _definition != null
                ? _definition
                : (_owner != null ? _owner.Definition : null);

            return definition != null ? definition.AttachmentProfile : null;
        }

        private void CacheFingerBones(
            Transform[] target,
            HumanBodyBones[] map)
        {
            if (target == null || map == null)
                return;

            int count = Mathf.Min(target.Length, map.Length);
            for (int i = 0; i < count; i++)
                target[i] = Bone(map[i]);
        }

        private static void CaptureFingerBasePose(
            Transform[] bones,
            Quaternion[] bases)
        {
            if (bones == null || bases == null)
                return;

            int count = Mathf.Min(bones.Length, bases.Length);
            for (int i = 0; i < count; i++)
                bases[i] = LocalRotation(bones[i]);
        }

        private static void ResetFingerPose(
            Transform[] bones,
            Quaternion[] bases)
        {
            if (bones == null || bases == null)
                return;

            int count = Mathf.Min(bones.Length, bases.Length);
            for (int i = 0; i < count; i++)
                SetLocalRotation(bones[i], bases[i]);
        }

        private static Vector3 ResolveRight(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        private static float SignedPlanarAngle(
            Vector3 from,
            Vector3 to,
            Vector3 axis)
        {
            from.y = 0f;
            to.y = 0f;

            if (from.sqrMagnitude <= 0.0001f || to.sqrMagnitude <= 0.0001f)
                return 0f;

            return Vector3.SignedAngle(from.normalized, to.normalized, axis);
        }

        private Vector3 ResolveAimDirection(Vector3 fallbackForward, float actionSignal)
        {
            if (_runtime == null || _owner == null || actionSignal < 0.05f)
                return fallbackForward;

            Vector3 aim = _runtime != null ? _runtime.AimDirection : fallbackForward;
            aim.y = 0f;
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : fallbackForward;
        }

        private bool TryResolveArmForSocket(
            BrawlerAttachmentSocket socket,
            out Transform upper,
            out Transform lower,
            out Transform hand)
        {
            switch (socket)
            {
                case BrawlerAttachmentSocket.LeftHand:
                case BrawlerAttachmentSocket.SecondaryWeapon:
                    upper = _leftUpperArm;
                    lower = _leftLowerArm;
                    hand = _leftHand;
                    return upper != null && lower != null && hand != null;

                case BrawlerAttachmentSocket.RightHand:
                case BrawlerAttachmentSocket.PrimaryWeapon:
                    upper = _rightUpperArm;
                    lower = _rightLowerArm;
                    hand = _rightHand;
                    return upper != null && lower != null && hand != null;

                default:
                    upper = null;
                    lower = null;
                    hand = null;
                    return false;
            }
        }

        private static void ApplyArmGripIk(
            Transform upper,
            Transform lower,
            Transform hand,
            Transform target,
            float weight)
        {
            if (upper == null ||
                lower == null ||
                hand == null ||
                target == null ||
                weight <= 0f)
            {
                return;
            }

            Vector3 targetPosition = target.position;
            for (int i = 0; i < 2; i++)
            {
                Vector3 upperDirection = targetPosition - upper.position;
                if (upperDirection.sqrMagnitude > 0.000001f)
                    RotateBoneToward(upper, lower, upperDirection.normalized, weight * 0.58f);

                Vector3 lowerDirection = targetPosition - lower.position;
                if (lowerDirection.sqrMagnitude > 0.000001f)
                    RotateBoneToward(lower, hand, lowerDirection.normalized, weight);
            }

            hand.rotation = Quaternion.Slerp(
                hand.rotation,
                target.rotation,
                Mathf.Clamp01(weight * 0.42f));
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
