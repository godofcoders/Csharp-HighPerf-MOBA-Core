using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "ProjectilePresentationProfile", menuName = "MOBA/Presentation/Projectile Presentation Profile")]
    public class ProjectilePresentationProfile : ScriptableObject
    {
        [Header("Visual")]
        public GameObject VisualPrefab;
        public Vector3 LocalPosition = Vector3.zero;
        public Vector3 LocalRotationEuler = Vector3.zero;
        public Vector3 LocalScale = Vector3.one;

        [Header("Orientation")]
        public bool FaceMovementDirection = true;

        [Header("Optional Motion Styling")]
        public bool UseSpin = false;
        public Vector3 SpinEulerPerSecond = Vector3.zero;
    }
}