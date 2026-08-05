using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerAttachmentRig : MonoBehaviour
    {
        [SerializeField] private Transform _root;
        [SerializeField] private BrawlerAttachmentSocketBinding[] _socketBindings =
            new BrawlerAttachmentSocketBinding[0];

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
            bool hasHumanoidRig = animator != null && animator.isHuman;
            Transform socketRoot = GetOrCreateSocketRoot(visualRoot.transform);
            Transform rightHand =
                ResolveBone(animator, HumanBodyBones.RightHand) ??
                FindFirst(visualRoot.transform, "RightHand", "Right_Hand", "Right_Arm");
            Transform leftHand =
                ResolveBone(animator, HumanBodyBones.LeftHand) ??
                FindFirst(visualRoot.transform, "LeftHand", "Left_Hand", "Left_Arm");
            Transform primaryWeapon =
                hasHumanoidRig && rightHand != null
                    ? GetOrCreateSocket(rightHand, "Weapon_Main", Vector3.zero)
                    : FindFirst(visualRoot.transform, "Weapon_Main", "PrimaryWeapon") ??
                      rightHand;
            Transform secondaryWeapon =
                hasHumanoidRig && leftHand != null
                    ? GetOrCreateSocket(leftHand, "Weapon_Offhand", Vector3.zero)
                    : FindFirst(visualRoot.transform, "Weapon_Offhand", "SecondaryWeapon") ??
                      leftHand;

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
                FindFirst(visualRoot.transform, "AimTarget") ??
                GetOrCreateSocket(socketRoot, "AimTarget", new Vector3(0f, 1.25f, 2.5f)));
            SetSocket(
                BrawlerAttachmentSocket.CastPoint,
                FindFirst(visualRoot.transform, "CastPoint") ??
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
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;

            GameObject socket = new GameObject(name);
            socket.transform.SetParent(parent, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = Quaternion.identity;
            socket.transform.localScale = Vector3.one;
            return socket.transform;
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
    }

    [Serializable]
    public struct BrawlerAttachmentSocketBinding
    {
        public BrawlerAttachmentSocket Socket;
        public Transform Transform;
    }
}
