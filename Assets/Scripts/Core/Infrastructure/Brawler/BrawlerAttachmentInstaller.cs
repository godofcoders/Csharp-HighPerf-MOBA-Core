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

                ConfigureLayer(attachment, gameObject.layer);
                StripGameplayComponents(attachment);
                _spawnedAttachments.Add(attachment);
            }
        }

        private static GameObject CreateAttachment(
            BrawlerAttachmentBinding binding,
            Transform parent)
        {
            if (binding.Prefab != null)
                return Instantiate(binding.Prefab, parent);

            if (!BrawlerGeneratedAttachmentFactory.TryCreate(
                    binding.GeneratedAttachment,
                    binding.Id,
                    out GameObject generated))
            {
                return null;
            }

            generated.transform.SetParent(parent, false);
            return generated;
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
    }
}
