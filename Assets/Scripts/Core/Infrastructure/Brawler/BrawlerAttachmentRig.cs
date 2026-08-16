using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerAttachmentRig : MonoBehaviour
    {
        private const float PalmForwardOffset = 0.092f;
        private const float PalmSideOffset = 0.014f;
        private const float PalmUpOffset = 0.004f;

        [SerializeField] private Transform _root;
        [SerializeField] private BrawlerAttachmentSocketBinding[] _socketBindings =
            new BrawlerAttachmentSocketBinding[0];

        [Header("Scene View")]
        [SerializeField] private bool _drawSocketGizmos = true;
        [SerializeField] private float _socketGizmoScale = 0.06f;

        private readonly Dictionary<BrawlerAttachmentSocket, Transform> _socketLookup =
            new Dictionary<BrawlerAttachmentSocket, Transform>(16);

        public Transform Root => _root != null ? _root : transform;

        public static BrawlerAttachmentRig Ensure(GameObject visualRoot)
        {
            if (visualRoot == null)
                return null;

            BrawlerAttachmentRig rig =
                visualRoot.GetComponentInChildren<BrawlerAttachmentRig>(true);
            if (rig == null)
                rig = visualRoot.AddComponent<BrawlerAttachmentRig>();

            return rig;
        }

        public void AutoBindFromModel(GameObject visualRoot)
        {
            if (visualRoot == null)
                return;

            _root = visualRoot.transform;
            _socketLookup.Clear();

            Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
            BrawlerHandPoseTargets authoredTargets =
                visualRoot.GetComponentInChildren<BrawlerHandPoseTargets>(true);
            bool hasHumanoidRig = animator != null && animator.isHuman;
            Transform socketRoot = GetOrCreateSocketRoot(visualRoot.transform);
            Transform authoredWeaponGrip = null;
            Transform authoredOffhandGrip = null;
            Transform authoredAim = null;
            Transform authoredMuzzle = null;
            if (authoredTargets != null)
            {
                authoredTargets.TryGetWeaponGripTarget(out authoredWeaponGrip);
                authoredTargets.TryGetOffhandGripTarget(out authoredOffhandGrip);
                authoredTargets.TryGetAimTarget(out authoredAim);
                authoredTargets.TryGetMuzzleTarget(out authoredMuzzle);
            }

            Transform rightHand =
                ResolveBone(animator, HumanBodyBones.RightHand) ??
                FindFirst(visualRoot.transform, "RightHand", "Right_Hand", "Right_Arm");
            Transform leftHand =
                ResolveBone(animator, HumanBodyBones.LeftHand) ??
                FindFirst(visualRoot.transform, "LeftHand", "Left_Hand", "Left_Arm");
            Quaternion weaponSocketRotation =
                ResolveForwardFacingSocketRotation(visualRoot.transform, animator);
            Transform primaryWeapon =
                authoredWeaponGrip ??
                (hasHumanoidRig && rightHand != null
                    ? GetOrCreateSocket(
                        rightHand,
                        "Weapon_Main",
                        ResolveHandSocketLocalPosition(rightHand, weaponSocketRotation, side: 1f),
                        Quaternion.Inverse(rightHand.rotation) * weaponSocketRotation,
                        forceTransform: true)
                    : FindFirst(visualRoot.transform, "Weapon_Main", "PrimaryWeapon") ??
                      rightHand);
            Transform secondaryWeapon =
                authoredOffhandGrip ??
                (hasHumanoidRig && leftHand != null
                    ? GetOrCreateSocket(
                        leftHand,
                        "Weapon_Offhand",
                        ResolveHandSocketLocalPosition(leftHand, weaponSocketRotation, side: -1f),
                        Quaternion.Inverse(leftHand.rotation) * weaponSocketRotation,
                        forceTransform: true)
                    : FindFirst(visualRoot.transform, "Weapon_Offhand", "SecondaryWeapon") ??
                      leftHand);

            SetSocket(BrawlerAttachmentSocket.Root, visualRoot.transform);
            SetSocket(
                BrawlerAttachmentSocket.Head,
                ResolveBone(animator, HumanBodyBones.Head) ??
                FindFirst(visualRoot.transform, "Head") ??
                GetOrCreateSocket(socketRoot, "Head", new Vector3(0f, 1.55f, 0f)));
            SetSocket(
                BrawlerAttachmentSocket.Chest,
                ResolveBone(animator, HumanBodyBones.Chest) ??
                ResolveBone(animator, HumanBodyBones.UpperChest) ??
                FindFirst(visualRoot.transform, "Chest", "Spine", "Torso") ??
                GetOrCreateSocket(socketRoot, "Chest", new Vector3(0f, 1.05f, 0f)));
            SetSocket(
                BrawlerAttachmentSocket.RightHand,
                rightHand ??
                GetOrCreateSocket(socketRoot, "RightHand", new Vector3(0.36f, 0.95f, 0.25f)));
            SetSocket(
                BrawlerAttachmentSocket.LeftHand,
                leftHand ??
                GetOrCreateSocket(socketRoot, "LeftHand", new Vector3(-0.36f, 0.95f, 0.25f)));
            SetSocket(
                BrawlerAttachmentSocket.Back,
                FindFirst(visualRoot.transform, "Back") ??
                GetOrCreateSocket(socketRoot, "Back", new Vector3(0f, 1.08f, 0.32f)));
            SetSocket(
                BrawlerAttachmentSocket.PrimaryWeapon,
                primaryWeapon ??
                ResolveSocket(BrawlerAttachmentSocket.RightHand, visualRoot.transform));
            SetSocket(
                BrawlerAttachmentSocket.SecondaryWeapon,
                secondaryWeapon ??
                ResolveSocket(BrawlerAttachmentSocket.LeftHand, visualRoot.transform));
            SetSocket(
                BrawlerAttachmentSocket.PrimaryMuzzle,
                authoredMuzzle ??
                FindFirst(visualRoot.transform, "Muzzle_Main", "PrimaryFirePoint") ??
                GetOrCreateSocket(socketRoot, "Muzzle_Main", new Vector3(0f, 1.05f, 0.65f)));
            SetSocket(
                BrawlerAttachmentSocket.SecondaryMuzzle,
                FindFirst(visualRoot.transform, "Muzzle_Offhand", "SecondaryFirePoint") ??
                GetOrCreateSocket(socketRoot, "Muzzle_Offhand", new Vector3(0.35f, 1.05f, 0.55f)));
            SetSocket(
                BrawlerAttachmentSocket.Throwable,
                FindFirst(visualRoot.transform, "Throwable") ??
                GetOrCreateSocket(socketRoot, "Throwable", new Vector3(0.18f, 1.02f, 0.46f)));
            SetSocket(
                BrawlerAttachmentSocket.AimTarget,
                authoredAim ??
                FindFirst(visualRoot.transform, "AimTarget") ??
                GetOrCreateSocket(socketRoot, "AimTarget", new Vector3(0f, 1.25f, 2.5f)));
            SetSocket(
                BrawlerAttachmentSocket.CastPoint,
                FindFirst(visualRoot.transform, "CastPoint") ??
                authoredMuzzle ??
                authoredAim ??
                ResolveSocket(BrawlerAttachmentSocket.AimTarget, visualRoot.transform));

            RebuildSerializedBindings();
        }

        public Transform ResolveSocket(
            BrawlerAttachmentSocket socket,
            Transform fallback = null)
        {
            if (_socketLookup.Count == 0)
                RebuildLookupFromSerialized();

            if (_socketLookup.TryGetValue(socket, out Transform transform) && transform != null)
                return transform;

            return fallback != null ? fallback : Root;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawSocketGizmos || _socketBindings == null)
                return;

            float size = Mathf.Max(0.01f, _socketGizmoScale);
            for (int i = 0; i < _socketBindings.Length; i++)
            {
                BrawlerAttachmentSocketBinding binding = _socketBindings[i];
                if (binding.Transform == null)
                    continue;

                DrawSocketGizmo(binding.Socket, binding.Transform, size);
            }
        }

        private void SetSocket(BrawlerAttachmentSocket socket, Transform socketTransform)
        {
            if (socketTransform == null)
                return;

            _socketLookup[socket] = socketTransform;
        }

        private void RebuildLookupFromSerialized()
        {
            _socketLookup.Clear();

            if (_socketBindings == null)
                return;

            for (int i = 0; i < _socketBindings.Length; i++)
            {
                BrawlerAttachmentSocketBinding binding = _socketBindings[i];
                if (binding.Transform == null)
                    continue;

                _socketLookup[binding.Socket] = binding.Transform;
            }
        }

        private void RebuildSerializedBindings()
        {
            List<BrawlerAttachmentSocketBinding> bindings =
                new List<BrawlerAttachmentSocketBinding>(_socketLookup.Count);

            foreach (KeyValuePair<BrawlerAttachmentSocket, Transform> kvp in _socketLookup)
            {
                if (kvp.Value == null)
                    continue;

                bindings.Add(new BrawlerAttachmentSocketBinding
                {
                    Socket = kvp.Key,
                    Transform = kvp.Value
                });
            }

            bindings.Sort((a, b) => a.Socket.CompareTo(b.Socket));
            _socketBindings = bindings.ToArray();
        }

        private static Transform ResolveBone(Animator animator, HumanBodyBones bone)
        {
            if (animator == null || !animator.isHuman)
                return null;

            return animator.GetBoneTransform(bone);
        }

        private static Transform GetOrCreateSocketRoot(Transform visualRoot)
        {
            Transform existing = FindFirst(visualRoot, "Sockets", "AttachmentSockets");
            if (existing != null)
                return existing;

            GameObject socketRoot = new GameObject("AttachmentSockets");
            socketRoot.transform.SetParent(visualRoot, false);
            return socketRoot.transform;
        }

        private static Transform GetOrCreateSocket(
            Transform parent,
            string name,
            Vector3 localPosition)
        {
            return GetOrCreateSocket(parent, name, localPosition, Quaternion.identity, forceTransform: false);
        }

        private static Transform GetOrCreateSocket(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            bool forceTransform)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (forceTransform)
                {
                    existing.localPosition = localPosition;
                    existing.localRotation = localRotation;
                    existing.localScale = Vector3.one;
                }

                return existing;
            }

            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = localRotation;
            socket.transform.localScale = Vector3.one;
            return socket.transform;
        }

        private static Quaternion ResolveForwardFacingSocketRotation(
            Transform visualRoot,
            Animator animator)
        {
            Vector3 forward = animator != null
                ? animator.transform.forward
                : visualRoot.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = visualRoot.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private static Vector3 ResolveHandSocketLocalPosition(
            Transform hand,
            Quaternion weaponSocketRotation,
            float side)
        {
            Vector3 forward = weaponSocketRotation * Vector3.forward;
            Vector3 right = weaponSocketRotation * Vector3.right;
            Vector3 up = Vector3.up;
            Vector3 worldPosition =
                hand.position +
                forward * PalmForwardOffset +
                right * side * PalmSideOffset +
                up * PalmUpOffset;

            return hand.InverseTransformPoint(worldPosition);
        }

        private static Transform FindFirst(Transform root, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = FindChildRecursive(root, names[i]);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void DrawSocketGizmo(
            BrawlerAttachmentSocket socket,
            Transform socketTransform,
            float size)
        {
            Color color = ResolveSocketColor(socket);
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(socketTransform.position, socketTransform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * size);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * size * 3f);
            Gizmos.DrawLine(Vector3.zero, Vector3.up * size * 1.75f);
            Gizmos.matrix = previousMatrix;

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(
                socketTransform.position + Vector3.up * size * 1.8f,
                socket.ToString());
#endif
        }

        private static Color ResolveSocketColor(BrawlerAttachmentSocket socket)
        {
            switch (socket)
            {
                case BrawlerAttachmentSocket.PrimaryWeapon:
                    return new Color(1f, 0.85f, 0.10f);
                case BrawlerAttachmentSocket.SecondaryWeapon:
                    return new Color(0.45f, 1f, 0.20f);
                case BrawlerAttachmentSocket.PrimaryMuzzle:
                case BrawlerAttachmentSocket.SecondaryMuzzle:
                    return new Color(1f, 0.35f, 0.05f);
                case BrawlerAttachmentSocket.AimTarget:
                    return new Color(0.30f, 0.55f, 1f);
                case BrawlerAttachmentSocket.RightHand:
                    return new Color(0.10f, 0.90f, 1f);
                case BrawlerAttachmentSocket.LeftHand:
                    return new Color(1f, 0.15f, 0.90f);
                case BrawlerAttachmentSocket.CastPoint:
                case BrawlerAttachmentSocket.Throwable:
                    return new Color(0.75f, 0.45f, 1f);
                default:
                    return new Color(1f, 1f, 1f, 0.85f);
            }
        }
    }

    [Serializable]
    public struct BrawlerAttachmentSocketBinding
    {
        public BrawlerAttachmentSocket Socket;
        public Transform Transform;
    }
}
