using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public sealed class BrawlerRuntimeAttachmentGrip : MonoBehaviour
    {
        [SerializeField] private Transform _secondaryGripTarget;
        [SerializeField] private BrawlerAttachmentSocket _secondaryGripSocket;
        [SerializeField, Range(0f, 1f)] private float _secondaryGripWeight;

        public void ConfigureSecondaryGrip(
            Transform gripTarget,
            BrawlerAttachmentSocket gripSocket,
            float gripWeight)
        {
            _secondaryGripTarget = gripTarget;
            _secondaryGripSocket = gripSocket;
            _secondaryGripWeight = Mathf.Clamp01(gripWeight);
        }

        public bool TryGetSecondaryGrip(
            out BrawlerAttachmentSocket socket,
            out Transform target,
            out float weight)
        {
            socket = _secondaryGripSocket;
            target = _secondaryGripTarget;
            weight = _secondaryGripWeight;
            return target != null && weight > 0f;
        }
    }
}
