using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public enum AimPreviewKind
    {
        None = 0,
        MainAttack = 1,
        Super = 2
    }

    public struct AimPreviewData
    {
        public bool IsValid;
        public AimPreviewKind Kind;
        public AimPreviewMode Mode;

        public Vector3 Origin;
        public Vector3 Direction;
        public float Range;

        // Throwable / landing preview
        public Vector3 TargetPoint;
        public float ArcHeight;

        // Optional landing/placement radius
        public float Radius;
    }
}