using System;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(
        fileName = "BrawlerAttachmentProfile",
        menuName = "MOBA/Presentation/Brawler Attachment Profile")]
    public sealed class BrawlerAttachmentProfile : ScriptableObject
    {
        [Header("Humanoid Grip Pose")]
        [Tooltip("Presentation-only hand pose used by imported humanoid models. Auto infers from the generated attachment type.")]
        public BrawlerAttachmentGripPose GripPose = BrawlerAttachmentGripPose.Auto;

        [Range(0f, 1f)]
        public float GripPoseWeight = 1f;

        public BrawlerAttachmentBinding[] Attachments = new BrawlerAttachmentBinding[0];
    }

    [Serializable]
    public sealed class BrawlerAttachmentBinding
    {
        public string Id;
        public BrawlerAttachmentSocket Socket = BrawlerAttachmentSocket.PrimaryWeapon;
        public UnityEngine.Object Prefab;
        public BrawlerGeneratedAttachmentType GeneratedAttachment = BrawlerGeneratedAttachmentType.None;
        public Vector3 LocalPositionOffset;
        public Vector3 LocalEulerOffset;
        public Vector3 LocalScale = Vector3.one;
        public bool UseExplicitGripPoint;
        public Vector3 ExplicitGripLocalPosition;
        public Vector3 ExplicitGripLocalEulerOffset;
        public bool FollowSocketRotation = true;
        public bool ReplaceExistingWithSameId = true;
        public bool UseAsPrimaryFirePoint;
        public bool UseAsSecondaryFirePoint;
        public bool UseAsCastPoint;
        public Vector3 PresentationAnchorLocalOffset;
    }
}
