using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class ShowdownCrateTargetUtility
    {
        public static bool IsTargetableCrate(ISpatialEntity entity)
        {
            return entity is PowerCubeCrateController crate &&
                   !crate.IsDestroyed &&
                   crate.CurrentHealth > 0f;
        }

        public static float CalculateAimPriorityScore(
            ISpatialEntity entity,
            Vector3 origin,
            float maxRange)
        {
            if (!IsTargetableCrate(entity))
                return float.MinValue;

            Vector3 offset = entity.Position - origin;
            offset.y = 0f;
            float reach = Mathf.Max(0f, maxRange) +
                          Mathf.Max(0f, entity.CollisionRadius);
            float distanceSq = offset.sqrMagnitude;
            if (distanceSq > reach * reach)
                return float.MinValue;

            return -distanceSq;
        }

        public static float CalculateAITargetBonus(
            float distance,
            float attackRange,
            float crateHealthRatio,
            float selfHealthRatio,
            int powerCubeCount,
            bool isCurrentTarget)
        {
            float usefulRange = Mathf.Max(5f, Mathf.Max(1f, attackRange) * 1.35f);
            float proximity = 1f - Mathf.Clamp01(Mathf.Max(0f, distance) / usefulRange);
            float progress = 1f - Mathf.Clamp01(crateHealthRatio);
            float cubeNeed = 1f - Mathf.Clamp01(Mathf.Max(0, powerCubeCount) / 6f);
            float lowHealthPenalty =
                (1f - Mathf.Clamp01(selfHealthRatio)) * 30f;

            float score = 75f +
                          proximity * 45f +
                          progress * 30f +
                          cubeNeed * 18f -
                          lowHealthPenalty;

            if (isCurrentTarget)
                score += 14f;

            return Mathf.Max(0f, score);
        }

        public static bool ShouldSuppressForEnemyThreat(
            float nearestEnemyDistance,
            float attackRange)
        {
            float dangerRadius = Mathf.Clamp(
                Mathf.Max(1f, attackRange) * 0.85f,
                4.5f,
                7f);

            return nearestEnemyDistance <= dangerRadius;
        }
    }
}
