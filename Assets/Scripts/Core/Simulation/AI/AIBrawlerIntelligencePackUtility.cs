using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIBrawlerPackDecision
    {
        public readonly bool ShouldUse;
        public readonly bool ForceUse;
        public readonly float Score;
        public readonly string Reason;

        public AIBrawlerPackDecision(
            bool shouldUse,
            bool forceUse,
            float score,
            string reason)
        {
            ShouldUse = shouldUse;
            ForceUse = forceUse;
            Score = score;
            Reason = reason;
        }
    }

    public static class AIBrawlerIntelligencePackUtility
    {
        public static AIBrawlerPackDecision EvaluateDashGadget(
            float selfHealthRatio,
            float enemyDistance,
            float dangerDistance,
            float targetHealthRatio,
            bool targetControlled,
            bool hasEscapeIntent)
        {
            float dangerRange = Mathf.Max(0.5f, dangerDistance);
            float closeDanger = 1f - Mathf.Clamp01(enemyDistance / dangerRange);
            float healthUrgency = 1f - Mathf.Clamp01(selfHealthRatio);
            float escapeScore = healthUrgency * 70f + closeDanger * 42f;

            if (hasEscapeIntent)
                escapeScore += 18f;

            if (escapeScore >= 62f)
            {
                return new AIBrawlerPackDecision(
                    shouldUse: true,
                    forceUse: true,
                    score: escapeScore,
                    reason: "dash_escape");
            }

            float finisherScore = (1f - Mathf.Clamp01(targetHealthRatio)) * 50f;
            if (targetControlled)
                finisherScore += 20f;

            if (enemyDistance > dangerRange * 0.85f)
                finisherScore += 12f;

            bool shouldChase = finisherScore >= 60f;
            return new AIBrawlerPackDecision(
                shouldUse: shouldChase,
                forceUse: shouldChase && targetControlled,
                score: finisherScore,
                reason: shouldChase ? "dash_finisher" : "dash_hold");
        }

        public static AIBrawlerPackDecision EvaluateAmmoRefillGadget(
            int availableAmmo,
            int maxAmmo,
            float targetDistance,
            float attackRange,
            float targetHealthRatio,
            bool targetControlled,
            int enemiesInLane)
        {
            int ammoCapacity = Mathf.Max(1, maxAmmo);
            float ammoMissingRatio = Mathf.Clamp01((ammoCapacity - availableAmmo) / (float)ammoCapacity);
            float rangeReadiness = 1f - Mathf.Clamp01(targetDistance / Mathf.Max(0.5f, attackRange));
            float finisherValue = 1f - Mathf.Clamp01(targetHealthRatio);

            float score =
                ammoMissingRatio * 58f +
                rangeReadiness * 20f +
                finisherValue * 22f +
                Mathf.Min(18f, Mathf.Max(0, enemiesInLane - 1) * 9f);

            if (targetControlled)
                score += 12f;

            bool emptyOrNearlyEmpty = availableAmmo <= 1;
            bool shouldUse = emptyOrNearlyEmpty && score >= 62f;

            return new AIBrawlerPackDecision(
                shouldUse,
                shouldUse,
                score,
                shouldUse ? "ammo_refill_pressure" : "ammo_refill_hold");
        }

        public static AIBrawlerPackDecision EvaluateSuperChargeGadget(
            float chargePercent,
            float chargeFraction,
            bool superReady,
            float targetDistance,
            float superRange,
            int nearbyEnemyCount,
            bool targetControlled)
        {
            if (superReady)
            {
                return new AIBrawlerPackDecision(
                    false,
                    false,
                    0f,
                    "super_charge_ready");
            }

            float postCharge = Mathf.Clamp01(chargePercent + Mathf.Max(0f, chargeFraction));
            float fillValue = postCharge >= 1f ? 48f : postCharge * 22f;
            float rangeReadiness = 1f - Mathf.Clamp01(targetDistance / Mathf.Max(0.5f, superRange));
            float pressure = Mathf.Clamp01(nearbyEnemyCount / 3f);

            float score = fillValue + rangeReadiness * 20f + pressure * 22f;
            if (targetControlled)
                score += 10f;

            bool shouldUse = postCharge >= 1f && score >= 60f;
            return new AIBrawlerPackDecision(
                shouldUse,
                shouldUse,
                score,
                shouldUse ? "super_charge_combo" : "super_charge_hold");
        }

        public static AIBrawlerPackDecision EvaluateSelfHealGadget(
            float selfHealthRatio,
            int enemyPressureCount,
            bool isGemCarrier)
        {
            float score =
                (1f - Mathf.Clamp01(selfHealthRatio)) * 82f +
                Mathf.Min(24f, Mathf.Max(0, enemyPressureCount) * 8f);

            if (isGemCarrier)
                score += 14f;

            bool shouldUse = score >= 58f;
            return new AIBrawlerPackDecision(
                shouldUse,
                shouldUse,
                score,
                shouldUse ? "self_heal_pressure" : "self_heal_hold");
        }

        public static float ScoreChainBounceAnchor(
            int bounceTargets,
            bool requestedTargetBonus,
            float targetHealthRatio,
            bool targetControlled)
        {
            float score = Mathf.Max(1, bounceTargets) * 20f;

            if (requestedTargetBonus)
                score += 8f;

            score += (1f - Mathf.Clamp01(targetHealthRatio)) * 16f;

            if (targetControlled)
                score += 12f;

            return score;
        }

        public static AIBrawlerPackDecision EvaluateLinePressureCommit(
            int enemiesInLane,
            int alliesInLane,
            float targetHealthRatio,
            bool targetControlled,
            int availableAmmo,
            bool isSuper)
        {
            float score =
                Mathf.Max(1, enemiesInLane) * 22f +
                (1f - Mathf.Clamp01(targetHealthRatio)) * 28f -
                Mathf.Max(0, alliesInLane) * 18f;

            if (targetControlled)
                score += 24f;

            if (availableAmmo <= 1 && !isSuper)
                score -= 18f;

            if (isSuper)
                score += 14f;

            bool shouldUse = score >= (isSuper ? 58f : 46f);
            return new AIBrawlerPackDecision(
                shouldUse,
                isSuper && score >= 66f,
                score,
                shouldUse ? "line_pressure" : "line_hold");
        }

        public static AIBrawlerPackDecision EvaluateAreaDenialCommit(
            int enemyPressureCount,
            int allyRiskCount,
            float targetHealthRatio,
            bool targetControlled,
            bool hasLingeringHazard,
            bool isSuper)
        {
            float score =
                Mathf.Max(1, enemyPressureCount) * 21f +
                (1f - Mathf.Clamp01(targetHealthRatio)) * 22f -
                Mathf.Max(0, allyRiskCount) * 22f;

            if (targetControlled)
                score += 24f;

            if (hasLingeringHazard)
                score += 12f;

            if (isSuper)
                score += 14f;

            bool shouldUse = score >= (isSuper ? 56f : 42f);
            return new AIBrawlerPackDecision(
                shouldUse,
                isSuper && score >= 64f,
                score,
                shouldUse ? "area_denial" : "area_hold");
        }
    }
}
