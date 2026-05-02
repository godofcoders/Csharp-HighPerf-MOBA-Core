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

        // Spread fan: half-angle in degrees on each side of Direction. When
        // > 0 the directional preview renders boundary lines at ±this angle
        // so the player sees the actual hit cone (Shelly-style shotgun,
        // Volley spread, etc.). 0 = single straight line.
        public float SpreadHalfAngleDegrees;
    }
}