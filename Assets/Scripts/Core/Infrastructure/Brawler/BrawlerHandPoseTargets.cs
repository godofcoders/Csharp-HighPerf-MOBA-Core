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

        [SerializeField] private bool _enabled = true;
        [SerializeField] private Transform _rightHandTarget;
        [SerializeField] private Transform _leftHandTarget;
        [SerializeField, Range(0f, 1f)] private float _rightHandWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _leftHandWeight = 1f;
        [SerializeField] private bool _useRightTargetRotation = true;
        [SerializeField] private bool _useLeftTargetRotation = true;

        [Header("Blend")]
        [SerializeField, Range(0f, 1f)] private float _idleWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _readyWeight = 0.78f;
        [SerializeField, Range(0f, 1f)] private float _actionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _showcaseWeight = 1f;

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

#if UNITY_EDITOR
        [ContextMenu("Create Missing Hand Targets")]
        private void CreateMissingHandTargets()
        {
            Transform targetRoot = GetOrCreateChild(transform, TargetRootName);
            Animator animator = GetComponentInChildren<Animator>(true);
            Transform rightHand = ResolveBone(animator, HumanBodyBones.RightHand);
            Transform leftHand = ResolveBone(animator, HumanBodyBones.LeftHand);

            if (_rightHandTarget == null)
            {
                _rightHandTarget = GetOrCreateChild(targetRoot, RightTargetName);
                PlaceTarget(_rightHandTarget, rightHand, new Vector3(0.24f, 1.02f, 0.42f));
            }

            if (_leftHandTarget == null)
            {
                _leftHandTarget = GetOrCreateChild(targetRoot, LeftTargetName);
                PlaceTarget(_leftHandTarget, leftHand, new Vector3(-0.24f, 1.02f, 0.42f));
            }

            UnityEditor.EditorUtility.SetDirty(this);
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
            Vector3 fallbackLocalPosition)
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
                target.localRotation = Quaternion.identity;
            }

            target.localScale = Vector3.one;
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
                return existing;

            GameObject child = new GameObject(childName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }
#endif

        private Transform ResolveTarget(Transform assigned, string fallbackName)
        {
            if (assigned != null)
                return assigned;

            Transform root = transform.Find(TargetRootName);
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

        private void OnDrawGizmosSelected()
        {
            DrawTargetGizmo(ResolveTarget(_rightHandTarget, RightTargetName), Color.cyan);
            DrawTargetGizmo(ResolveTarget(_leftHandTarget, LeftTargetName), Color.magenta);
        }

        private static void DrawTargetGizmo(Transform target, Color color)
        {
            if (target == null)
                return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(target.position, 0.035f);
            Gizmos.DrawLine(target.position, target.position + target.forward * 0.16f);
            Gizmos.DrawLine(target.position, target.position + target.up * 0.10f);
        }
    }
}
