using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using System.Collections.Generic;
using System.Text;
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
        private const float DefaultHandRotationStrength = 0.36f;
        private const float AuthoredHandRotationStrength = 0.72f;
        private const string RuntimePalmAnchorRootName = "RuntimePalmAnchors";
        private const string RightPalmSocketName = "RightPalmSocket";
        private const string LeftPalmSocketName = "LeftPalmSocket";
        private const string RightHandIkTargetName = "RightHand_IK_Target";
        private const string LeftHandIkTargetName = "LeftHand_IK_Target";
        private const string WeaponGripTargetName = "WeaponGrip_Target";
        private const string OffhandGripTargetName = "OffhandGrip_Target";
        private const string WeaponMainSocketName = "Weapon_Main";
        private const string WeaponOffhandSocketName = "Weapon_Offhand";
        private const float RuntimePalmForwardOffset = 0.07f;
        private const float RuntimePalmSideOffset = 0.02f;
        private const float RuntimePalmUpOffset = 0.005f;
        private const float SparsePalmForwardBias = 0.055f;
        private const float SparsePalmForwardExtentBias = 0.34f;
        private const float SparsePalmSideExtentBias = 1.08f;
        private const float SparsePalmDownExtentBias = 1.12f;
        private const float SparsePalmMinimumDrop = 0.14f;
        private const float SparsePalmMinimumSide = 0.065f;

        private enum AnimationPersona
        {
            Balanced,
            Gunslinger,
            Engineer,
            Thrower,
            Heavyweight,
            Archer,
            Support,
            Sniper,
            Assassin,
            Robot
        }

        private static readonly string[] HipBoneNames =
            { "mixamorig:Hips", "Hips", "hips", "root" };
        private static readonly string[] SpineBoneNames =
            { "mixamorig:Spine", "mixamorig:Spine1", "Spine", "spine", "torso" };
        private static readonly string[] ChestBoneNames =
            { "mixamorig:Spine2", "mixamorig:Chest", "Chest", "UpperChest", "chest" };
        private static readonly string[] HeadBoneNames =
            { "mixamorig:Head", "Head", "head" };
        private static readonly string[] LeftUpperArmBoneNames =
            { "mixamorig:LeftArm", "mixamorig:LeftShoulder", "LeftUpperArm", "LeftArm", "LeftShoulder", "Left_Arm", "Arm_L", "arm-left", "l-arm" };
        private static readonly string[] LeftLowerArmBoneNames =
            { "mixamorig:LeftForeArm", "LeftLowerArm", "LeftForeArm", "Left_ForeArm", "Left_LowerArm", "ForeArm_L", "LowerArm_L", "forearm-left", "lower-arm-left" };
        private static readonly string[] LeftHandBoneNames =
            { "mixamorig:LeftHand", "LeftHand", "Left_Hand", "Hand_L", "Palm_L", "hand-left", "palm-left", "l-hand" };
        private static readonly string[] RightUpperArmBoneNames =
            { "mixamorig:RightArm", "mixamorig:RightShoulder", "RightUpperArm", "RightArm", "RightShoulder", "Right_Arm", "Arm_R", "arm-right", "r-arm" };
        private static readonly string[] RightLowerArmBoneNames =
            { "mixamorig:RightForeArm", "RightLowerArm", "RightForeArm", "Right_ForeArm", "Right_LowerArm", "ForeArm_R", "LowerArm_R", "forearm-right", "lower-arm-right" };
        private static readonly string[] RightHandBoneNames =
            { "mixamorig:RightHand", "RightHand", "Right_Hand", "Hand_R", "Palm_R", "hand-right", "palm-right", "r-hand" };
        private static readonly string[] LeftUpperLegBoneNames =
            { "mixamorig:LeftUpLeg", "LeftUpperLeg", "LeftUpLeg", "leg-left" };
        private static readonly string[] LeftLowerLegBoneNames =
            { "mixamorig:LeftLeg", "LeftLowerLeg", "LeftLeg", "lower-leg-left" };
        private static readonly string[] LeftFootBoneNames =
            { "mixamorig:LeftFoot", "LeftFoot", "foot-left" };
        private static readonly string[] RightUpperLegBoneNames =
            { "mixamorig:RightUpLeg", "RightUpperLeg", "RightUpLeg", "leg-right" };
        private static readonly string[] RightLowerLegBoneNames =
            { "mixamorig:RightLeg", "RightLowerLeg", "RightLeg", "lower-leg-right" };
        private static readonly string[] RightFootBoneNames =
            { "mixamorig:RightFoot", "RightFoot", "foot-right" };

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
        [SerializeField] private AnimationPersona _animationPersona =
            AnimationPersona.Balanced;

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
        private Transform _runtimePalmAnchorRoot;
        private Transform _rightPalmSocket;
        private Transform _leftPalmSocket;
        private Transform _rightPalmParent;
        private Transform _leftPalmParent;
        private Transform _rightPalmSource;
        private Transform _leftPalmSource;
        private Transform _rightPalmAuthoringTarget;
        private Transform _leftPalmAuthoringTarget;
        private Renderer[] _rightPalmSourceRenderers = new Renderer[0];
        private Renderer[] _leftPalmSourceRenderers = new Renderer[0];
        private bool _rightPalmUsesSparseSource;
        private bool _leftPalmUsesSparseSource;
        private bool _rightHandIsVisualFallback;
        private bool _leftHandIsVisualFallback;
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
        private BrawlerHandPoseTargets[] _handPoseTargets =
            new BrawlerHandPoseTargets[0];

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
        private float _locomotionBounceScale = 1f;
        private float _locomotionLeanScale = 1f;
        private float _locomotionSwaggerScale = 1f;
        private float _actionSnapScale = 1f;
        private float _superSnapScale = 1f;
        private float _showcaseInspectScale = 1f;
        private float _showcaseTempoScale = 1f;

        public static BrawlerAuthoredModelAnimator Ensure(
            GameObject root,
            BrawlerController owner,
            BrawlerDefinition definition = null)
        {
            if (root == null)
                return null;

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null && !LooksLikeSparseRig(root.transform))
                return null;

            BrawlerAuthoredModelAnimator pose =
                root.GetComponent<BrawlerAuthoredModelAnimator>();
            if (pose == null)
                pose = root.AddComponent<BrawlerAuthoredModelAnimator>();

            pose.Bind(owner, animator, definition);
            return pose;
        }

        public Transform RightHandTransform => _rightHand;

        public Transform LeftHandTransform => _leftHand;

        public Transform RightPalmSocketTransform =>
            _rightPalmSocket != null
                ? _rightPalmSocket
                : (_rightHand != null ? _rightHand : (_rightLowerArm != null ? _rightLowerArm : _rightUpperArm));

        public Transform LeftPalmSocketTransform =>
            _leftPalmSocket != null
                ? _leftPalmSocket
                : (_leftHand != null ? _leftHand : (_leftLowerArm != null ? _leftLowerArm : _leftUpperArm));

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
            ConfigureAnimationStyle();
            _hasBasePose = false;
            CacheBones();
            CaptureBasePose();
            EnsureRuntimePalmAnchors();
            RefreshPalmAnchorsNow();
            RefreshRuntimeGripTargets();
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
            _handPoseTargets =
                GetComponentsInChildren<BrawlerHandPoseTargets>(true);
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
            ConfigureAnimationStyle();
            _hasBasePose = false;
            CacheBones();
            CaptureBasePose();
            EnsureRuntimePalmAnchors();
            RefreshPalmAnchorsNow();
            RefreshRuntimeGripTargets();
        }

        private void LateUpdate()
        {
            if (!_hasBasePose)
                return;

            SmoothPoseSignals(_owner == null ? Time.unscaledDeltaTime : Time.deltaTime);
            ResetToBasePose();
            ApplyPresentationPose();
        }

        private void CacheBones()
        {
            Transform searchRoot = _animator != null ? _animator.transform : transform;

            _rightHandIsVisualFallback = false;
            _leftHandIsVisualFallback = false;

            _hips = Bone(HumanBodyBones.Hips, HipBoneNames);
            _spine = Bone(HumanBodyBones.Spine, SpineBoneNames);
            _chest =
                Bone(HumanBodyBones.Chest, ChestBoneNames) ??
                Bone(HumanBodyBones.UpperChest, ChestBoneNames);
            _head = Bone(HumanBodyBones.Head, HeadBoneNames);
            _leftUpperArm =
                Bone(HumanBodyBones.LeftUpperArm, LeftUpperArmBoneNames) ??
                Bone(HumanBodyBones.LeftShoulder, LeftUpperArmBoneNames) ??
                FindBone(searchRoot, LeftUpperArmBoneNames);
            _leftLowerArm =
                Bone(HumanBodyBones.LeftLowerArm, LeftLowerArmBoneNames) ??
                FindBone(searchRoot, LeftLowerArmBoneNames);
            _leftHand =
                Bone(HumanBodyBones.LeftHand, LeftHandBoneNames) ??
                FindBone(searchRoot, LeftHandBoneNames);
            _rightUpperArm =
                Bone(HumanBodyBones.RightUpperArm, RightUpperArmBoneNames) ??
                Bone(HumanBodyBones.RightShoulder, RightUpperArmBoneNames) ??
                FindBone(searchRoot, RightUpperArmBoneNames);
            _rightLowerArm =
                Bone(HumanBodyBones.RightLowerArm, RightLowerArmBoneNames) ??
                FindBone(searchRoot, RightLowerArmBoneNames);
            _rightHand =
                Bone(HumanBodyBones.RightHand, RightHandBoneNames) ??
                FindBone(searchRoot, RightHandBoneNames);
            _leftUpperLeg =
                Bone(HumanBodyBones.LeftUpperLeg, LeftUpperLegBoneNames) ??
                FindBone(searchRoot, LeftUpperLegBoneNames);
            _leftLowerLeg =
                Bone(HumanBodyBones.LeftLowerLeg, LeftLowerLegBoneNames) ??
                FindBone(searchRoot, LeftLowerLegBoneNames);
            _leftFoot =
                Bone(HumanBodyBones.LeftFoot, LeftFootBoneNames) ??
                FindBone(searchRoot, LeftFootBoneNames);
            _rightUpperLeg =
                Bone(HumanBodyBones.RightUpperLeg, RightUpperLegBoneNames) ??
                FindBone(searchRoot, RightUpperLegBoneNames);
            _rightLowerLeg =
                Bone(HumanBodyBones.RightLowerLeg, RightLowerLegBoneNames) ??
                FindBone(searchRoot, RightLowerLegBoneNames);
            _rightFoot =
                Bone(HumanBodyBones.RightFoot, RightFootBoneNames) ??
                FindBone(searchRoot, RightFootBoneNames);

            if (_rightHand == null)
            {
                _rightHand = FindVisualPart(searchRoot, side: 1f, preferHand: true);
                _rightHandIsVisualFallback = _rightHand != null;
            }

            if (_leftHand == null)
            {
                _leftHand = FindVisualPart(searchRoot, side: -1f, preferHand: true);
                _leftHandIsVisualFallback = _leftHand != null;
            }

            if (_rightLowerArm == null)
                _rightLowerArm = FindVisualPart(searchRoot, side: 1f, preferHand: false);
            if (_leftLowerArm == null)
                _leftLowerArm = FindVisualPart(searchRoot, side: -1f, preferHand: false);
            if (_rightUpperArm == null)
                _rightUpperArm = _rightLowerArm;
            if (_leftUpperArm == null)
                _leftUpperArm = _leftLowerArm;

            CacheFingerBones(_leftFingerBones, LeftFingerBoneMap);
            CacheFingerBones(_rightFingerBones, RightFingerBoneMap);
            CachePalmAuthoringTargets();
        }

        private void CachePalmAuthoringTargets()
        {
            _rightPalmAuthoringTarget =
                FindChildIncludingAuthoring(
                    transform,
                    WeaponGripTargetName,
                    RightHandIkTargetName,
                    WeaponMainSocketName,
                    "RightHand");
            _leftPalmAuthoringTarget =
                FindChildIncludingAuthoring(
                    transform,
                    OffhandGripTargetName,
                    LeftHandIkTargetName,
                    WeaponOffhandSocketName,
                    "LeftHand");
        }

        private Transform Bone(HumanBodyBones bone, string[] fallbackNames = null)
        {
            if (_animator != null && _animator.isHuman)
            {
                Transform humanoidBone = _animator.GetBoneTransform(bone);
                if (humanoidBone != null)
                    return humanoidBone;
            }

            return fallbackNames != null
                ? FindBone(_animator != null ? _animator.transform : transform, fallbackNames)
                : null;
        }

        private void CaptureBasePose()
        {
            if (!HasUsablePoseRig())
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

        private bool HasUsablePoseRig()
        {
            return _hips != null ||
                   _spine != null ||
                   _head != null ||
                   _leftUpperArm != null ||
                   _rightUpperArm != null ||
                   _leftUpperLeg != null ||
                   _rightUpperLeg != null;
        }

        private static bool LooksLikeSparseRig(Transform root)
        {
            if (root == null)
                return false;

            if (FindBone(root, HipBoneNames) != null &&
                FindBone(root, SpineBoneNames) != null &&
                (FindBone(root, LeftUpperArmBoneNames) != null ||
                 FindBone(root, RightUpperArmBoneNames) != null))
            {
                return true;
            }

            return FindVisualPart(root, side: 1f, preferHand: false) != null ||
                   FindVisualPart(root, side: -1f, preferHand: false) != null;
        }

        private static Transform FindBone(Transform root, string[] names)
        {
            if (root == null || names == null)
                return null;

            return FindBone(root, names, allowAuthoringSockets: false);
        }

        private static Transform FindBone(
            Transform root,
            string[] names,
            bool allowAuthoringSockets)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform result = FindBone(root, names[i], allowAuthoringSockets);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static Transform FindBone(Transform root, string name)
        {
            return FindBone(root, name, allowAuthoringSockets: false);
        }

        private static Transform FindBone(
            Transform root,
            string name,
            bool allowAuthoringSockets)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            if (NameEquals(root.name, name) &&
                (allowAuthoringSockets || !IsAuthoringSocketBranch(root)))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform result = FindBone(child, name, allowAuthoringSockets);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static bool IsAuthoringSocketBranch(Transform transform)
        {
            for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            {
                string name = cursor.name;
                if (NameEquals(name, "Sockets") ||
                    NameEquals(name, "AttachmentSockets") ||
                    NameEquals(name, "HandPoseTargets") ||
                    NameEquals(name, "RuntimeAttachments") ||
                    NameEquals(name, RuntimePalmAnchorRootName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NameEquals(string actual, string expected)
        {
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expected))
                return false;

            if (string.Equals(
                    actual,
                    expected,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(
                NormalizeName(actual),
                NormalizeName(expected),
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private static Transform FindVisualPart(
            Transform root,
            float side,
            bool preferHand)
        {
            Transform best = null;
            int bestScore = 0;
            ScoreVisualPart(root, side, preferHand, ref best, ref bestScore);
            return best;
        }

        private static void ScoreVisualPart(
            Transform candidate,
            float side,
            bool preferHand,
            ref Transform best,
            ref int bestScore)
        {
            if (candidate == null ||
                IsAuthoringSocketBranch(candidate) ||
                IsAttachmentLikeBranch(candidate))
            {
                return;
            }

            string name = NormalizeName(candidate.name);
            int score = ScoreVisualPartName(name, side, preferHand);
            Renderer renderer = candidate.GetComponent<Renderer>();
            if (renderer != null)
                score += 8;

            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }

            for (int i = 0; i < candidate.childCount; i++)
                ScoreVisualPart(candidate.GetChild(i), side, preferHand, ref best, ref bestScore);
        }

        private static int ScoreVisualPartName(
            string normalizedName,
            float side,
            bool preferHand)
        {
            if (string.IsNullOrEmpty(normalizedName) ||
                IsAttachmentLikeName(normalizedName))
            {
                return -1000;
            }

            bool right = side >= 0f;
            bool hasSide =
                normalizedName.Contains(right ? "right" : "left") ||
                normalizedName.Contains(right ? "rhand" : "lhand") ||
                normalizedName.Contains(right ? "rarm" : "larm") ||
                normalizedName.EndsWith(right ? "r" : "l");
            bool hasHand =
                normalizedName.Contains("hand") ||
                normalizedName.Contains("palm") ||
                normalizedName.Contains("fist");
            bool hasArm =
                normalizedName.Contains("arm") ||
                normalizedName.Contains("forearm") ||
                normalizedName.Contains("upperarm") ||
                normalizedName.Contains("lowerarm");

            if (!hasSide || (!hasHand && !hasArm))
                return 0;

            int score = 20;
            score += hasHand ? (preferHand ? 42 : 22) : 0;
            score += hasArm ? (preferHand ? 18 : 38) : 0;
            score += normalizedName.Contains("forearm") ||
                     normalizedName.Contains("lowerarm")
                ? 12
                : 0;
            return score;
        }

        private static bool IsAttachmentLikeBranch(Transform transform)
        {
            for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
            {
                if (IsAttachmentLikeName(NormalizeName(cursor.name)))
                    return true;
            }

            return false;
        }

        private static bool IsAttachmentLikeName(string normalizedName)
        {
            return !string.IsNullOrEmpty(normalizedName) &&
                   (normalizedName.Contains("weapon") ||
                    normalizedName.Contains("muzzle") ||
                    normalizedName.Contains("socket") ||
                    normalizedName.Contains("attachment") ||
                    normalizedName.Contains("grip") ||
                    normalizedName.Contains("target") ||
                    normalizedName.Contains("pistol") ||
                    normalizedName.Contains("gun") ||
                    normalizedName.Contains("bow") ||
                    normalizedName.Contains("umbrella") ||
                    normalizedName.Contains("staff") ||
                    normalizedName.Contains("bottle") ||
                    normalizedName.Contains("star"));
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
            float attackPulse =
                _runtime != null ? ResolveActionPulse(_runtime.MainAttackAgeSeconds, 0.18f) : attack;
            float superPulse =
                _runtime != null ? ResolveActionPulse(_runtime.SuperAgeSeconds, 0.28f) : super;
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

            AddLocal(_hips, _hipsBase, idleBreath * 1.1f - gaitMove01 * 3.8f, idleLook * 0.7f, strideCos * gaitMove01 * 2.4f, weight);
            AddLocal(_spine, _spineBase, idleBreath * -1.0f + gaitMove01 * 3.2f - hit * 7.0f, idleLook * 0.8f, -strideCos * gaitMove01 * 1.9f, weight);
            AddLocal(_chest, _chestBase, idleBreath * -0.7f + super * -5.5f, idleLook * 1.1f, strideCos * gaitMove01 * 1.4f + hyper * 2.0f, weight);
            AddLocal(_head, _headBase, idleBreath * 0.7f - attack * 3.0f, idleLook * 2.2f, -strideCos * gaitMove01 * 0.7f, weight);
            ApplyStylizedLocomotionAccent(
                strideSin,
                strideCos,
                gaitMove01,
                gaitRun01,
                move01,
                run01,
                weight);

            float rightReady = ResolveArmReady(gripPose, side: 1f, ready);
            float leftReady = ResolveArmReady(gripPose, side: -1f, ready);
            float rightAttack = ResolveArmAction(gripPose, side: 1f, attack);
            float leftAttack = ResolveArmAction(gripPose, side: -1f, attack * 0.65f);

            PoseArm(
                _rightUpperArm,
                _rightLowerArm,
                _rightHand,
                1f,
                rightReady,
                rightAttack,
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
                leftReady,
                leftAttack,
                super,
                -strideSin,
                gaitMove01 * _armSwingScale,
                forward,
                right,
                up,
                aim,
                gripPose,
                weight);

            ApplyWeaponReadyHandAnchors(
                gripPose,
                ready,
                attack,
                super,
                forward,
                right,
                up,
                aim,
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
                attackPulse,
                superPulse,
                hit,
                hyper,
                forward,
                right,
                up,
                aim,
                weight);
            ApplyShowcaseIdleAccent(
                gripPose,
                time,
                ready,
                move01,
                forward,
                right,
                up,
                aim,
                weight);
            UpdateRuntimePalmAnchors();
            ApplyAttachmentFollowersNow();
            ApplyAuthoredHandPoseTargets(ready, attack, super, weight);
            UpdateRuntimePalmAnchors();
            ApplyAttachmentFollowersNow();
            ApplyRuntimeGripTargets(weight);
            UpdateRuntimePalmAnchors();
            ApplyAttachmentFollowersNow();
        }

        private void ApplyStylizedLocomotionAccent(
            float strideSin,
            float strideCos,
            float gaitMove01,
            float gaitRun01,
            float move01,
            float run01,
            float weight)
        {
            float energy =
                Mathf.Clamp01(move01 * 0.78f + run01 * 0.46f) *
                Mathf.Clamp01(gaitMove01 + gaitRun01 * 0.35f);
            if (energy <= 0.001f || weight <= 0f)
                return;

            float bounce = Mathf.Abs(strideSin) * energy * _locomotionBounceScale;
            float sway = strideCos * energy * _locomotionSwaggerScale;
            float lean = energy * _locomotionLeanScale;
            float heavy = _animationPersona == AnimationPersona.Heavyweight ? 1f : 0f;
            float robot = _animationPersona == AnimationPersona.Robot ? 1f : 0f;
            float light = _animationPersona == AnimationPersona.Assassin ? 1f : 0f;

            AddLocal(
                _hips,
                LocalRotation(_hips),
                -lean * Mathf.Lerp(1.25f, 2.35f, heavy) - bounce * Mathf.Lerp(1.15f, 1.90f, heavy),
                sway * Mathf.Lerp(0.45f, 0.20f, robot),
                sway * Mathf.Lerp(0.95f, 1.42f, light),
                weight * energy);
            AddLocal(
                _spine,
                LocalRotation(_spine),
                lean * Mathf.Lerp(0.72f, 1.18f, heavy) + bounce * Mathf.Lerp(0.90f, 0.42f, robot),
                -sway * Mathf.Lerp(0.34f, 0.10f, robot),
                -sway * Mathf.Lerp(0.78f, 1.18f, light),
                weight * energy);
            AddLocal(
                _chest,
                LocalRotation(_chest),
                -lean * Mathf.Lerp(0.38f, 0.75f, heavy) + bounce * 0.38f,
                sway * Mathf.Lerp(0.62f, 0.18f, robot),
                sway * Mathf.Lerp(0.80f, 1.08f, light),
                weight * energy);
            AddLocal(
                _head,
                LocalRotation(_head),
                bounce * Mathf.Lerp(0.58f, 0.30f, heavy),
                -sway * Mathf.Lerp(0.75f, 0.30f, robot),
                -sway * 0.36f,
                weight * energy);
        }

        private void ApplyShowcaseIdleAccent(
            BrawlerAttachmentGripPose gripPose,
            float time,
            float ready,
            float move01,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            if (!_useShowcasePose || weight <= 0f)
                return;

            float idle01 = Mathf.Clamp01(1f - move01 * 1.8f);
            if (idle01 <= 0.001f)
                return;

            float pulse = ResolveShowcaseInspectPulse(time);
            float inspect =
                pulse *
                idle01 *
                Mathf.Clamp01(0.42f + ready * 0.72f) *
                _showcaseInspectScale;
            float breathe =
                Mathf.Sin(time * 1.12f * _showcaseTempoScale + _gaitPhaseOffset) *
                idle01;
            float look =
                Mathf.Sin(time * 0.46f * _showcaseTempoScale + _gaitPhaseOffset * 0.33f) *
                idle01;

            AddLocal(
                _spine,
                LocalRotation(_spine),
                breathe * 1.15f,
                look * 0.70f,
                -look * 0.46f,
                weight * idle01);
            AddLocal(
                _chest,
                LocalRotation(_chest),
                -breathe * 0.92f,
                look * 1.05f,
                look * 0.62f,
                weight * idle01);
            AddLocal(
                _head,
                LocalRotation(_head),
                breathe * 0.72f,
                look * 2.15f,
                -look * 0.38f,
                weight * idle01);

            if (inspect <= 0.001f)
                return;

            Vector3 showcaseAim = ResolveShowcaseCameraAim(aim);
            Quaternion aimRotation =
                Quaternion.LookRotation(aim.sqrMagnitude > 0.001f ? aim : forward, up);
            Quaternion showcaseAimRotation =
                Quaternion.LookRotation(showcaseAim, up);
            Vector3 anchor = ResolveUpperBodyAnchor(up);

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.DualSidearm:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + showcaseAim * 0.54f + right * 0.16f + up * 0.05f,
                        showcaseAimRotation,
                        weight * inspect * 0.78f);
                    AddLocal(
                        _rightUpperArm,
                        LocalRotation(_rightUpperArm),
                        -14.0f * inspect,
                        2.0f * inspect,
                        -24.0f * inspect,
                        weight * inspect * 0.72f);
                    AddLocal(
                        _rightHand,
                        LocalRotation(_rightHand),
                        -3.0f * inspect,
                        8.0f * inspect,
                        -8.0f * inspect,
                        weight * inspect);
                    AddLocal(
                        _leftUpperArm,
                        LocalRotation(_leftUpperArm),
                        2.0f * inspect,
                        -8.0f * inspect,
                        10.0f * inspect,
                        weight * inspect * 0.42f);
                    AddLocal(
                        _leftHand,
                        LocalRotation(_leftHand),
                        -4.0f * inspect,
                        -10.0f * inspect,
                        12.0f * inspect,
                        weight * inspect * 0.72f);
                    break;

                case BrawlerAttachmentGripPose.Sidearm:
                case BrawlerAttachmentGripPose.ThrowingStars:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.24f + right * 0.24f + up * 0.04f,
                        aimRotation,
                        weight * inspect * 0.44f);
                    AddLocal(
                        _rightHand,
                        LocalRotation(_rightHand),
                        -6.0f * inspect,
                        14.0f * inspect,
                        -20.0f * inspect,
                        weight * inspect);
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.26f + right * 0.18f + up * 0.01f,
                        aimRotation,
                        weight * inspect * 0.36f);
                    ApplyHandAnchor(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        anchor + aim * 0.30f - right * 0.18f - up * 0.03f,
                        aimRotation,
                        weight * inspect * 0.28f);
                    AddLocal(
                        _rightHand,
                        LocalRotation(_rightHand),
                        -5.0f * inspect,
                        8.0f * inspect,
                        -12.0f * inspect,
                        weight * inspect);
                    break;

                case BrawlerAttachmentGripPose.Bottle:
                    AddLocal(
                        _rightUpperArm,
                        LocalRotation(_rightUpperArm),
                        -12.0f * inspect,
                        5.0f * inspect,
                        -8.0f * inspect,
                        weight * inspect);
                    AddLocal(
                        _rightHand,
                        LocalRotation(_rightHand),
                        -18.0f * inspect,
                        6.0f * inspect,
                        -14.0f * inspect,
                        weight * inspect);
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    ApplyHandAnchor(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        anchor + aim * 0.28f - right * 0.25f + up * 0.01f,
                        aimRotation,
                        weight * inspect * 0.38f);
                    AddLocal(
                        _rightLowerArm,
                        LocalRotation(_rightLowerArm),
                        8.0f * inspect,
                        6.0f * inspect,
                        -10.0f * inspect,
                        weight * inspect);
                    break;
            }
        }

        private Vector3 ResolveShowcaseCameraAim(Vector3 fallback)
        {
            Vector3 fallbackAim =
                fallback.sqrMagnitude > 0.001f
                    ? fallback.normalized
                    : ResolveForward();

            Camera camera = Camera.main;
            if (camera == null)
                return fallbackAim;

            Vector3 origin = ResolveUpperBodyAnchor(Vector3.up);
            Vector3 toCamera = camera.transform.position - origin;
            if (toCamera.sqrMagnitude < 0.0001f)
                return fallbackAim;

            return toCamera.normalized;
        }

        private float ResolveShowcaseInspectPulse(float time)
        {
            float phase = time * 0.82f * _showcaseTempoScale + _gaitPhaseOffset;
            float wave = Mathf.Sin(phase) * 0.5f + 0.5f;
            float pulse = Mathf.SmoothStep(0.54f, 1f, wave);
            return pulse * pulse;
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

        public void RefreshPalmAnchorsNow()
        {
            UpdateRuntimePalmAnchors();
        }

        private void EnsureRuntimePalmAnchors()
        {
            _runtimePalmAnchorRoot = GetOrCreateRuntimeChild(transform, RuntimePalmAnchorRootName);
            _runtimePalmAnchorRoot.localPosition = Vector3.zero;
            _runtimePalmAnchorRoot.localRotation = Quaternion.identity;
            _runtimePalmAnchorRoot.localScale = Vector3.one;

            Transform rightPalmSource = ResolvePalmParent(_rightHand, _rightLowerArm, _rightUpperArm);
            Transform leftPalmSource = ResolvePalmParent(_leftHand, _leftLowerArm, _leftUpperArm);
            if (_rightPalmSource != rightPalmSource)
            {
                _rightPalmSource = rightPalmSource;
                _rightPalmSourceRenderers = ResolvePalmSourceRenderers(rightPalmSource);
            }

            if (_leftPalmSource != leftPalmSource)
            {
                _leftPalmSource = leftPalmSource;
                _leftPalmSourceRenderers = ResolvePalmSourceRenderers(leftPalmSource);
            }

            _rightPalmUsesSparseSource =
                (_rightHand == null || _rightHandIsVisualFallback) &&
                _rightPalmSource != null;
            _leftPalmUsesSparseSource =
                (_leftHand == null || _leftHandIsVisualFallback) &&
                _leftPalmSource != null;

            Transform rightPalmParent = rightPalmSource != null
                ? rightPalmSource
                : _runtimePalmAnchorRoot;
            Transform leftPalmParent = leftPalmSource != null
                ? leftPalmSource
                : _runtimePalmAnchorRoot;

            _rightPalmSocket = EnsureLivePalmSocket(
                _rightPalmSocket,
                ref _rightPalmParent,
                rightPalmParent,
                RightPalmSocketName,
                WeaponMainSocketName);
            _leftPalmSocket = EnsureLivePalmSocket(
                _leftPalmSocket,
                ref _leftPalmParent,
                leftPalmParent,
                LeftPalmSocketName,
                WeaponOffhandSocketName);
        }

        private void UpdateRuntimePalmAnchors()
        {
            EnsureRuntimePalmAnchors();
            UpdatePalmSocket(
                _rightPalmSocket,
                _rightPalmSource,
                _rightPalmSourceRenderers,
                _rightPalmAuthoringTarget,
                side: 1f,
                useSparseSource: _rightPalmUsesSparseSource);
            UpdatePalmSocket(
                _leftPalmSocket,
                _leftPalmSource,
                _leftPalmSourceRenderers,
                _leftPalmAuthoringTarget,
                side: -1f,
                useSparseSource: _leftPalmUsesSparseSource);
        }

        private Transform EnsureLivePalmSocket(
            Transform socket,
            ref Transform cachedParent,
            Transform parent,
            string name,
            string weaponSocketName)
        {
            parent = parent != null ? parent : transform;

            bool createdOrMoved = socket == null || cachedParent != parent || socket.parent != parent;
            if (socket == null)
                socket = FindRuntimePalmSocket(name);

            if (socket == null)
            {
                GameObject socketObject = new GameObject(name);
                socket = socketObject.transform;
                createdOrMoved = true;
            }

            if (socket.parent != parent)
            {
                socket.SetParent(parent, true);
                createdOrMoved = true;
            }

            if (createdOrMoved)
                socket.localScale = Vector3.one;
            ResetWeaponSocketChild(socket, weaponSocketName);
            cachedParent = parent;
            return socket;
        }

        private static Transform ResolvePalmParent(
            Transform hand,
            Transform lowerArm,
            Transform upperArm)
        {
            if (hand != null)
                return hand;
            if (lowerArm != null)
                return lowerArm;
            return upperArm;
        }

        private void UpdatePalmSocket(
            Transform socket,
            Transform source,
            Renderer[] sourceRenderers,
            Transform authoringTarget,
            float side,
            bool useSparseSource)
        {
            if (socket == null)
                return;

            Vector3 position;
            Quaternion rotation;
            if (source != null)
            {
                if (useSparseSource &&
                    TryResolveSparsePalmWorldPose(source, sourceRenderers, side, out position, out rotation))
                {
                    socket.SetPositionAndRotation(position, rotation);
                }
                else
                {
                    Vector3 worldOffset = ResolvePalmWorldOffset(side);
                    socket.SetPositionAndRotation(source.position + worldOffset, source.rotation);
                }
            }
            else if (authoringTarget != null && !IsDescendantOf(authoringTarget, socket))
            {
                socket.SetPositionAndRotation(authoringTarget.position, authoringTarget.rotation);
            }
            else
            {
                Vector3 forward = ResolveForward();
                Vector3 right = ResolveRight(forward);
                position =
                    transform.position +
                    forward * RuntimePalmForwardOffset +
                    right * side * RuntimePalmSideOffset +
                    Vector3.up * RuntimePalmUpOffset;
                rotation = Quaternion.LookRotation(forward, Vector3.up);
                socket.SetPositionAndRotation(position, rotation);
            }

            socket.localScale = Vector3.one;
            ResetWeaponSocketChild(
                socket,
                side >= 0f ? WeaponMainSocketName : WeaponOffhandSocketName);
        }

        private Transform FindRuntimePalmSocket(string name)
        {
            return FindChildIncludingAuthoring(transform, name);
        }

        private static Transform FindChildIncludingAuthoring(
            Transform root,
            params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = FindBone(root, names[i], allowAuthoringSockets: true);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool IsDescendantOf(Transform candidate, Transform ancestor)
        {
            for (Transform cursor = candidate; cursor != null; cursor = cursor.parent)
            {
                if (cursor == ancestor)
                    return true;
            }

            return false;
        }

        private Renderer[] ResolvePalmSourceRenderers(Transform source)
        {
            if (source == null)
                return new Renderer[0];

            Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Renderer[0];

            List<Renderer> usable = new List<Renderer>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    IsAuthoringSocketBranch(renderer.transform) ||
                    IsAttachmentLikeBranch(renderer.transform))
                {
                    continue;
                }

                usable.Add(renderer);
            }

            return usable.Count > 0 ? usable.ToArray() : new Renderer[0];
        }

        private Vector3 ResolvePalmWorldOffset(float side)
        {
            Vector3 forward = ResolveForward();
            Vector3 right = ResolveRight(forward);
            return
                forward * RuntimePalmForwardOffset +
                right * side * RuntimePalmSideOffset +
                Vector3.up * RuntimePalmUpOffset;
        }

        private bool TryResolveSparsePalmWorldPose(
            Transform source,
            Renderer[] sourceRenderers,
            float side,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            Vector3 forward = ResolveForward();
            rotation = Quaternion.LookRotation(forward, Vector3.up);

            Bounds localBounds;
            if (source == null ||
                !TryCalculateLocalRendererBounds(source, sourceRenderers, out localBounds))
            {
                return false;
            }

            float sideSign = side >= 0f ? 1f : -1f;
            float sideOffset =
                Mathf.Max(localBounds.extents.x * SparsePalmSideExtentBias, SparsePalmMinimumSide) *
                sideSign;
            float downOffset =
                Mathf.Max(localBounds.extents.y * SparsePalmDownExtentBias, SparsePalmMinimumDrop);
            float forwardOffset =
                Mathf.Max(localBounds.extents.z * SparsePalmForwardExtentBias, SparsePalmForwardBias);

            Vector3 localPalm =
                localBounds.center +
                new Vector3(sideOffset, -downOffset, forwardOffset);
            position = source.TransformPoint(localPalm);
            return true;
        }

        private static bool TryCalculateLocalRendererBounds(
            Transform source,
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (source == null || renderers == null)
                return false;

            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                Bounds world = renderer.bounds;
                Vector3 extents = world.extents;
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(-extents.x, -extents.y, -extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(-extents.x, -extents.y, extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(-extents.x, extents.y, -extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(-extents.x, extents.y, extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(extents.x, -extents.y, -extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(extents.x, -extents.y, extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(extents.x, extents.y, -extents.z),
                    ref bounds,
                    ref hasBounds);
                EncapsulateLocalPoint(
                    source,
                    world.center + new Vector3(extents.x, extents.y, extents.z),
                    ref bounds,
                    ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateLocalPoint(
            Transform root,
            Vector3 worldPoint,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            Vector3 localPoint = root.InverseTransformPoint(worldPoint);
            if (!hasBounds)
            {
                bounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(localPoint);
        }

        private static void ResetWeaponSocketChild(
            Transform palmSocket,
            string weaponSocketName)
        {
            if (palmSocket == null || string.IsNullOrEmpty(weaponSocketName))
                return;

            Transform weaponSocket = palmSocket.Find(weaponSocketName);
            if (weaponSocket == null)
            {
                GameObject socketObject = new GameObject(weaponSocketName);
                weaponSocket = socketObject.transform;
                weaponSocket.SetParent(palmSocket, false);
            }

            weaponSocket.localPosition = Vector3.zero;
            weaponSocket.localRotation = Quaternion.identity;
            weaponSocket.localScale = Vector3.one;
        }

        private static Transform GetOrCreateRuntimeChild(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null)
                return existing;

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
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

        private void ApplyAuthoredHandPoseTargets(
            float ready,
            float attack,
            float super,
            float weight)
        {
            if (_handPoseTargets == null || _handPoseTargets.Length == 0)
                return;

            float action01 = Mathf.Clamp01(attack + super);
            for (int i = 0; i < _handPoseTargets.Length; i++)
            {
                BrawlerHandPoseTargets targetSet = _handPoseTargets[i];
                if (targetSet == null)
                    continue;

                float targetSetWeight =
                    weight *
                    targetSet.ResolvePoseWeight(ready, action01, _useShowcasePose);
                if (targetSetWeight <= 0.001f)
                    continue;

                if (targetSet.TryGetRightHandPoseTarget(
                        out Transform rightTarget,
                        out float rightWeight,
                        out bool useRightRotation))
                {
                    ApplyHandPoseTarget(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        rightTarget,
                        targetSetWeight * rightWeight,
                        useRightRotation);
                }

                if (targetSet.TryGetRightFingerGrip(
                        out float rightFingerCurl,
                        out float rightThumbCurl,
                        out Vector3 rightHandOffset,
                        out float rightGripWeight))
                {
                    ApplyAuthoredFingerGrip(
                        _rightHand,
                        _rightFingerBones,
                        _rightFingerBase,
                        rightFingerCurl,
                        rightThumbCurl,
                        rightHandOffset,
                        1f,
                        targetSetWeight * rightGripWeight);
                }

                if (targetSet.TryGetLeftHandPoseTarget(
                        out Transform leftTarget,
                        out float leftWeight,
                        out bool useLeftRotation))
                {
                    ApplyHandPoseTarget(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        leftTarget,
                        targetSetWeight * leftWeight,
                        useLeftRotation);
                }

                if (targetSet.TryGetLeftFingerGrip(
                        out float leftFingerCurl,
                        out float leftThumbCurl,
                        out Vector3 leftHandOffset,
                        out float leftGripWeight))
                {
                    ApplyAuthoredFingerGrip(
                        _leftHand,
                        _leftFingerBones,
                        _leftFingerBase,
                        leftFingerCurl,
                        leftThumbCurl,
                        leftHandOffset,
                        -1f,
                        targetSetWeight * leftGripWeight);
                }
            }
        }

        private static void ApplyAuthoredFingerGrip(
            Transform hand,
            Transform[] fingerBones,
            Quaternion[] fingerBase,
            float fingerCurl,
            float thumbCurl,
            Vector3 localEulerOffset,
            float side,
            float weight)
        {
            float poseWeight = Mathf.Clamp01(weight);
            if (poseWeight <= 0f)
                return;

            PoseGripHand(
                hand,
                fingerBones,
                fingerBase,
                Mathf.Clamp01(fingerCurl),
                Mathf.Clamp01(thumbCurl),
                side,
                1f,
                poseWeight);

            if (localEulerOffset.sqrMagnitude > 0.000001f)
            {
                AddLocal(
                    hand,
                    hand != null ? hand.localRotation : Quaternion.identity,
                    localEulerOffset.x,
                    localEulerOffset.y,
                    localEulerOffset.z,
                    poseWeight);
            }
        }

        private static void ApplyHandPoseTarget(
            Transform upper,
            Transform lower,
            Transform hand,
            Transform target,
            float weight,
            bool useTargetRotation)
        {
            if (target == null)
                return;

            ApplyArmGripIk(
                upper,
                lower,
                hand,
                target.position,
                target.rotation,
                Mathf.Clamp01(weight),
                useTargetRotation,
                AuthoredHandRotationStrength);
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

        private static float ResolveArmReady(
            BrawlerAttachmentGripPose gripPose,
            float side,
            float ready)
        {
            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.ThrowingStars:
                    return side > 0f ? ready : ready * 0.08f;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    return side > 0f ? ready : ready * 0.16f;

                case BrawlerAttachmentGripPose.LongGun:
                case BrawlerAttachmentGripPose.Bow:
                case BrawlerAttachmentGripPose.DualSidearm:
                    return ready;

                default:
                    return ready;
            }
        }

        private static float ResolveArmAction(
            BrawlerAttachmentGripPose gripPose,
            float side,
            float action)
        {
            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.ThrowingStars:
                    return side > 0f ? action : action * 0.08f;

                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    return side > 0f ? action : action * 0.12f;

                default:
                    return action;
            }
        }

        private void ApplyWeaponReadyHandAnchors(
            BrawlerAttachmentGripPose gripPose,
            float ready,
            float attack,
            float super,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            if (gripPose == BrawlerAttachmentGripPose.None)
                return;

            float poseWeight =
                weight *
                Mathf.Clamp01(_gripPoseWeight) *
                Mathf.Clamp01(ready * 1.18f + attack * 0.35f + super * 0.25f);
            if (poseWeight <= 0.001f)
                return;

            Vector3 anchor = ResolveUpperBodyAnchor(up);
            Quaternion aimRotation = Quaternion.LookRotation(aim.sqrMagnitude > 0.001f ? aim : forward, up);

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Sidearm:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.43f + right * 0.22f - up * 0.08f,
                        aimRotation,
                        poseWeight * 0.72f);
                    break;

                case BrawlerAttachmentGripPose.DualSidearm:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.43f + right * 0.24f - up * 0.08f,
                        aimRotation,
                        poseWeight * 0.72f);
                    ApplyHandAnchor(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        anchor + aim * 0.43f - right * 0.24f - up * 0.08f,
                        aimRotation,
                        poseWeight * 0.72f);
                    break;

                case BrawlerAttachmentGripPose.LongGun:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.36f + right * 0.17f - up * 0.12f,
                        aimRotation,
                        poseWeight * 0.70f);
                    ApplyHandAnchor(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        anchor + aim * 0.52f - right * 0.17f - up * 0.08f,
                        aimRotation,
                        poseWeight * 0.64f);
                    break;

                case BrawlerAttachmentGripPose.LongTool:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.28f + right * 0.19f - up * 0.16f,
                        aimRotation,
                        poseWeight * 0.62f);
                    ApplyHandAnchor(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        anchor + aim * 0.34f - right * 0.13f - up * 0.12f,
                        aimRotation,
                        poseWeight * 0.32f);
                    break;

                case BrawlerAttachmentGripPose.Bottle:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.22f + right * 0.22f + up * 0.03f,
                        aimRotation,
                        poseWeight * 0.62f);
                    break;

                case BrawlerAttachmentGripPose.Bow:
                    ApplyHandAnchor(
                        _leftUpperArm,
                        _leftLowerArm,
                        _leftHand,
                        anchor + aim * 0.34f - right * 0.24f - up * 0.03f,
                        aimRotation,
                        poseWeight * 0.72f);
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.24f + right * 0.18f - up * 0.05f,
                        aimRotation,
                        poseWeight * 0.60f);
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.28f + right * 0.22f - up * 0.04f,
                        aimRotation,
                        poseWeight * 0.58f);
                    break;

                case BrawlerAttachmentGripPose.Umbrella:
                    ApplyHandAnchor(
                        _rightUpperArm,
                        _rightLowerArm,
                        _rightHand,
                        anchor + aim * 0.24f + right * 0.18f - up * 0.04f,
                        aimRotation,
                        poseWeight * 0.62f);
                    break;
            }
        }

        private Vector3 ResolveUpperBodyAnchor(Vector3 up)
        {
            if (_chest != null)
                return _chest.position - up * 0.04f;

            if (_spine != null)
                return _spine.position + up * 0.18f;

            if (_hips != null)
                return _hips.position + up * 0.72f;

            return transform.position + up * 0.96f;
        }

        private static void ApplyHandAnchor(
            Transform upper,
            Transform lower,
            Transform hand,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float weight)
        {
            ApplyArmGripIk(
                upper,
                lower,
                hand,
                targetPosition,
                targetRotation,
                Mathf.Clamp01(weight));
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
            if (upper == null)
                return;

            if (lower == null)
            {
                PoseSparseArm(
                    upper,
                    side,
                    ready,
                    attack,
                    super,
                    swing,
                    move01,
                    gripPose,
                    weight);
                return;
            }

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

        private void PoseSparseArm(
            Transform upper,
            float side,
            float ready,
            float attack,
            float super,
            float swing,
            float move01,
            BrawlerAttachmentGripPose gripPose,
            float weight)
        {
            float action = Mathf.Clamp01(attack + super);
            float runSwing = swing * move01 * Mathf.Lerp(5.0f, 12.0f, _smoothedRun01);
            float idleBreath = (1f - move01) *
                Mathf.Sin((_runtime != null ? _runtime.PoseTime : Time.unscaledTime) * 1.45f);
            float baseDrop = ResolveSparseArmDrop(gripPose, side, ready, action);
            Quaternion baseRotation = side > 0f ? _rightUpperArmBase : _leftUpperArmBase;

            AddLocal(
                upper,
                baseRotation,
                runSwing + action * -8.0f + idleBreath * 1.5f,
                side * (ready * 16.0f + action * 6.0f),
                baseDrop,
                weight);
        }

        private static float ResolveSparseArmDrop(
            BrawlerAttachmentGripPose gripPose,
            float side,
            float ready,
            float action)
        {
            float relaxedDrop = -side * 82.0f;
            float readyDrop = -side * 48.0f;

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.Bow:
                    readyDrop = side > 0f ? -36.0f : 44.0f;
                    break;
                case BrawlerAttachmentGripPose.DualSidearm:
                case BrawlerAttachmentGripPose.LongGun:
                    readyDrop = -side * 40.0f;
                    break;
                case BrawlerAttachmentGripPose.LongTool:
                case BrawlerAttachmentGripPose.Umbrella:
                    readyDrop = side > 0f ? -42.0f : 66.0f;
                    break;
                case BrawlerAttachmentGripPose.Bottle:
                case BrawlerAttachmentGripPose.ThrowingStars:
                    readyDrop = side > 0f ? -38.0f : 78.0f;
                    break;
            }

            return Mathf.Lerp(relaxedDrop, readyDrop, Mathf.Clamp01(ready + action * 0.65f));
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
            float attackPulse,
            float superPulse,
            float hit,
            float hyper,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            float shot = Mathf.Clamp01(Mathf.Max(attack, attackPulse) * _actionSnapScale);
            float ability = Mathf.Clamp01(Mathf.Max(super, superPulse) * _superSnapScale);
            float impact = Mathf.Clamp01(hit);
            float action = Mathf.Clamp01(shot + ability);
            float accent = Mathf.Clamp01(action + impact + hyper * 0.35f);
            if (accent <= 0.001f || weight <= 0f)
                return;

            float yawToAim = SignedPlanarAngle(forward, aim, up);
            float torsoAim = Mathf.Clamp(yawToAim, -36f, 36f) * action;
            float releaseKick = Mathf.Clamp01(attackPulse + superPulse * 0.85f);
            float recoil =
                ResolveRecoilAmount(gripPose, shot, ability) *
                Mathf.Lerp(0.72f, 1.22f, releaseKick) *
                Mathf.Lerp(1f, 1.18f, Mathf.Clamp01(hyper));

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
            ApplyPersonaActionAccent(
                gripPose,
                shot,
                ability,
                releaseKick,
                hyper,
                weight);

            switch (gripPose)
            {
                case BrawlerAttachmentGripPose.None:
                    ApplyUnarmedActionAccent(
                        shot,
                        ability,
                        recoil,
                        forward,
                        right,
                        up,
                        aim,
                        weight);
                    break;

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
                    ApplyBowActionAccent(shot, ability, forward, right, up, aim, weight);
                    break;

                case BrawlerAttachmentGripPose.ThrowingStars:
                    ApplyThrowingStarActionAccent(shot, ability, weight);
                    break;
            }
        }

        private void ApplyPersonaActionAccent(
            BrawlerAttachmentGripPose gripPose,
            float shot,
            float ability,
            float releaseKick,
            float hyper,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0.001f)
                return;

            float commit = Mathf.Clamp01(action + releaseKick * 0.42f);
            float superCommit = Mathf.Clamp01(ability + hyper * 0.28f);
            switch (_animationPersona)
            {
                case AnimationPersona.Gunslinger:
                    AddLocal(
                        _hips,
                        LocalRotation(_hips),
                        -2.2f * commit,
                        0f,
                        -2.8f * commit,
                        weight * commit);
                    AddLocal(
                        _chest,
                        LocalRotation(_chest),
                        -3.4f * commit - 2.4f * superCommit,
                        3.6f * releaseKick,
                        5.2f * commit,
                        weight * commit);
                    AddLocal(
                        _head,
                        LocalRotation(_head),
                        -1.8f * commit,
                        -2.4f * releaseKick,
                        -1.4f * commit,
                        weight * commit);
                    break;

                case AnimationPersona.Heavyweight:
                    AddLocal(
                        _hips,
                        LocalRotation(_hips),
                        -5.2f * commit - 4.0f * superCommit,
                        0f,
                        1.4f * releaseKick,
                        weight * commit);
                    AddLocal(
                        _chest,
                        LocalRotation(_chest),
                        -2.8f * commit - 7.2f * superCommit,
                        0f,
                        -5.4f * commit,
                        weight * commit);
                    AddLocal(
                        _head,
                        LocalRotation(_head),
                        -2.0f * commit,
                        0f,
                        2.2f * releaseKick,
                        weight * commit);
                    break;

                case AnimationPersona.Engineer:
                    AddLocal(
                        _chest,
                        LocalRotation(_chest),
                        -1.8f * commit,
                        2.0f * releaseKick,
                        2.8f * commit,
                        weight * commit);
                    AddLocal(
                        _head,
                        LocalRotation(_head),
                        -0.8f * commit,
                        -1.6f * releaseKick,
                        0.8f * commit,
                        weight * commit);
                    break;

                case AnimationPersona.Archer:
                    AddLocal(
                        _chest,
                        LocalRotation(_chest),
                        1.4f * commit - 2.8f * superCommit,
                        -3.2f * commit,
                        -3.8f * commit,
                        weight * commit);
                    AddLocal(
                        _head,
                        LocalRotation(_head),
                        -1.0f * commit,
                        -2.2f * commit,
                        0.8f * commit,
                        weight * commit);
                    break;

                case AnimationPersona.Robot:
                    AddLocal(
                        _chest,
                        LocalRotation(_chest),
                        -2.2f * commit,
                        0f,
                        Mathf.Sign(releaseKick - 0.5f) * 2.0f * commit,
                        weight * commit);
                    break;

                case AnimationPersona.Support:
                case AnimationPersona.Sniper:
                    AddLocal(
                        _spine,
                        LocalRotation(_spine),
                        -1.2f * commit,
                        0f,
                        1.5f * commit,
                        weight * commit);
                    AddLocal(
                        _head,
                        LocalRotation(_head),
                        -0.8f * commit,
                        -1.4f * releaseKick,
                        0.6f * commit,
                        weight * commit);
                    break;

                case AnimationPersona.Assassin:
                    AddLocal(
                        _chest,
                        LocalRotation(_chest),
                        -2.5f * commit,
                        4.0f * releaseKick,
                        -6.0f * commit,
                        weight * commit);
                    AddLocal(
                        _head,
                        LocalRotation(_head),
                        -1.4f * commit,
                        2.2f * releaseKick,
                        2.0f * commit,
                        weight * commit);
                    break;
            }

            if (gripPose == BrawlerAttachmentGripPose.None && _animationPersona != AnimationPersona.Heavyweight)
            {
                AddLocal(
                    _chest,
                    LocalRotation(_chest),
                    -2.2f * superCommit,
                    0f,
                    -2.0f * commit,
                    weight * commit);
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

            float gunslinger = _animationPersona == AnimationPersona.Gunslinger ? 1f : 0f;
            float snap = Mathf.Lerp(1f, 1.24f, gunslinger);
            float flourish = gunslinger * Mathf.Clamp01(shot * 0.88f + ability * 0.55f);
            AddLocal(
                upper,
                upper != null ? upper.localRotation : Quaternion.identity,
                -recoil * 0.55f * snap - flourish * 1.6f,
                side * (2.5f + flourish * 2.6f) * action,
                side * (-2.0f - flourish * 3.4f) * action,
                weight * action);
            AddLocal(
                lower,
                lower != null ? lower.localRotation : Quaternion.identity,
                -recoil * 0.75f * snap,
                side * (2.0f + flourish * 3.8f) * action,
                side * (-2.5f - flourish * 5.2f) * action,
                weight * action);
            AddLocal(
                hand,
                hand != null ? hand.localRotation : Quaternion.identity,
                -recoil * 1.35f * snap,
                side * (3.5f + flourish * 8.5f) * action,
                side * (-4.0f - flourish * 12.0f) * action,
                weight * action);
        }

        private void ApplyUnarmedActionAccent(
            float shot,
            float ability,
            float recoil,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            Vector3 anchor = ResolveUpperBodyAnchor(up);
            Quaternion punchRotation =
                Quaternion.LookRotation(aim.sqrMagnitude > 0.001f ? aim : forward, up);
            float personaHeavy = _animationPersona == AnimationPersona.Heavyweight ? 1f : 0f;
            float heavy = Mathf.Clamp01(
                Mathf.Max(ability * 1.25f, personaHeavy * (shot * 0.76f + ability)));
            float lunge = Mathf.Clamp01(shot * 0.72f + ability + personaHeavy * action * 0.16f);
            float rightPunch = Mathf.Clamp01(action * Mathf.Lerp(0.76f, 1.0f, heavy));
            float leftGuard = Mathf.Clamp01(action * Mathf.Lerp(0.42f, 0.72f, heavy));

            ApplyHandAnchor(
                _rightUpperArm,
                _rightLowerArm,
                _rightHand,
                anchor + aim * Mathf.Lerp(0.44f, 0.66f, heavy) + right * 0.18f - up * 0.02f,
                punchRotation,
                weight * rightPunch * Mathf.Lerp(0.74f, 0.90f, personaHeavy));
            ApplyHandAnchor(
                _leftUpperArm,
                _leftLowerArm,
                _leftHand,
                anchor + aim * Mathf.Lerp(0.30f, 0.48f, heavy) - right * 0.22f - up * 0.04f,
                punchRotation,
                weight * leftGuard * 0.56f);

            AddLocal(
                _hips,
                _hips != null ? _hips.localRotation : Quaternion.identity,
                -lunge * Mathf.Lerp(1.5f, 5.0f, personaHeavy),
                0f,
                3.8f * heavy,
                weight * action);
            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                -recoil * 0.42f - heavy * 9.0f,
                7.0f * action,
                -10.0f * action - 4.0f * heavy,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                -recoil * 0.56f - heavy * 6.5f,
                5.0f * action,
                -7.0f * action,
                weight * action);
            AddLocal(
                _leftUpperArm,
                _leftUpperArm != null ? _leftUpperArm.localRotation : Quaternion.identity,
                -heavy * 4.5f,
                -4.0f * leftGuard,
                5.5f * leftGuard,
                weight * leftGuard);
            AddLocal(
                _chest,
                _chest != null ? _chest.localRotation : Quaternion.identity,
                -heavy * 7.0f - lunge * 2.0f,
                0f,
                -4.5f * action,
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

            float precision = _animationPersona == AnimationPersona.Support ||
                _animationPersona == AnimationPersona.Sniper
                    ? 1f
                    : 0f;
            float superRead = Mathf.Clamp01(ability * Mathf.Lerp(1f, 1.28f, precision));
            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                -recoil * Mathf.Lerp(0.35f, 0.48f, precision) - superRead * 2.8f,
                1.5f * action + precision * 2.2f * shot,
                -2.2f * action - precision * 1.8f * shot,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                -recoil * Mathf.Lerp(0.18f, 0.38f, precision) - superRead * 1.5f,
                precision * 2.0f * action,
                -precision * 2.5f * action,
                weight * action);
            AddLocal(
                _rightHand,
                _rightHand != null ? _rightHand.localRotation : Quaternion.identity,
                -recoil * Mathf.Lerp(0.9f, 1.12f, precision),
                2.0f * action + precision * 4.0f * shot,
                -3.0f * action - precision * 4.4f * shot,
                weight * action);
            AddLocal(
                _leftHand,
                _leftHand != null ? _leftHand.localRotation : Quaternion.identity,
                -superRead * 3.0f,
                -precision * 2.5f * action,
                precision * 3.2f * action,
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

            float thrower = _animationPersona == AnimationPersona.Robot ||
                _animationPersona == AnimationPersona.Thrower
                    ? 1f
                    : 0f;
            float windup = Mathf.Clamp01(action * Mathf.Lerp(1f, 1.24f, thrower));
            AddLocal(
                _chest,
                _chest != null ? _chest.localRotation : Quaternion.identity,
                -3.2f * windup,
                5.2f * windup,
                -4.5f * windup,
                weight * windup);
            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                (-10.5f - thrower * 5.0f) * action,
                (5.0f + thrower * 4.0f) * action,
                (-8.0f - thrower * 5.0f) * action,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                (-13.0f - thrower * 6.0f) * action,
                (3.0f + thrower * 2.0f) * action,
                (-6.0f - thrower * 6.0f) * action,
                weight * action);
            AddLocal(
                _rightHand,
                _rightHand != null ? _rightHand.localRotation : Quaternion.identity,
                (-18.0f - thrower * 9.0f) * action,
                (4.0f + thrower * 4.0f) * action,
                (-9.0f - thrower * 8.0f) * action,
                weight * action);
            AddLocal(
                _leftUpperArm,
                _leftUpperArm != null ? _leftUpperArm.localRotation : Quaternion.identity,
                3.0f * thrower * action,
                -5.5f * thrower * action,
                6.0f * thrower * action,
                weight * action);
        }

        private void ApplyBowActionAccent(
            float shot,
            float ability,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 aim,
            float weight)
        {
            float action = Mathf.Clamp01(shot + ability);
            if (action <= 0f)
                return;

            float archer = _animationPersona == AnimationPersona.Archer ? 1f : 0f;
            float draw = Mathf.Clamp01(action * Mathf.Lerp(1f, 1.30f, archer));
            Vector3 aimDirection = aim.sqrMagnitude > 0.001f ? aim : forward;
            Vector3 anchor = ResolveUpperBodyAnchor(up);
            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, up);

            if (archer > 0f)
            {
                ApplyHandAnchor(
                    _leftUpperArm,
                    _leftLowerArm,
                    _leftHand,
                    anchor + aimDirection * 0.48f - right * 0.24f - up * 0.02f,
                    aimRotation,
                    weight * draw * 0.45f);
                ApplyHandAnchor(
                    _rightUpperArm,
                    _rightLowerArm,
                    _rightHand,
                    anchor + aimDirection * 0.18f + right * 0.22f - up * 0.05f,
                    aimRotation,
                    weight * draw * 0.50f);
            }

            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                (5.0f + archer * 3.0f) * draw,
                (6.0f + archer * 4.0f) * draw,
                (-5.5f - archer * 6.0f) * draw,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                (8.0f + archer * 4.0f) * draw,
                (5.0f + archer * 3.0f) * draw,
                (-7.0f - archer * 8.0f) * draw,
                weight * action);
            AddLocal(
                _leftLowerArm,
                _leftLowerArm != null ? _leftLowerArm.localRotation : Quaternion.identity,
                (-3.0f - archer * 2.4f) * draw,
                (-2.0f - archer * 3.2f) * draw,
                (3.0f + archer * 5.0f) * draw,
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

            float assassin = _animationPersona == AnimationPersona.Assassin ? 1f : 0f;
            float flick = Mathf.Clamp01(action * Mathf.Lerp(1f, 1.28f, assassin));
            AddLocal(
                _chest,
                _chest != null ? _chest.localRotation : Quaternion.identity,
                -2.8f * assassin * flick,
                3.0f * assassin * flick,
                -5.0f * assassin * flick,
                weight * flick);
            AddLocal(
                _rightUpperArm,
                _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity,
                (-8.0f - assassin * 4.0f) * flick,
                (8.0f + assassin * 5.0f) * flick,
                (-10.0f - assassin * 6.0f) * flick,
                weight * action);
            AddLocal(
                _rightLowerArm,
                _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity,
                (-7.0f - assassin * 5.0f) * flick,
                (5.0f + assassin * 3.5f) * flick,
                (-8.0f - assassin * 5.0f) * flick,
                weight * action);
            AddLocal(
                _rightHand,
                _rightHand != null ? _rightHand.localRotation : Quaternion.identity,
                (-14.0f - assassin * 6.0f) * flick,
                (12.0f + assassin * 8.0f) * flick,
                (-18.0f - assassin * 12.0f) * flick,
                weight * action);
        }

        private static float ResolveActionPulse(float ageSeconds, float durationSeconds)
        {
            if (ageSeconds < 0f || ageSeconds >= durationSeconds)
                return 0f;

            float t = Mathf.Clamp01(ageSeconds / Mathf.Max(0.001f, durationSeconds));
            return 1f - Mathf.SmoothStep(0f, 1f, t);
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
            if (upper == null)
                return;

            if (lower == null)
            {
                PoseSparseLeg(upper, swing, move01, run01, weight);
                return;
            }

            float strideAmount =
                Mathf.Lerp(0.10f, 0.34f, run01) *
                move01 *
                _strideReachScale;
            float lift01 = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, swing));
            float planted01 = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, -swing));
            float stepLift =
                lift01 *
                Mathf.Lerp(0.010f, 0.044f, run01) *
                move01 *
                _footLiftScale;
            float plantPress = planted01 * Mathf.Lerp(0.018f, 0.040f, run01) * move01;
            Vector3 desiredUpper =
                (-up * 0.98f + forward * swing * strideAmount + up * (stepLift * 0.10f - plantPress * 0.18f)).normalized;
            RotateBoneToward(upper, lower, desiredUpper, weight * Mathf.Clamp01(move01 * 1.2f));

            if (foot == null)
                return;

            Vector3 desiredLower =
                (-up * 0.99f - forward * swing * strideAmount * 0.56f + up * (stepLift * 0.08f - plantPress * 0.20f)).normalized;
            RotateBoneToward(lower, foot, desiredLower, weight * Mathf.Clamp01(move01 * 1.1f));

            AddLocal(
                foot,
                foot.localRotation,
                -swing * Mathf.Lerp(3f, 9f, run01) * move01,
                0f,
                swing * Mathf.Lerp(0.8f, 2.2f, run01) * move01,
                weight * Mathf.Clamp01(move01 * 1.2f));
        }

        private void PoseSparseLeg(
            Transform upper,
            float swing,
            float move01,
            float run01,
            float weight)
        {
            Quaternion baseRotation =
                upper == _rightUpperLeg ? _rightUpperLegBase : _leftUpperLegBase;
            float stride = swing * Mathf.Lerp(8.0f, 18.0f, run01) * move01 * _strideReachScale;
            float lift = Mathf.Max(0f, swing) * Mathf.Lerp(2.0f, 6.0f, run01) * move01;

            AddLocal(
                upper,
                baseRotation,
                stride - lift,
                0f,
                swing * Mathf.Lerp(1.0f, 3.0f, run01) * move01,
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

        private void ConfigureAnimationStyle()
        {
            string name = ResolveBrawlerName();
            _animationPersona = AnimationPersona.Balanced;
            _locomotionBounceScale = 1f;
            _locomotionLeanScale = 1f;
            _locomotionSwaggerScale = 1f;
            _actionSnapScale = 1f;
            _superSnapScale = 1f;
            _showcaseInspectScale = 1f;
            _showcaseTempoScale = 1f;

            if (Contains(name, "Colt"))
            {
                _animationPersona = AnimationPersona.Gunslinger;
                _locomotionBounceScale = 1.16f;
                _locomotionLeanScale = 1.18f;
                _locomotionSwaggerScale = 1.22f;
                _actionSnapScale = 1.28f;
                _superSnapScale = 1.18f;
                _showcaseInspectScale = 1.35f;
                _showcaseTempoScale = 1.18f;
            }
            else if (Contains(name, "Jessie"))
            {
                _animationPersona = AnimationPersona.Engineer;
                _locomotionBounceScale = 1.10f;
                _locomotionLeanScale = 0.96f;
                _locomotionSwaggerScale = 1.10f;
                _actionSnapScale = 1.06f;
                _superSnapScale = 1.14f;
                _showcaseInspectScale = 1.18f;
                _showcaseTempoScale = 1.10f;
            }
            else if (Contains(name, "Barley"))
            {
                _animationPersona = AnimationPersona.Robot;
                _locomotionBounceScale = 0.70f;
                _locomotionLeanScale = 0.72f;
                _locomotionSwaggerScale = 0.60f;
                _actionSnapScale = 0.92f;
                _superSnapScale = 1.10f;
                _showcaseInspectScale = 0.82f;
                _showcaseTempoScale = 0.84f;
            }
            else if (Contains(name, "El Primo"))
            {
                _animationPersona = AnimationPersona.Heavyweight;
                _locomotionBounceScale = 1.34f;
                _locomotionLeanScale = 1.26f;
                _locomotionSwaggerScale = 0.84f;
                _actionSnapScale = 1.22f;
                _superSnapScale = 1.42f;
                _showcaseInspectScale = 0.88f;
                _showcaseTempoScale = 0.82f;
            }
            else if (Contains(name, "Bo"))
            {
                _animationPersona = AnimationPersona.Archer;
                _locomotionBounceScale = 0.92f;
                _locomotionLeanScale = 0.98f;
                _locomotionSwaggerScale = 0.86f;
                _actionSnapScale = 1.08f;
                _superSnapScale = 1.18f;
                _showcaseInspectScale = 1.06f;
                _showcaseTempoScale = 0.94f;
            }
            else if (Contains(name, "Byron"))
            {
                _animationPersona = AnimationPersona.Support;
                _locomotionBounceScale = 0.76f;
                _locomotionLeanScale = 0.72f;
                _locomotionSwaggerScale = 0.66f;
                _actionSnapScale = 0.94f;
                _superSnapScale = 1.16f;
                _showcaseInspectScale = 1.12f;
                _showcaseTempoScale = 0.78f;
            }
            else if (Contains(name, "Piper"))
            {
                _animationPersona = AnimationPersona.Sniper;
                _locomotionBounceScale = 0.82f;
                _locomotionLeanScale = 0.80f;
                _locomotionSwaggerScale = 0.72f;
                _actionSnapScale = 1.12f;
                _superSnapScale = 1.22f;
                _showcaseInspectScale = 1.22f;
                _showcaseTempoScale = 0.84f;
            }
            else if (Contains(name, "Leon"))
            {
                _animationPersona = AnimationPersona.Assassin;
                _locomotionBounceScale = 1.24f;
                _locomotionLeanScale = 1.12f;
                _locomotionSwaggerScale = 1.26f;
                _actionSnapScale = 1.18f;
                _superSnapScale = 1.08f;
                _showcaseInspectScale = 1.16f;
                _showcaseTempoScale = 1.28f;
            }
        }

        private string ResolveBrawlerName()
        {
            BrawlerDefinition definition = _definition != null
                ? _definition
                : (_owner != null ? _owner.Definition : null);

            if (definition == null)
                return gameObject.name;

            return !string.IsNullOrWhiteSpace(definition.name)
                ? definition.name
                : definition.BrawlerName;
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

            ApplyArmGripIk(
                upper,
                lower,
                hand,
                target.position,
                target.rotation,
                weight,
                rotateHand: true,
                DefaultHandRotationStrength);
        }

        private static void ApplyArmGripIk(
            Transform upper,
            Transform lower,
            Transform hand,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float weight)
        {
            ApplyArmGripIk(
                upper,
                lower,
                hand,
                targetPosition,
                targetRotation,
                weight,
                rotateHand: true,
                DefaultHandRotationStrength);
        }

        private static void ApplyArmGripIk(
            Transform upper,
            Transform lower,
            Transform hand,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float weight,
            bool rotateHand)
        {
            ApplyArmGripIk(
                upper,
                lower,
                hand,
                targetPosition,
                targetRotation,
                weight,
                rotateHand,
                DefaultHandRotationStrength);
        }

        private static void ApplyArmGripIk(
            Transform upper,
            Transform lower,
            Transform hand,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float weight,
            bool rotateHand,
            float handRotationStrength)
        {
            if (upper == null ||
                lower == null ||
                hand == null ||
                weight <= 0f)
            {
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                Vector3 upperDirection = targetPosition - upper.position;
                if (upperDirection.sqrMagnitude > 0.000001f)
                    RotateBoneToward(upper, lower, upperDirection.normalized, weight * 0.62f);

                Vector3 lowerDirection = targetPosition - lower.position;
                if (lowerDirection.sqrMagnitude > 0.000001f)
                    RotateBoneToward(lower, hand, lowerDirection.normalized, weight);
            }

            if (rotateHand)
            {
                hand.rotation = Quaternion.Slerp(
                    hand.rotation,
                    targetRotation,
                    Mathf.Clamp01(weight * Mathf.Max(0f, handRotationStrength)));
            }
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
