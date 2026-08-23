using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerAttachmentInstaller : MonoBehaviour
    {
        private const string RuntimeAttachmentRootName = "RuntimeAttachments";

        [SerializeField] private BrawlerDefinition _definition;
        [SerializeField] private BrawlerAttachmentRig _rig;
        [SerializeField] private Transform _runtimeAttachmentRoot;

        private readonly List<GameObject> _spawnedAttachments = new List<GameObject>(4);

        public static BrawlerAttachmentInstaller Ensure(GameObject visualRoot)
        {
            if (visualRoot == null)
                return null;

            BrawlerAttachmentInstaller installer =
                visualRoot.GetComponentInChildren<BrawlerAttachmentInstaller>(true);
            if (installer == null)
                installer = visualRoot.AddComponent<BrawlerAttachmentInstaller>();

            return installer;
        }

        public void Bind(BrawlerDefinition definition, BrawlerAttachmentRig rig)
        {
            _definition = definition;
            _rig = rig;
            RebuildAttachments();
        }

        public void RebuildAttachments()
        {
            ClearSpawnedAttachments();

            if (_definition == null ||
                _definition.AttachmentProfile == null ||
                _definition.AttachmentProfile.Attachments == null ||
                _rig == null)
            {
                return;
            }

            EnsureRuntimeAttachmentRoot();

            Transform primaryFireOverride = null;
            Transform secondaryFireOverride = null;
            Transform castOverride = null;
            BrawlerAttachmentBinding[] attachments = _definition.AttachmentProfile.Attachments;
            for (int i = 0; i < attachments.Length; i++)
            {
                BrawlerAttachmentBinding binding = attachments[i];
                if (binding == null)
                    continue;

                if (binding.ReplaceExistingWithSameId)
                    RemoveExisting(binding.Id);

                Transform socket = _rig.ResolveSocket(binding.Socket, transform);
                bool useFollower =
                    binding.UseStableCharacterRotation ||
                    RequiresAnimatedSocketFollower(binding.Socket);
                Transform parent =
                    binding.FollowSocketRotation && !useFollower
                        ? socket
                        : _runtimeAttachmentRoot;
                GameObject attachment = CreateAttachment(binding, parent);
                if (attachment == null)
                    continue;

                attachment.name = BuildAttachmentName(binding, attachment);

                if (binding.FollowSocketRotation && !useFollower)
                {
                    attachment.transform.localPosition = binding.LocalPositionOffset;
                    attachment.transform.localRotation = Quaternion.Euler(binding.LocalEulerOffset);
                }
                else
                {
                    attachment.transform.position = socket.TransformPoint(binding.LocalPositionOffset);
                    attachment.transform.rotation = Quaternion.Euler(binding.LocalEulerOffset);
                }

                attachment.transform.localScale = ResolveScale(binding.LocalScale);
                if (useFollower)
                    InstallAttachmentFollower(binding, socket, attachment.transform);
                else
                    AlignAttachmentGripPoint(binding, socket, attachment.transform);

                ConfigureLayer(attachment, gameObject.layer);
                StripGameplayComponents(attachment);
                InstallRuntimeGripTargets(binding, attachment.transform);

                Transform presentationAnchor = CreatePresentationAnchor(binding, attachment.transform);
                if (presentationAnchor != null)
                {
                    if (binding.UseAsPrimaryFirePoint)
                        primaryFireOverride = presentationAnchor;
                    if (binding.UseAsSecondaryFirePoint)
                        secondaryFireOverride = presentationAnchor;
                    if (binding.UseAsCastPoint)
                        castOverride = presentationAnchor;
                }

                _spawnedAttachments.Add(attachment);
            }

            ApplyPresentationAnchorOverrides(primaryFireOverride, secondaryFireOverride, castOverride);
            RefreshModelGripTargets();
        }

        private void InstallAttachmentFollower(
            BrawlerAttachmentBinding binding,
            Transform socket,
            Transform attachmentRoot)
        {
            if (binding == null || socket == null || attachmentRoot == null)
                return;

            bool alignGripPoint = TryResolveAttachmentGripPoint(
                binding,
                attachmentRoot,
                out Vector3 gripLocalPosition,
                out Quaternion gripLocalRotation);

            BrawlerAttachmentFollower follower =
                attachmentRoot.GetComponent<BrawlerAttachmentFollower>();
            if (follower == null)
                follower = attachmentRoot.gameObject.AddComponent<BrawlerAttachmentFollower>();

            Transform characterRoot = _rig != null ? _rig.Root : transform;
            follower.Configure(
                socket,
                characterRoot,
                binding.LocalPositionOffset,
                binding.LocalEulerOffset,
                binding.UseStableCharacterRotation,
                alignGripPoint,
                gripLocalPosition,
                gripLocalRotation);
        }

        private static bool RequiresAnimatedSocketFollower(BrawlerAttachmentSocket socket)
        {
            switch (socket)
            {
                case BrawlerAttachmentSocket.PrimaryWeapon:
                case BrawlerAttachmentSocket.SecondaryWeapon:
                case BrawlerAttachmentSocket.RightHand:
                case BrawlerAttachmentSocket.LeftHand:
                case BrawlerAttachmentSocket.Throwable:
                    return true;
                default:
                    return false;
            }
        }

        private static void InstallRuntimeGripTargets(
            BrawlerAttachmentBinding binding,
            Transform attachmentRoot)
        {
            if (binding == null ||
                attachmentRoot == null ||
                !binding.UseSecondaryGripPoint)
            {
                return;
            }

            Transform configuredSecondaryGrip =
                FindConfiguredChild(attachmentRoot, binding.SecondaryGripPointName);
            Transform secondaryGrip =
                configuredSecondaryGrip ??
                GetOrCreateChild(attachmentRoot, "SecondaryGripTarget");
            if (configuredSecondaryGrip == null)
            {
                secondaryGrip.localPosition = binding.SecondaryGripLocalPosition;
                secondaryGrip.localRotation = Quaternion.Euler(binding.SecondaryGripLocalEulerOffset);
                secondaryGrip.localScale = Vector3.one;
            }

            BrawlerRuntimeAttachmentGrip runtimeGrip =
                attachmentRoot.GetComponent<BrawlerRuntimeAttachmentGrip>();
            if (runtimeGrip == null)
                runtimeGrip = attachmentRoot.gameObject.AddComponent<BrawlerRuntimeAttachmentGrip>();

            runtimeGrip.ConfigureSecondaryGrip(
                secondaryGrip,
                binding.SecondaryGripSocket,
                binding.SecondaryGripWeight);
        }

        private static Transform GetOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
                return existing;

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private void RefreshModelGripTargets()
        {
            BrawlerAuthoredModelAnimator modelAnimator =
                GetComponentInChildren<BrawlerAuthoredModelAnimator>(true);
            if (modelAnimator == null)
                modelAnimator = GetComponentInParent<BrawlerAuthoredModelAnimator>();

            if (modelAnimator != null)
                modelAnimator.RefreshRuntimeGripTargets();
        }

        private GameObject CreateAttachment(
            BrawlerAttachmentBinding binding,
            Transform parent)
        {
            GameObject authoredPrefab = ResolveAuthoredPrefab(binding);
            if (authoredPrefab != null)
            {
                if (TryCreatePrefabAttachment(binding, authoredPrefab, parent, out GameObject prefabAttachment))
                    return prefabAttachment;

                Debug.LogWarning(
                    $"[BrawlerAttachmentInstaller] Authored attachment '{authoredPrefab.name}' for '{binding.Id}' could not be instantiated. Generated fallback skipped so the authored asset mismatch is visible.");
                return null;
            }

            if (binding.GeneratedAttachment == BrawlerGeneratedAttachmentType.None)
            {
                Debug.LogWarning(
                    $"[BrawlerAttachmentInstaller] Attachment '{binding.Id}' has no authored weapon prefab assigned. No fallback will be generated.");
                return null;
            }

            Debug.LogWarning(
                $"[BrawlerAttachmentInstaller] Attachment '{binding.Id}' requested generated weapon '{binding.GeneratedAttachment}', but generated fallbacks are disabled. Assign an authored weapon prefab instead.");
            return null;
        }

        private static bool TryCreatePrefabAttachment(
            BrawlerAttachmentBinding binding,
            GameObject prefab,
            Transform parent,
            out GameObject attachment)
        {
            attachment = null;

            if (prefab == null)
                return false;

            UnityEngine.Object clone = null;
            try
            {
                clone = Instantiate((UnityEngine.Object)prefab);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[BrawlerAttachmentInstaller] Failed to instantiate authored attachment '{prefab.name}' for '{binding.Id}': {exception.GetType().Name} {exception.Message}");
                return false;
            }

            attachment = ResolveInstantiatedRoot(clone);
            if (attachment != null)
            {
                attachment.transform.SetParent(parent, false);
                return true;
            }

            if (clone != null)
                Destroy(clone);

            return false;
        }

        private static GameObject ResolveAuthoredPrefab(BrawlerAttachmentBinding binding)
        {
            if (binding == null)
                return null;

            if (binding.Prefab != null)
                return binding.Prefab;

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(binding.PrefabAssetPath))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(binding.PrefabAssetPath);
#endif

            return null;
        }

        private static GameObject ResolveInstantiatedRoot(UnityEngine.Object clone)
        {
            if (clone is GameObject gameObject)
                return gameObject;

            if (clone is Component component)
                return component.gameObject;

            return null;
        }

        private void EnsureRuntimeAttachmentRoot()
        {
            if (_runtimeAttachmentRoot != null)
                return;

            Transform existing = transform.Find(RuntimeAttachmentRootName);
            if (existing != null)
            {
                _runtimeAttachmentRoot = existing;
                return;
            }

            GameObject root = new GameObject(RuntimeAttachmentRootName);
            root.transform.SetParent(transform, false);
            _runtimeAttachmentRoot = root.transform;
        }

        private void ClearSpawnedAttachments()
        {
            for (int i = _spawnedAttachments.Count - 1; i >= 0; i--)
            {
                GameObject attachment = _spawnedAttachments[i];
                if (attachment == null)
                    continue;

                Destroy(attachment);
            }

            _spawnedAttachments.Clear();
        }

        private void RemoveExisting(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            string expectedName = NormalizeAttachmentId(id);
            for (int i = _spawnedAttachments.Count - 1; i >= 0; i--)
            {
                GameObject attachment = _spawnedAttachments[i];
                if (attachment == null)
                {
                    _spawnedAttachments.RemoveAt(i);
                    continue;
                }

                if (!attachment.name.StartsWith(expectedName, System.StringComparison.Ordinal))
                    continue;

                Destroy(attachment);
                _spawnedAttachments.RemoveAt(i);
            }
        }

        private static string BuildAttachmentName(
            BrawlerAttachmentBinding binding,
            GameObject attachment)
        {
            string id = NormalizeAttachmentId(binding.Id);
            if (!string.IsNullOrEmpty(id))
                return $"{id}_{attachment.name}";

            return "Attachment_" + attachment.name;
        }

        private static string NormalizeAttachmentId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            return id.Trim().Replace(' ', '_');
        }

        private static Vector3 ResolveScale(Vector3 requested)
        {
            return requested == Vector3.zero ? Vector3.one : requested;
        }

        private static Transform CreatePresentationAnchor(
            BrawlerAttachmentBinding binding,
            Transform attachmentRoot)
        {
            if (binding == null ||
                attachmentRoot == null ||
                (!binding.UseAsPrimaryFirePoint &&
                 !binding.UseAsSecondaryFirePoint &&
                 !binding.UseAsCastPoint))
            {
                return null;
            }

            GameObject anchor = new GameObject("PresentationAnchor");
            Transform anchorTransform = anchor.transform;
            anchorTransform.SetParent(attachmentRoot, false);
            anchorTransform.localPosition = binding.PresentationAnchorLocalOffset;
            anchorTransform.localRotation = Quaternion.identity;
            anchorTransform.localScale = Vector3.one;
            return anchorTransform;
        }

        private static void AlignAttachmentGripPoint(
            BrawlerAttachmentBinding binding,
            Transform socket,
            Transform attachmentRoot)
        {
            if (binding == null || socket == null || attachmentRoot == null)
                return;

            if (!TryResolveAttachmentGripPoint(
                    binding,
                    attachmentRoot,
                    out Vector3 gripLocalPosition,
                    out Quaternion gripLocalRotation))
            {
                return;
            }

            Quaternion targetRotation = binding.FollowSocketRotation
                ? socket.rotation * Quaternion.Euler(binding.LocalEulerOffset)
                : Quaternion.Euler(binding.LocalEulerOffset);
            Vector3 targetPosition = socket.TransformPoint(binding.LocalPositionOffset);

            attachmentRoot.rotation = targetRotation * Quaternion.Inverse(gripLocalRotation);
            Vector3 gripWorldPosition = attachmentRoot.TransformPoint(gripLocalPosition);
            attachmentRoot.position += targetPosition - gripWorldPosition;
        }

        private static bool TryResolveAttachmentGripPoint(
            BrawlerAttachmentBinding binding,
            Transform attachmentRoot,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            if (binding.UseExplicitGripPoint)
            {
                localPosition = binding.ExplicitGripLocalPosition;
                localRotation = Quaternion.Euler(binding.ExplicitGripLocalEulerOffset);
                return true;
            }

            Transform gripPoint =
                FindConfiguredChild(attachmentRoot, binding.GripPointName) ??
                FindChildRecursive(attachmentRoot, BrawlerGeneratedAttachmentFactory.GripPointName) ??
                FindChildRecursive(attachmentRoot, BrawlerGeneratedAttachmentFactory.HoldPointName) ??
                FindChildRecursive(attachmentRoot, "WeaponGrip");
            if (gripPoint != null)
            {
                localPosition = attachmentRoot.InverseTransformPoint(gripPoint.position);
                localRotation = Quaternion.Inverse(attachmentRoot.rotation) * gripPoint.rotation;
                return true;
            }

            return TryResolveVirtualGripPoint(
                binding.GeneratedAttachment,
                out localPosition,
                out localRotation);
        }

        private static bool TryResolveVirtualGripPoint(
            BrawlerGeneratedAttachmentType attachmentType,
            out Vector3 localPosition,
            out Quaternion localRotation)
        {
            localRotation = Quaternion.identity;

            switch (attachmentType)
            {
                case BrawlerGeneratedAttachmentType.Pistol:
                    localPosition = new Vector3(0f, -0.19f, -0.08f);
                    return true;
                case BrawlerGeneratedAttachmentType.Bottle:
                    localPosition = new Vector3(0f, 0.25f, 0f);
                    return true;
                case BrawlerGeneratedAttachmentType.Staff:
                    localPosition = new Vector3(0f, -0.32f, 0f);
                    return true;
                case BrawlerGeneratedAttachmentType.ShockGun:
                    localPosition = new Vector3(0f, -0.22f, -0.18f);
                    return true;
                case BrawlerGeneratedAttachmentType.Bow:
                case BrawlerGeneratedAttachmentType.NinjaStars:
                case BrawlerGeneratedAttachmentType.Umbrella:
                    localPosition = Vector3.zero;
                    return true;
                default:
                    localPosition = Vector3.zero;
                    return false;
            }
        }

        private void ApplyPresentationAnchorOverrides(
            Transform primaryFireOverride,
            Transform secondaryFireOverride,
            Transform castOverride)
        {
            if (primaryFireOverride == null &&
                secondaryFireOverride == null &&
                castOverride == null)
            {
                return;
            }

            BrawlerPresentationAnchors anchors =
                GetComponentInChildren<BrawlerPresentationAnchors>(true);
            if (anchors == null)
                return;

            anchors.Configure(
                primaryFireOverride != null ? primaryFireOverride : anchors.PrimaryFirePoint,
                secondaryFireOverride != null ? secondaryFireOverride : anchors.SecondaryFirePoint,
                castOverride != null ? castOverride : anchors.CastPoint);
        }

        private static void ConfigureLayer(GameObject root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.layer = layer;
        }

        private static void StripGameplayComponents(GameObject attachment)
        {
            Collider[] colliders = attachment.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                Destroy(colliders[i]);
            }

            Rigidbody[] bodies = attachment.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
                Destroy(bodies[i]);
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

        private static Transform FindConfiguredChild(
            Transform root,
            string childName)
        {
            return string.IsNullOrWhiteSpace(childName)
                ? null
                : FindChildRecursive(root, childName.Trim());
        }
    }
}
