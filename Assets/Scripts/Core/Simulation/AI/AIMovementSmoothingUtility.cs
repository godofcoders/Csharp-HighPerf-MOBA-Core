using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIMovementSmoothingState
    {
        public readonly bool HasDirection;
        public readonly Vector3 Direction;

        public AIMovementSmoothingState(bool hasDirection, Vector3 direction)
        {
            HasDirection = hasDirection && direction.sqrMagnitude > 0.0001f;
            Direction = HasDirection ? direction.normalized : Vector3.zero;
        }

        public static AIMovementSmoothingState None =>
            new AIMovementSmoothingState(false, Vector3.zero);
    }

    public readonly struct AIMovementSmoothingResult
    {
        public readonly Vector3 Direction;
        public readonly AIMovementSmoothingState State;
        public readonly string Reason;

        public AIMovementSmoothingResult(
            Vector3 direction,
            AIMovementSmoothingState state,
            string reason)
        {
            Direction = direction;
            State = state;
            Reason = string.IsNullOrEmpty(reason) ? "smooth_none" : reason;
        }
    }

    public static class AIMovementSmoothingUtility
    {
        public static AIMovementSmoothingResult SmoothDirection(
            Vector3 desiredDirection,
            AIMovementSmoothingState previousState,
            float baseBlend,
            bool highPriority,
            bool avoidanceLocked)
        {
            desiredDirection.y = 0f;
            float rawMagnitude = desiredDirection.magnitude;
            float magnitude = Mathf.Clamp01(rawMagnitude);
            if (rawMagnitude <= 0.001f)
            {
                return new AIMovementSmoothingResult(
                    Vector3.zero,
                    AIMovementSmoothingState.None,
                    "smooth_idle");
            }

            Vector3 desired = desiredDirection / rawMagnitude;
            if (!previousState.HasDirection)
            {
                return new AIMovementSmoothingResult(
                    desired * magnitude,
                    new AIMovementSmoothingState(true, desired),
                    "smooth_seed");
            }

            float dot = Vector3.Dot(previousState.Direction, desired);
            float blend = Mathf.Clamp01(baseBlend);

            if (highPriority)
                blend = Mathf.Max(blend, 0.78f);
            else if (avoidanceLocked)
                blend = Mathf.Max(blend, 0.62f);
            else if (dot < -0.20f)
                blend = Mathf.Min(blend, 0.32f);

            Vector3 smoothed = Vector3.Lerp(previousState.Direction, desired, blend);
            smoothed.y = 0f;

            if (smoothed.sqrMagnitude <= 0.0001f)
            {
                smoothed = desired;
            }
            else
            {
                smoothed.Normalize();
            }

            string reason = dot < -0.20f
                ? "smooth_flip"
                : "smooth_blend";

            return new AIMovementSmoothingResult(
                smoothed * magnitude,
                new AIMovementSmoothingState(true, smoothed),
                reason);
        }
    }
}
