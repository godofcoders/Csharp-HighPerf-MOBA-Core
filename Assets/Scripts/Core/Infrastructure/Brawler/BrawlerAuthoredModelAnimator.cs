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
        private const float GripIdleHold = 0.88f;
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
            CacheBones();
            CaptureBasePose();
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
            CacheBones();
            CaptureBasePose();
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
            float ready = Mathf.Clamp01(action * 1.10f + move01 * 0.20f);
            if (gripPose != BrawlerAttachmentGripPose.None)
                ready = Mathf.Clamp01(ready + 0.08f);

            if (gripPose == BrawlerAttachmentGripPose.None)
                ready = Mathf.Clamp01(action * 0.9f + move01 * 0.15f);

            float strideSin = _runtime != null ? _runtime.StrideSin : Mathf.Sin(time * 2.4f);
            float strideCos = _runtime != null ? _runtime.StrideCos : Mathf.Cos(time * 2.4f);
            float idleBreath = Mathf.Sin(time * 1.7f) * (1f - move01);
            float idleLook = Mathf.Sin(time * 0.73f) * (1f - move01);
            Vector3 forward = ResolveForward();
            Vector3 right = ResolveRight(forward);
            Vector3 up = Vector3.up;
            Vector3 aim = ResolveAimDirection(forward, Mathf.Clamp01(action + attack + super));

            AddLocal(_hips, _hipsBase, idleBreath * 1.1f - move01 * 5.2f, idleLook * 0.7f, strideCos * move01 * 3.0f, weight);
            AddLocal(_spine, _spineBase, idleBreath * -1.0f + move01 * 4.4f - hit * 7.0f, idleLook * 0.8f, -strideCos * move01 * 2.6f, weight);
            AddLocal(_chest, _chestBase, idleBreath * -0.7f + super * -5.5f, idleLook * 1.1f, strideCos * move01 * 1.8f + hyper * 2.0f, weight);
            AddLocal(_head, _headBase, idleBreath * 0.7f - attack * 3.0f, idleLook * 2.2f, -strideCos * move01 * 0.8f, weight);

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
            PoseHandsForGrip(gripPose, attack, super, weight);
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

            float action = Mathf.Clamp01(attack + super);
            float runSwing = Mathf.Lerp(0.7f, 1.35f, Mathf.Clamp01(move01));
            Vector3 relaxedUpper =
                (-up * 0.92f + forward * 0.08f + right * side * 0.26f).normalized;
            Vector3 readyUpper =
                (aim * 0.78f + up * 0.04f + right * side * 0.30f).normalized;
            Vector3 desiredUpper = Vector3.Slerp(relaxedUpper, readyUpper, ready);
            desiredUpper = Vector3.Slerp(desiredUpper, aim, attack * 0.92f + super * 0.66f);
            desiredUpper = (desiredUpper + forward * swing * move01 * 0.13f * runSwing).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight);

            if (hand == null)
                return;

            Vector3 relaxedLower =
                (-up * 0.62f + forward * 0.46f + right * side * 0.12f).normalized;
            Vector3 readyLower =
                (aim * 0.92f - up * 0.06f + right * side * 0.08f).normalized;
            Vector3 desiredLower = Vector3.Slerp(relaxedLower, readyLower, ready);
            desiredLower = Vector3.Slerp(desiredLower, aim, attack * 0.96f + super * 0.72f);
            RotateBoneToward(lower, hand, desiredLower, weight);

            AddLocal(
                hand,
                hand.localRotation,
                -action * 10.0f - super * 6.0f,
                side * (5.0f + attack * 5.0f),
                side * (-5.0f - super * 3.0f),
                weight * Mathf.Clamp01(ready + action));
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
                        rightThumbCurl: 0.72f,
                        leftFingerCurl: 0f,
                        leftThumbCurl: 0f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    PoseGripHands(
                        rightFingerCurl: 1f,
                        rightThumbCurl: 0.72f,
                        leftFingerCurl: 1f,
                        leftThumbCurl: 0.72f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                    PoseGripHands(
                        rightFingerCurl: 1f,
                        rightThumbCurl: 0.76f,
                        leftFingerCurl: 0.62f,
                        leftThumbCurl: 0.48f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.Umbrella:
                    PoseGripHands(
                        rightFingerCurl: 0.96f,
                        rightThumbCurl: 0.68f,
                        leftFingerCurl: 0.12f,
                        leftThumbCurl: 0.08f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    PoseGripHands(
                        rightFingerCurl: 0.82f,
                        rightThumbCurl: 0.50f,
                        leftFingerCurl: 0.66f,
                        leftThumbCurl: 0.42f,
                        hold,
                        poseWeight);
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    PoseGripHands(
                        rightFingerCurl: 0.62f,
                        rightThumbCurl: 0.90f,
                        leftFingerCurl: 0.18f,
                        leftThumbCurl: 0.20f,
                        hold,
                        poseWeight);
                    break;
            }
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

            ApplyFingerCurl(fingerBones, fingerBase, 0, thumbCurl, side * -10f, weight);
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

            SetFingerRotation(bones[start], bases[start], 26f * curl, yaw, 0f, weight);
            SetFingerRotation(bones[start + 1], bases[start + 1], 34f * curl, 0f, 0f, weight);
            SetFingerRotation(bones[start + 2], bases[start + 2], 24f * curl, 0f, 0f, weight);
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

            float strideAmount = Mathf.Lerp(0.12f, 0.42f, run01) * move01;
            Vector3 desiredUpper =
                (-up * 0.96f + forward * swing * strideAmount).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight * Mathf.Clamp01(move01 * 1.2f));

            if (foot == null)
                return;

            Vector3 desiredLower =
                (-up * 0.98f - forward * swing * strideAmount * 0.62f).normalized;
            RotateBoneToward(lower, foot, desiredLower, weight * Mathf.Clamp01(move01 * 1.1f));
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
