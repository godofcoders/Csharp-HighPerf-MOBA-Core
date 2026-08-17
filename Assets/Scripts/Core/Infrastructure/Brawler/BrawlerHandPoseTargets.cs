using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Designer-authored hand IK targets for imported humanoid brawler models.
    /// Add this to the visual prefab/root and move the target transforms in Prefab Mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BrawlerHandPoseTargets : MonoBehaviour
    {
        private const string TargetRootName = "HandPoseTargets";
        private const string RightTargetName = "RightHand_IK_Target";
        private const string LeftTargetName = "LeftHand_IK_Target";
        private const string WeaponGripTargetName = "WeaponGrip_Target";
        private const string OffhandGripTargetName = "OffhandGrip_Target";
        private const string AimTargetName = "Aim_Target";
        private const string MuzzleTargetName = "Muzzle_Target";
        private const float MarkerFallbackHandWeight = 0.72f;

        [Header("Runtime")]
        [SerializeField] private bool _enabled = true;

        [Header("Hand IK")]
        [SerializeField] private Transform _rightHandTarget;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField, Range(0f, 1f)] private float _rightHandWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _leftHandWeight = 1f;
        [SerializeField] private bool _useRightTargetRotation = true;
        [SerializeField] private bool _useLeftTargetRotation = true;

        [Header("Weapon Authoring Markers")]
        [SerializeField] private Transform _weaponGripTarget;
        [SerializeField] private Transform _offhandGripTarget;
        [SerializeField] private Transform _aimTarget;
        [SerializeField] private Transform _muzzleTarget;

        [Header("Blend")]
        [SerializeField, Range(0f, 1f)] private float _idleWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _readyWeight = 0.78f;
        [SerializeField, Range(0f, 1f)] private float _actionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _showcaseWeight = 1f;

        [Header("Finger Grip")]
        [SerializeField] private bool _overrideFingerGrip;
        [SerializeField, Range(0f, 1f)] private float _rightFingerCurl = 1f;
        [SerializeField, Range(0f, 1f)] private float _rightThumbCurl = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _leftFingerCurl = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _leftThumbCurl = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _fingerGripWeight = 1f;
        [SerializeField] private Vector3 _rightHandLocalEulerOffset;
        [SerializeField] private Vector3 _leftHandLocalEulerOffset;

        [Header("Scene View")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private bool _drawWhenNotSelected;
        [SerializeField] private float _gizmoScale = 0.09f;

        public float ResolvePoseWeight(
            float ready01,
            float action01,
            bool showcase)
        {
            if (!_enabled || !isActiveAndEnabled)
                return 0f;

            float readyBlend = Mathf.Lerp(
                Mathf.Clamp01(_idleWeight),
                Mathf.Clamp01(_readyWeight),
                Mathf.Clamp01(ready01));
            float actionBlend = Mathf.Lerp(
                readyBlend,
                Mathf.Clamp01(_actionWeight),
                Mathf.Clamp01(action01));

            return showcase
                ? Mathf.Max(actionBlend, Mathf.Clamp01(_showcaseWeight))
                : actionBlend;
        }

        public bool TryGetRightHandTarget(
            out Transform target,
            out float weight,
            out bool useRotation)
        {
            target = ResolveTarget(_rightHandTarget, RightTargetName);
            weight = Mathf.Clamp01(_rightHandWeight);
            useRotation = _useRightTargetRotation;
            return _enabled && target != null && weight > 0f;
        }

        public bool TryGetLeftHandTarget(
            out Transform target,
            out float weight,
            out bool useRotation)
        {
            target = ResolveTarget(_leftHandTarget, LeftTargetName);
            weight = Mathf.Clamp01(_leftHandWeight);
            useRotation = _useLeftTargetRotation;
            return _enabled && target != null && weight > 0f;
        }

        public bool TryGetRightHandPoseTarget(
            out Transform target,
            out float weight,
            out bool useRotation)
        {
            if (TryGetRightHandTarget(out target, out weight, out useRotation))
                return true;

            target = ResolveTarget(_weaponGripTarget, WeaponGripTargetName);
            weight = Mathf.Clamp01(_rightHandWeight * MarkerFallbackHandWeight);
            useRotation = _useRightTargetRotation;
            return _enabled && target != null && weight > 0f;
        }

        public bool TryGetLeftHandPoseTarget(
            out Transform target,
            out float weight,
            out bool useRotation)
        {
            if (TryGetLeftHandTarget(out target, out weight, out useRotation))
                return true;

            target = ResolveTarget(_offhandGripTarget, OffhandGripTargetName);
            weight = Mathf.Clamp01(_leftHandWeight * MarkerFallbackHandWeight);
            useRotation = _useLeftTargetRotation;
            return _enabled && target != null && weight > 0f;
        }

        public bool TryGetWeaponGripTarget(out Transform target)
        {
            target = ResolveTarget(_weaponGripTarget, WeaponGripTargetName);
            return _enabled && target != null;
        }

        public bool TryGetOffhandGripTarget(out Transform target)
        {
            target = ResolveTarget(_offhandGripTarget, OffhandGripTargetName);
            return _enabled && target != null;
        }

        public bool TryGetAimTarget(out Transform target)
        {
            target = ResolveTarget(_aimTarget, AimTargetName);
            return _enabled && target != null;
        }

        public bool TryGetMuzzleTarget(out Transform target)
        {
            target = ResolveTarget(_muzzleTarget, MuzzleTargetName);
            return _enabled && target != null;
        }

        public bool TryGetRightFingerGrip(
            out float fingerCurl,
            out float thumbCurl,
            out Vector3 localEulerOffset,
            out float weight)
        {
            fingerCurl = Mathf.Clamp01(_rightFingerCurl);
            thumbCurl = Mathf.Clamp01(_rightThumbCurl);
            localEulerOffset = _rightHandLocalEulerOffset;
            weight = Mathf.Clamp01(_fingerGripWeight);
            return _enabled && _overrideFingerGrip && weight > 0f;
        }

        public bool TryGetLeftFingerGrip(
            out float fingerCurl,
            out float thumbCurl,
            out Vector3 localEulerOffset,
            out float weight)
        {
            fingerCurl = Mathf.Clamp01(_leftFingerCurl);
            thumbCurl = Mathf.Clamp01(_leftThumbCurl);
            localEulerOffset = _leftHandLocalEulerOffset;
            weight = Mathf.Clamp01(_fingerGripWeight);
            return _enabled && _overrideFingerGrip && weight > 0f;
        }

#if UNITY_EDITOR
        [ContextMenu("Create Full Grip Authoring Rig")]
        public void CreateFullGripAuthoringRig()
        {
            UnityEditor.Undo.RecordObject(this, "Create Grip Authoring Rig");

            Transform targetRoot = GetOrCreateChild(transform, TargetRootName, out _);
            Animator animator = GetComponentInChildren<Animator>(true);
            Transform rightHand = ResolveBone(animator, HumanBodyBones.RightHand);
            Transform leftHand = ResolveBone(animator, HumanBodyBones.LeftHand);
            Quaternion forwardRotation = ResolveDefaultForwardRotation(animator, transform);
            bool rightCreated = false;
            bool leftCreated = false;
            bool weaponCreated = false;
            bool offhandCreated = false;
            bool aimCreated = false;
            bool muzzleCreated = false;

            if (_rightHandTarget == null)
                _rightHandTarget = GetOrCreateChild(targetRoot, RightTargetName, out rightCreated);

            if (_leftHandTarget == null)
                _leftHandTarget = GetOrCreateChild(targetRoot, LeftTargetName, out leftCreated);

            if (_weaponGripTarget == null)
                _weaponGripTarget = GetOrCreateChild(targetRoot, WeaponGripTargetName, out weaponCreated);

            if (_offhandGripTarget == null)
                _offhandGripTarget = GetOrCreateChild(targetRoot, OffhandGripTargetName, out offhandCreated);

            if (_aimTarget == null)
                _aimTarget = GetOrCreateChild(targetRoot, AimTargetName, out aimCreated);

            if (_muzzleTarget == null)
                _muzzleTarget = GetOrCreateChild(targetRoot, MuzzleTargetName, out muzzleCreated);

            if (rightCreated)
                PlaceTarget(_rightHandTarget, rightHand, new Vector3(0.24f, 1.02f, 0.42f), forwardRotation);

            if (leftCreated)
                PlaceTarget(_leftHandTarget, leftHand, new Vector3(-0.24f, 1.02f, 0.42f), forwardRotation);

            if (weaponCreated)
                PlaceTarget(_weaponGripTarget, rightHand, new Vector3(0.16f, 1.02f, 0.44f), forwardRotation);

            if (offhandCreated)
                PlaceTarget(_offhandGripTarget, leftHand, new Vector3(-0.16f, 1.02f, 0.38f), forwardRotation);

            if (aimCreated)
                PlaceTarget(_aimTarget, null, new Vector3(0f, 1.18f, 1.35f), forwardRotation);

            if (muzzleCreated)
                PlaceTarget(_muzzleTarget, null, new Vector3(0.10f, 1.05f, 0.74f), forwardRotation);

            UnityEditor.EditorUtility.SetDirty(targetRoot.gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Create Missing Hand Targets")]
        public void CreateMissingHandTargets()
        {
            CreateFullGripAuthoringRig();
        }

        [ContextMenu("Snap Hand Targets To Humanoid Hands")]
        public void SnapHandTargetsToHumanoidHands()
        {
            UnityEditor.Undo.RecordObject(this, "Snap Hand Targets");

            Animator animator = GetComponentInChildren<Animator>(true);
            Transform rightHand = ResolveBone(animator, HumanBodyBones.RightHand);
            Transform leftHand = ResolveBone(animator, HumanBodyBones.LeftHand);
            Quaternion forwardRotation = ResolveDefaultForwardRotation(animator, transform);

            if (_rightHandTarget != null)
                PlaceTarget(_rightHandTarget, rightHand, new Vector3(0.24f, 1.02f, 0.42f), forwardRotation);

            if (_leftHandTarget != null)
                PlaceTarget(_leftHandTarget, leftHand, new Vector3(-0.24f, 1.02f, 0.42f), forwardRotation);

            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Select Target Root")]
        public void SelectTargetRoot()
        {
            Transform targetRoot = ResolveTargetRoot();
            if (targetRoot != null)
                UnityEditor.Selection.activeTransform = targetRoot;
        }

        private static Transform ResolveBone(Animator animator, HumanBodyBones bone)
        {
            return animator != null && animator.isHuman
                ? animator.GetBoneTransform(bone)
                : null;
        }

        private static void PlaceTarget(
            Transform target,
            Transform source,
            Vector3 fallbackLocalPosition,
            Quaternion fallbackRotation)
        {
            if (target == null)
                return;

            if (source != null)
            {
                target.position = source.position;
                target.rotation = source.rotation;
            }
            else
            {
                target.localPosition = fallbackLocalPosition;
                target.rotation = fallbackRotation;
            }

            target.localScale = Vector3.one;
        }

        private static Quaternion ResolveDefaultForwardRotation(
            Animator animator,
            Transform fallback)
        {
            Vector3 forward = animator != null
                ? animator.transform.forward
                : (fallback != null ? fallback.forward : Vector3.forward);

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f && fallback != null)
                forward = fallback.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName,
            out bool created)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            GameObject child = new GameObject(childName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            created = true;
            return child.transform;
        }
#endif

        private Transform ResolveTargetRoot()
        {
            Transform direct = transform.Find(TargetRootName);
            return direct != null ? direct : FindDeep(transform, TargetRootName);
        }

        private Transform ResolveTarget(Transform assigned, string fallbackName)
        {
            if (assigned != null)
                return assigned;

            Transform root = ResolveTargetRoot();
            if (root == null)
                return FindDeep(transform, fallbackName);

            Transform direct = root.Find(fallbackName);
            return direct != null ? direct : FindDeep(root, fallbackName);
        }

        private static Transform FindDeep(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void OnDrawGizmos()
        {
            if (_drawWhenNotSelected)
                DrawAuthoringGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawAuthoringGizmos(true);
        }

        private void DrawAuthoringGizmos(bool selected)
        {
            if (!_drawGizmos)
                return;

            float size = Mathf.Max(0.01f, _gizmoScale);
            Transform right = ResolveTarget(_rightHandTarget, RightTargetName);
            Transform left = ResolveTarget(_leftHandTarget, LeftTargetName);
            Transform weapon = ResolveTarget(_weaponGripTarget, WeaponGripTargetName);
            Transform offhand = ResolveTarget(_offhandGripTarget, OffhandGripTargetName);
            Transform aim = ResolveTarget(_aimTarget, AimTargetName);
            Transform muzzle = ResolveTarget(_muzzleTarget, MuzzleTargetName);

            DrawLink(right, weapon, new Color(0.1f, 0.9f, 1f, selected ? 0.95f : 0.55f));
            DrawLink(left, offhand, new Color(1f, 0.15f, 0.9f, selected ? 0.95f : 0.55f));
            DrawLink(weapon, offhand, new Color(1f, 0.95f, 0.15f, selected ? 0.85f : 0.45f));
            DrawLink(muzzle, aim, new Color(1f, 0.42f, 0.08f, selected ? 0.95f : 0.55f));

            DrawTargetGizmo(right, new Color(0.1f, 0.9f, 1f), "Right IK", size);
            DrawTargetGizmo(left, new Color(1f, 0.15f, 0.9f), "Left IK", size);
            DrawTargetGizmo(weapon, new Color(1f, 0.88f, 0.10f), "Weapon Grip", size * 1.15f);
            DrawTargetGizmo(offhand, new Color(0.45f, 1f, 0.20f), "Offhand Grip", size);
            DrawTargetGizmo(aim, new Color(0.30f, 0.55f, 1f), "Aim", size * 1.25f);
            DrawTargetGizmo(muzzle, new Color(1f, 0.35f, 0.05f), "Muzzle", size);
        }

        private static void DrawTargetGizmo(
            Transform target,
            Color color,
            string label,
            float size)
        {
            if (target == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(target.position, target.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * size);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * size * 2.75f);
            Gizmos.DrawLine(Vector3.zero, Vector3.up * size * 1.75f);
            Gizmos.DrawLine(Vector3.zero, Vector3.right * size * 1.45f);
            Gizmos.matrix = previousMatrix;

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(target.position + Vector3.up * size * 1.8f, label);
#endif
        }

        private static void DrawLink(
            Transform from,
            Transform to,
            Color color)
        {
            if (from == null || to == null)
                return;

            Gizmos.color = color;
            Gizmos.DrawLine(from.position, to.position);
        }
    }
}
