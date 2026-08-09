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
        [SerializeField] private bool _preferGeneratedAttachments;

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

        public void SetPreferGeneratedAttachments(bool preferGeneratedAttachments)
        {
            if (_preferGeneratedAttachments == preferGeneratedAttachments)
                return;

            _preferGeneratedAttachments = preferGeneratedAttachments;
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
                Transform parent = binding.FollowSocketRotation ? socket : _runtimeAttachmentRoot;
                GameObject attachment = CreateAttachment(binding, parent);
                if (attachment == null)
                    continue;

                attachment.name = BuildAttachmentName(binding, attachment);

                if (binding.FollowSocketRotation)
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
                AlignAttachmentGripPoint(binding, socket, attachment.transform);

                ConfigureLayer(attachment, gameObject.layer);
                StripGameplayComponents(attachment);

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
        }

        private GameObject CreateAttachment(
            BrawlerAttachmentBinding binding,
            Transform parent)
        {
            if (_preferGeneratedAttachments &&
                TryCreateGeneratedAttachment(binding, parent, out GameObject generatedFirst))
            {
                return generatedFirst;
            }

            if (TryCreatePrefabAttachment(binding, parent, out GameObject prefabAttachment))
                return prefabAttachment;

            return TryCreateGeneratedAttachment(binding, parent, out GameObject generated)
                ? generated
                : null;
        }

        private static bool TryCreateGeneratedAttachment(
            BrawlerAttachmentBinding binding,
            Transform parent,
            out GameObject attachment)
        {
            attachment = null;

            if (binding == null ||
                !BrawlerGeneratedAttachmentFactory.TryCreate(
                    binding.GeneratedAttachment,
                    binding.Id,
                    out GameObject generated))
            {
                return false;
            }

            generated.transform.SetParent(parent, false);
            attachment = generated;
            return true;
        }

        private static bool TryCreatePrefabAttachment(
            BrawlerAttachmentBinding binding,
            Transform parent,
            out GameObject attachment)
        {
            attachment = null;

            if (binding.Prefab == null)
                return false;

            UnityEngine.Object clone = null;
            try
            {
                clone = Instantiate((UnityEngine.Object)binding.Prefab, parent);
            }
            catch (InvalidCastException)
            {
                return false;
            }

            attachment = clone as GameObject;
            if (attachment != null)
                return true;

            if (clone != null)
                Destroy(clone);

            return false;
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
            Transform gripPoint =
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
    }
}
