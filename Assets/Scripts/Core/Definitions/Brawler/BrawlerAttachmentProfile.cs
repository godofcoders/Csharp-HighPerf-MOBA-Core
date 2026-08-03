using System;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(
        fileName = "BrawlerAttachmentProfile",
        menuName = "MOBA/Presentation/Brawler Attachment Profile")]
    public sealed class BrawlerAttachmentProfile : ScriptableObject
    {
        public BrawlerAttachmentBinding[] Attachments = new BrawlerAttachmentBinding[0];
    }

    [Serializable]
    public sealed class BrawlerAttachmentBinding
    {
        public string Id;
        public BrawlerAttachmentSocket Socket = BrawlerAttachmentSocket.PrimaryWeapon;
        public GameObject Prefab;
        public BrawlerGeneratedAttachmentType GeneratedAttachment = BrawlerGeneratedAttachmentType.None;
        public Vector3 LocalPositionOffset;
        public Vector3 LocalEulerOffset;
        public Vector3 LocalScale = Vector3.one;
        public bool FollowSocketRotation = true;
        public bool ReplaceExistingWithSameId = true;
    }
}
