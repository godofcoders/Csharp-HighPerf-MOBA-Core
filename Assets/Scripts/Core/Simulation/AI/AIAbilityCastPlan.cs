using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public struct AIAbilityCastPlan
    {
        public ISpatialEntity Target;
        public Vector3 Direction;
        public Vector3 TargetPoint;
        public bool HasTargetPoint;
        public bool ForceUse;
        public string Reason;

        public static AIAbilityCastPlan Directional(ISpatialEntity target, Vector3 direction, string reason)
        {
            return new AIAbilityCastPlan
            {
                Target = target,
                Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward,
                TargetPoint = Vector3.zero,
                HasTargetPoint = false,
                ForceUse = false,
                Reason = reason
            };
        }

        public static AIAbilityCastPlan PointTarget(ISpatialEntity target, Vector3 origin, Vector3 targetPoint, string reason)
        {
            Vector3 direction = targetPoint - origin;
            direction.y = 0f;

            return new AIAbilityCastPlan
            {
                Target = target,
                Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward,
                TargetPoint = targetPoint,
                HasTargetPoint = true,
                ForceUse = false,
                Reason = reason
            };
        }
    }
}
