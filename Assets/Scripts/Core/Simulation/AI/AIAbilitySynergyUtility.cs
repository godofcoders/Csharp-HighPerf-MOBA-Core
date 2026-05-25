using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public struct AIComboWindowResult
    {
        public bool IsActive;
        public bool ShouldCommit;
        public float Score;
        public string Reason;
    }

    public static class AIAbilitySynergyUtility
    {
        public static AIComboWindowResult EvaluateComboWindow(
            bool hasSetup,
            uint currentTick,
            uint setupTick,
            uint windowTicks,
            float targetHealthRatio,
            bool targetControlled,
            int enemyPressureCount,
            int allyRiskCount)
        {
            if (!hasSetup)
                return BuildComboResult(false, false, 0f, "no_setup");

            uint window = windowTicks == 0u ? 1u : windowTicks;
            uint age = currentTick >= setupTick ? currentTick - setupTick : 0u;
            if (age > window)
                return BuildComboResult(false, false, 0f, "expired");

            float windowScore = 1f - Mathf.Clamp01(age / (float)window);
            float score = 30f + windowScore * 24f;
            string reason = "setup";

            if (targetControlled)
            {
                score += 26f;
                reason += "|controlled";
            }

            if (targetHealthRatio <= 0.45f)
            {
                score += 22f;
                reason += "|finish";
            }

            if (enemyPressureCount > 1)
            {
                score += Mathf.Min(26f, (enemyPressureCount - 1) * 13f);
                reason += "|multi";
            }

            if (allyRiskCount > 0)
            {
                score -= Mathf.Min(35f, allyRiskCount * 18f);
                reason += "|ally_risk";
            }

            return BuildComboResult(true, score >= 58f, score, reason);
        }

        public static Vector3 ResolveLayeredAreaDenialPoint(
            Vector3 selfPosition,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float impactRadius,
            float maxRange,
            int enemyClusterCount,
            int layerIndex)
        {
            Vector3 fromSelf = targetPosition - selfPosition;
            fromSelf.y = 0f;

            if (fromSelf.sqrMagnitude <= 0.001f)
                fromSelf = Vector3.forward;

            Vector3 forward = fromSelf.normalized;
            targetVelocity.y = 0f;

            float radius = Mathf.Max(0.5f, impactRadius);
            int clampedCluster = Mathf.Max(1, enemyClusterCount);
            float forwardLayer = Mathf.Min(radius * 1.15f, radius * (0.45f + clampedCluster * 0.18f));

            Vector3 desired = targetPosition;
            if (targetVelocity.sqrMagnitude > 0.04f)
                desired += targetVelocity.normalized * forwardLayer;
            else
                desired += forward * (radius * 0.35f);

            Vector3 side = new Vector3(forward.z, 0f, -forward.x);
            float sideSign = (layerIndex & 1) == 0 ? 1f : -1f;
            float sideLayer = Mathf.Min(radius * 0.55f, 1.1f) *
                              (clampedCluster > 1 ? 1f : 0.45f);
            desired += side * sideSign * sideLayer;

            Vector3 offset = desired - selfPosition;
            offset.y = 0f;

            float range = Mathf.Max(0.5f, maxRange);
            if (offset.sqrMagnitude > range * range)
                desired = selfPosition + offset.normalized * range;

            return desired;
        }

        public static float ScoreDeployableProtection(
            float deployableHealthRatio,
            float enemyDistanceToDeployable,
            float protectionRadius,
            int nearbyEnemyCount)
        {
            float healthUrgency = 1f - Mathf.Clamp01(deployableHealthRatio);
            float radius = Mathf.Max(0.5f, protectionRadius);
            float threatProximity = 1f - Mathf.Clamp01(enemyDistanceToDeployable / radius);
            float pressure = Mathf.Clamp01(nearbyEnemyCount / 3f);

            return Mathf.Clamp01((healthUrgency * 0.55f) + (threatProximity * 0.30f) + (pressure * 0.15f));
        }

        private static AIComboWindowResult BuildComboResult(
            bool isActive,
            bool shouldCommit,
            float score,
            string reason)
        {
            return new AIComboWindowResult
            {
                IsActive = isActive,
                ShouldCommit = shouldCommit,
                Score = score,
                Reason = reason
            };
        }
    }
}
