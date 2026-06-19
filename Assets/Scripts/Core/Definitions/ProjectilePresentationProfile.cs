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

        [Header("Readability")]
        [Tooltip("Uniformly scales very small projectile visuals up to this largest local axis size.")]
        [Min(0.01f)]
        public float MinimumVisualDiameter = 0.16f;

        [Header("Runtime Trail")]
        public bool UseRuntimeTrail = true;
        [Min(0.01f)] public float TrailTime = 0.10f;
        [Min(0f)] public float TrailStartWidth = 0.14f;
        [Min(0f)] public float TrailEndWidth = 0.025f;
        public Color TrailColor = new Color(1f, 0.78f, 0.26f, 0.78f);
        public Color SuperTrailColor = new Color(1f, 0.35f, 0.92f, 0.82f);
    }
}
