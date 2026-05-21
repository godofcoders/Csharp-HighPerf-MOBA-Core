using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class AIAimAccuracyUtility
    {
        public static AIAbilityCastPlan ApplyAimError(
            AIAbilityCastPlan plan,
            Vector3 origin,
            float aimErrorDegrees)
        {
            if (aimErrorDegrees <= 0.01f)
                return plan;

            float yawError = Random.Range(-aimErrorDegrees, aimErrorDegrees);

            if (plan.HasTargetPoint)
            {
                Vector3 offset = plan.TargetPoint - origin;
                offset.y = 0f;

                if (offset.sqrMagnitude > 0.001f)
                {
                    Vector3 erroredOffset = Quaternion.Euler(0f, yawError, 0f) * offset;
                    plan.TargetPoint = origin + erroredOffset;
                    plan.Direction = erroredOffset.normalized;
                }

                return plan;
            }

            if (plan.Direction.sqrMagnitude > 0.001f)
            {
                plan.Direction = (Quaternion.Euler(0f, yawError, 0f) * plan.Direction).normalized;
            }

            return plan;
        }
    }
}
