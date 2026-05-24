using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIPredictiveShotResult
    {
        public readonly bool ShouldFire;
        public readonly Vector3 AimPoint;
        public readonly float Quality;
        public readonly float TravelTime;
        public readonly string Reason;

        public AIPredictiveShotResult(
            bool shouldFire,
            Vector3 aimPoint,
            float quality,
            float travelTime,
            string reason)
        {
            ShouldFire = shouldFire;
            AimPoint = aimPoint;
            Quality = quality;
            TravelTime = travelTime;
            Reason = reason;
        }
    }

    public static class AIPredictiveCombatUtility
    {
        private const float MaxLeadSeconds = 0.65f;
        private const float LowAmmoQualityThreshold = 0.55f;
        private const float NormalQualityThreshold = 0.32f;
        private const float StrongLaneQualityBonus = 0.28f;
        private const float ControlledTargetQualityBonus = 0.30f;
        private const float FriendlyLanePenalty = 0.12f;

        public static AIPredictiveShotResult EvaluateProjectileShot(
            Vector3 shooterPosition,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float range,
            float projectileSpeed,
            int availableAmmo,
            bool targetControlled,
            int enemyCountInLane,
            int allyCountInLane)
        {
            range = Mathf.Max(1f, range);
            projectileSpeed = Mathf.Max(1f, projectileSpeed);

            shooterPosition.y = 0f;
            targetPosition.y = 0f;
            targetVelocity.y = 0f;

            Vector3 toTarget = targetPosition - shooterPosition;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                return new AIPredictiveShotResult(
                    true,
                    targetPosition,
                    1f,
                    0f,
                    "point_blank");
            }

            float travelTime = distance / projectileSpeed;
            float leadSeconds = Mathf.Min(MaxLeadSeconds, travelTime);
            Vector3 aimPoint = targetPosition + targetVelocity * leadSeconds;

            Vector3 aimOffset = aimPoint - shooterPosition;
            aimOffset.y = 0f;
            if (aimOffset.magnitude > range)
            {
                aimPoint = shooterPosition + aimOffset.normalized * range;
                aimOffset = aimPoint - shooterPosition;
                aimOffset.y = 0f;
            }

            Vector3 fireDirection = aimOffset.sqrMagnitude > 0.001f
                ? aimOffset.normalized
                : toTarget.normalized;

            float lateralSpeed = Vector3.Cross(fireDirection, targetVelocity).magnitude;
            float distanceRatio = Mathf.Clamp01(distance / range);
            float dodgeRisk = Mathf.Clamp01((lateralSpeed * travelTime) / 2.25f);
            float quality = 1f - (distanceRatio * 0.25f) - (dodgeRisk * 0.65f);

            if (targetControlled)
                quality += ControlledTargetQualityBonus;

            if (enemyCountInLane >= 2)
                quality += StrongLaneQualityBonus;

            if (allyCountInLane > 0 && enemyCountInLane <= 1)
                quality -= FriendlyLanePenalty * allyCountInLane;

            quality = Mathf.Clamp01(quality);

            float threshold = availableAmmo <= 1
                ? LowAmmoQualityThreshold
                : NormalQualityThreshold;

            bool shouldFire =
                targetControlled ||
                enemyCountInLane >= 2 ||
                quality >= threshold;

            string reason;
            if (targetControlled)
                reason = "controlled_target";
            else if (enemyCountInLane >= 2)
                reason = "strong_fire_lane";
            else if (shouldFire)
                reason = "predictive_window";
            else
                reason = "hold_bad_angle";

            return new AIPredictiveShotResult(
                shouldFire,
                aimPoint,
                quality,
                travelTime,
                reason);
        }

        public static bool TryGetProjectileKinematics(
            AbilityDefinition ability,
            float fallbackRange,
            out float range,
            out float speed)
        {
            switch (ability)
            {
                case BasicProjectileAttackDefinition basic:
                    range = basic.Range;
                    speed = basic.ProjectileSpeed;
                    return true;

                case BasicSuperDefinition basicSuper:
                    range = basicSuper.Range;
                    speed = basicSuper.ProjectileSpeed;
                    return true;

                case ProjectileAbilityDefinition projectile:
                    range = projectile.Range;
                    speed = projectile.Speed;
                    return true;

                case BurstSequenceProjectileAbilityDefinition burst:
                    range = burst.Range;
                    speed = burst.Speed;
                    return true;

                case ChainProjectileAbilityDefinition chain:
                    range = chain.Range;
                    speed = chain.Speed;
                    return true;

                case HybridProjectileAbilityDefinition hybrid:
                    range = hybrid.Range;
                    speed = hybrid.Speed;
                    return true;

                case ThrownHybridAoEAbilityDefinition thrown:
                    range = thrown.ThrowRange;
                    speed = thrown.ThrowSpeed;
                    return true;

                case ThrownVolleyAoEAbilityDefinition volley:
                    range = volley.ThrowRange;
                    speed = volley.ThrowSpeed;
                    return true;

                default:
                    range = Mathf.Max(1f, fallbackRange);
                    speed = 0f;
                    return false;
            }
        }
    }
}
