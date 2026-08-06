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
            float move01 = _runtime != null ? _runtime.Move01 : 0f;
            float run01 = _runtime != null ? _runtime.Run01 : 0f;
            float action = _runtime != null ? _runtime.ActionWeight : 0f;
            float attack = _runtime != null ? _runtime.MainAttackWeight : 0f;
            float super = _runtime != null ? _runtime.SuperWeight : 0f;
            float hit = _runtime != null ? _runtime.HitReactWeight : 0f;
            float hyper = _runtime != null ? _runtime.HyperchargeWeight : 0f;
            float ready = Mathf.Clamp01(0.35f + action * 0.9f + move01 * 0.25f);
            BrawlerAttachmentGripPose gripPose = ResolveGripPose();
            if (gripPose == BrawlerAttachmentGripPose.None)
                ready = Mathf.Clamp01(action * 0.9f + move01 * 0.15f);

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
            PoseHandsForGrip(gripPose, ready, attack, super, weight);
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

        private void PoseHandsForGrip(
            BrawlerAttachmentGripPose gripPose,
            float ready,
            float attack,
            float super,
            float weight)
        {
            float poseWeight = weight * Mathf.Clamp01(_gripPoseWeight);
            if (poseWeight <= 0f)
                return;

            float action = Mathf.Clamp01(attack + super);
            float hold = Mathf.Clamp01(0.55f + ready * 0.35f + action * 0.25f);

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                    PoseGripHand(_rightHand, _rightFingerBones, _rightFingerBase, 0.95f, 0.65f, 1f, hold, poseWeight);
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    PoseGripHand(_rightHand, _rightFingerBones, _rightFingerBase, 0.95f, 0.65f, 1f, hold, poseWeight);
                    PoseGripHand(_leftHand, _leftFingerBones, _leftFingerBase, 0.95f, 0.65f, -1f, hold, poseWeight);
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                    PoseGripHand(_rightHand, _rightFingerBones, _rightFingerBase, 0.95f, 0.70f, 1f, hold, poseWeight);
                    PoseGripHand(_leftHand, _leftFingerBones, _leftFingerBase, 0.45f, 0.30f, -1f, hold * (0.4f + action * 0.6f), poseWeight);
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.Umbrella:
                    PoseGripHand(_rightHand, _rightFingerBones, _rightFingerBase, 0.9f, 0.55f, 1f, hold, poseWeight);
                    PoseGripHand(_leftHand, _leftFingerBones, _leftFingerBase, 0.22f, 0.15f, -1f, action, poseWeight);
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    PoseGripHand(_rightHand, _rightFingerBones, _rightFingerBase, 0.75f, 0.35f, 1f, hold, poseWeight);
                    PoseGripHand(_leftHand, _leftFingerBones, _leftFingerBase, 0.55f, 0.35f, -1f, Mathf.Clamp01(hold + action * 0.3f), poseWeight);
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    PoseGripHand(_rightHand, _rightFingerBones, _rightFingerBase, 0.55f, 0.85f, 1f, hold, poseWeight);
                    PoseGripHand(_leftHand, _leftFingerBones, _leftFingerBase, 0.28f, 0.35f, -1f, action, poseWeight);
                    break;
            }
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
