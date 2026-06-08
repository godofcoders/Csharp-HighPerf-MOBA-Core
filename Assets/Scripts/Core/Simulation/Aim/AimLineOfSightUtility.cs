using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public readonly struct AimLineTraceResult
    {
        public readonly bool IsBlocked;
        public readonly float ClearDistance;
        public readonly Vector3 EndPoint;
        public readonly Vector3 BlockPoint;

        public AimLineTraceResult(
            bool isBlocked,
            float clearDistance,
            Vector3 endPoint,
            Vector3 blockPoint)
        {
            IsBlocked = isBlocked;
            ClearDistance = clearDistance;
            EndPoint = endPoint;
            BlockPoint = blockPoint;
        }
    }

    public static class AimLineOfSightUtility
    {
        private const float MinSampleStep = 0.08f;
        private const float MaxSampleStep = 0.35f;
        private const float LineOfSightTolerance = 0.12f;

        public static AimLineTraceResult Trace(
            AStarSolver pathfinder,
            Vector3 origin,
            Vector3 direction,
            float maxRange,
            float projectileRadius)
        {
            float range = Mathf.Max(0f, maxRange);
            Vector3 flatDirection = Flatten(direction);

            if (flatDirection.sqrMagnitude <= 0.001f || range <= 0f)
                return new AimLineTraceResult(false, 0f, origin, origin);

            flatDirection.Normalize();

            if (pathfinder == null)
                return BuildOpenResult(origin, flatDirection, range);

            float sampleStep = Mathf.Clamp(
                pathfinder.CellSize * 0.20f,
                MinSampleStep,
                MaxSampleStep);

            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(range / sampleStep));
            Vector3 previousClearPoint = origin;
            float previousClearDistance = 0f;

            for (int i = 1; i <= sampleCount; i++)
            {
                float distance = Mathf.Min(range, i * sampleStep);
                Vector3 point = origin + flatDirection * distance;

                if (IsShotBlockedAt(pathfinder, point, flatDirection, projectileRadius))
                {
                    return new AimLineTraceResult(
                        true,
                        previousClearDistance,
                        previousClearPoint,
                        point);
                }

                previousClearDistance = distance;
                previousClearPoint = point;
            }

            return BuildOpenResult(origin, flatDirection, range);
        }

        public static bool HasLineOfSight(
            AStarSolver pathfinder,
            Vector3 origin,
            Vector3 target,
            float projectileRadius)
        {
            Vector3 toTarget = target - origin;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f || pathfinder == null)
                return true;

            AimLineTraceResult trace = Trace(
                pathfinder,
                origin,
                toTarget,
                distance,
                projectileRadius);

            return !trace.IsBlocked ||
                   trace.ClearDistance >= distance - LineOfSightTolerance;
        }

        public static bool IsSegmentBlocked(
            AStarSolver pathfinder,
            Vector3 start,
            Vector3 end,
            float projectileRadius)
        {
            if (pathfinder == null)
                return false;

            Vector3 delta = end - start;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
                return IsShotBlockedAt(pathfinder, end, Vector3.forward, projectileRadius);

            AimLineTraceResult trace = Trace(
                pathfinder,
                start,
                delta,
                distance,
                projectileRadius);

            return trace.IsBlocked;
        }

        private static AimLineTraceResult BuildOpenResult(
            Vector3 origin,
            Vector3 direction,
            float range)
        {
            Vector3 endPoint = origin + direction * range;
            return new AimLineTraceResult(false, range, endPoint, endPoint);
        }

        private static bool IsShotBlockedAt(
            AStarSolver pathfinder,
            Vector3 point,
            Vector3 direction,
            float projectileRadius)
        {
            if (IsPointBlocked(pathfinder, point))
                return true;

            float radius = Mathf.Max(0f, projectileRadius);
            if (radius <= 0.01f)
                return false;

            Vector3 right = new Vector3(direction.z, 0f, -direction.x);
            if (right.sqrMagnitude <= 0.001f)
                return false;

            right.Normalize();
            float sideOffset = radius * 0.85f;

            return IsPointBlocked(pathfinder, point + right * sideOffset) ||
                   IsPointBlocked(pathfinder, point - right * sideOffset);
        }

        private static bool IsPointBlocked(AStarSolver pathfinder, Vector3 point)
        {
            Vector2Int coords = GetRawGridCoords(pathfinder, point);
            return !pathfinder.IsInBounds(coords) || !pathfinder.IsWalkable(coords);
        }

        private static Vector2Int GetRawGridCoords(AStarSolver pathfinder, Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - pathfinder.Origin.x) / pathfinder.CellSize);
            int y = Mathf.FloorToInt((worldPos.z - pathfinder.Origin.z) / pathfinder.CellSize);
            return new Vector2Int(x, y);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
