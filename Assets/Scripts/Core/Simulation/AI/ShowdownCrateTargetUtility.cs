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
            int rewardCubeCount,
            float centerProximity,
            AIPersonalityType personality,
            bool isCurrentTarget)
        {
            float usefulRange = Mathf.Max(5f, Mathf.Max(1f, attackRange) * 1.35f);
            float proximity = 1f - Mathf.Clamp01(Mathf.Max(0f, distance) / usefulRange);
            float progress = 1f - Mathf.Clamp01(crateHealthRatio);
            float cubeNeed = 1f - Mathf.Clamp01(Mathf.Max(0, powerCubeCount) / 6f);
            float lowHealthPenalty =
                (1f - Mathf.Clamp01(selfHealthRatio)) * 30f;
            float rewardBonus = Mathf.Max(0, rewardCubeCount - 1) * 30f;
            float centerBonus = Mathf.Clamp01(centerProximity) *
                                ResolveCenterPreference(personality);

            float score = 75f +
                          proximity * 45f +
                          progress * 30f +
                          cubeNeed * 18f -
                          lowHealthPenalty +
                          rewardBonus +
                          centerBonus;

            if (isCurrentTarget)
                score += 14f;

            return Mathf.Max(0f, score);
        }

        public static bool ShouldSuppressForEnemyThreat(
            float nearestEnemyDistance,
            float attackRange,
            AIPersonalityType personality,
            float selfHealthRatio)
        {
            float dangerRadius = Mathf.Clamp(
                Mathf.Max(1f, attackRange) * 0.85f,
                4.5f,
                7f);

            float personalityScale;
            switch (personality)
            {
                case AIPersonalityType.Aggressive:
                    personalityScale = 0.65f;
                    break;
                case AIPersonalityType.Cautious:
                    personalityScale = 1.15f;
                    break;
                case AIPersonalityType.TeamPlayer:
                    personalityScale = 1f;
                    break;
                default:
                    personalityScale = 0.90f;
                    break;
            }

            float injuryScale = Mathf.Lerp(
                1.25f,
                1f,
                Mathf.Clamp01(selfHealthRatio));
            dangerRadius *= personalityScale * injuryScale;

            return nearestEnemyDistance <= dangerRadius;
        }

        public static float CalculateCenterObjectiveBonus(
            AIPersonalityType personality,
            float selfHealthRatio,
            int powerCubeCount)
        {
            float basePreference;
            float needScale;
            float injuryPenalty;
            switch (personality)
            {
                case AIPersonalityType.Aggressive:
                    basePreference = 44f;
                    needScale = 14f;
                    injuryPenalty = 24f;
                    break;
                case AIPersonalityType.Cautious:
                    basePreference = -12f;
                    needScale = 3f;
                    injuryPenalty = 42f;
                    break;
                case AIPersonalityType.TeamPlayer:
                    basePreference = 12f;
                    needScale = 8f;
                    injuryPenalty = 34f;
                    break;
                default:
                    basePreference = 20f;
                    needScale = 10f;
                    injuryPenalty = 30f;
                    break;
            }

            float cubeNeed = 1f - Mathf.Clamp01(Mathf.Max(0, powerCubeCount) / 8f);
            float injury = 1f - Mathf.Clamp01(selfHealthRatio);
            return basePreference + cubeNeed * needScale - injury * injuryPenalty;
        }

        public static bool IsCenterCubeObjective(in AIObjectiveCandidate objective)
        {
            return objective.IsRuntime &&
                   objective.Name == ShowdownPowerCubeLayoutUtility.CenterObjectiveName;
        }

        private static float ResolveCenterPreference(AIPersonalityType personality)
        {
            switch (personality)
            {
                case AIPersonalityType.Aggressive:
                    return 32f;
                case AIPersonalityType.Cautious:
                    return 4f;
                case AIPersonalityType.TeamPlayer:
                    return 14f;
                default:
                    return 18f;
            }
        }
    }
}
