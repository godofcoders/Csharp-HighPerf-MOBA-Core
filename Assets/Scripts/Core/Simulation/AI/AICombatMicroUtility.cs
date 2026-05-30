using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public enum AICombatMicroMoveStyle
    {
        None,
        ReloadBait,
        PeekTiming,
        DodgeFeint,
        ThrowerSpacing
    }

    public readonly struct AIAmmoDisciplineDecision
    {
        public readonly bool ShouldHoldFire;
        public readonly string Reason;

        public AIAmmoDisciplineDecision(bool shouldHoldFire, string reason)
        {
            ShouldHoldFire = shouldHoldFire;
            Reason = reason;
        }
    }

    public readonly struct AICombatMicroMovementDecision
    {
        public readonly AICombatMicroMoveStyle Style;
        public readonly string Reason;

        public AICombatMicroMovementDecision(
            AICombatMicroMoveStyle style,
            string reason)
        {
            Style = style;
            Reason = reason;
        }
    }

    public static class AICombatMicroUtility
    {
        private const float LowAmmoShotQualityThreshold = 0.62f;
        private const float LowAmmoAreaQualityThreshold = 0.56f;
        private const float ReloadReserveProgressThreshold = 0.72f;
        private const float FinisherHealthRatio = 0.32f;
        private const uint FeintWindowTicks = 72u;
        private const uint FeintActiveTicks = 7u;

        public static AIAmmoDisciplineDecision EvaluateAmmoDiscipline(
            int availableAmmo,
            int maxAmmo,
            float currentAmmo,
            float shotQuality,
            float targetHealthRatio,
            bool targetControlled,
            int enemyCountInLane,
            int allyCountInLane,
            bool isAreaDenial)
        {
            if (availableAmmo <= 0)
                return new AIAmmoDisciplineDecision(true, "no_ammo");

            if (targetControlled)
                return new AIAmmoDisciplineDecision(false, "controlled_target");

            if (targetHealthRatio <= FinisherHealthRatio)
                return new AIAmmoDisciplineDecision(false, "finisher_window");

            if (enemyCountInLane >= 2)
                return new AIAmmoDisciplineDecision(false, "multi_target_lane");

            if (allyCountInLane > 0 && enemyCountInLane <= 1 && shotQuality < 0.72f)
                return new AIAmmoDisciplineDecision(true, "friendly_lane_discipline");

            int clampedMaxAmmo = Mathf.Max(1, maxAmmo);
            if (availableAmmo >= clampedMaxAmmo)
                return new AIAmmoDisciplineDecision(false, "full_ammo");

            float reserveProgress = GetReloadReserveProgress(currentAmmo);
            if (availableAmmo <= 1 && reserveProgress >= ReloadReserveProgressThreshold && shotQuality < 0.78f)
                return new AIAmmoDisciplineDecision(true, "reload_rhythm");

            if (availableAmmo <= 1)
            {
                float threshold = isAreaDenial
                    ? LowAmmoAreaQualityThreshold
                    : LowAmmoShotQualityThreshold;

                if (shotQuality < threshold)
                    return new AIAmmoDisciplineDecision(true, "low_ammo_hold");
            }

            return new AIAmmoDisciplineDecision(false, "ammo_commit");
        }

        public static AICombatMicroMovementDecision ResolveMovementStyle(
            int availableAmmo,
            int maxAmmo,
            float currentAmmo,
            float targetDistance,
            float preferredRange,
            float tooCloseDistance,
            bool isArtillery,
            bool hasDanger,
            uint currentTick,
            int entityId)
        {
            if (hasDanger)
                return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.None, "danger_override");

            if (isArtillery && targetDistance <= preferredRange + 1.25f)
                return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.ThrowerSpacing, "thrower_spacing");

            float reserveProgress = GetReloadReserveProgress(currentAmmo);
            if (availableAmmo <= 0 ||
                (availableAmmo <= 1 && reserveProgress < ReloadReserveProgressThreshold))
            {
                return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.ReloadBait, "reload_bait");
            }

            if (ShouldUseDodgeFeint(currentTick, entityId) &&
                targetDistance > tooCloseDistance &&
                targetDistance <= preferredRange + 1.5f)
            {
                return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.DodgeFeint, "dodge_feint");
            }

            if (availableAmmo >= Mathf.Min(2, Mathf.Max(1, maxAmmo)) &&
                targetDistance >= preferredRange * 0.75f &&
                targetDistance <= preferredRange + 2.0f)
            {
                return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.PeekTiming, "peek_timing");
            }

            return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.None, "standard");
        }

        public static float GetReloadReserveProgress(float currentAmmo)
        {
            return Mathf.Clamp01(currentAmmo - Mathf.Floor(currentAmmo));
        }

        public static float GetAmmoPressure(int availableAmmo, int maxAmmo, float currentAmmo)
        {
            int clampedMaxAmmo = Mathf.Max(1, maxAmmo);
            float ammoRatio = Mathf.Clamp01(currentAmmo / clampedMaxAmmo);

            if (availableAmmo <= 0)
                return 1f;

            if (availableAmmo <= 1)
                return Mathf.Lerp(0.55f, 0.25f, GetReloadReserveProgress(currentAmmo));

            return Mathf.Clamp01(1f - ammoRatio) * 0.35f;
        }

        private static bool ShouldUseDodgeFeint(uint currentTick, int entityId)
        {
            uint seed = (uint)(entityId < 0 ? -entityId : entityId);
            uint shiftedTick = currentTick + (seed * 17u);
            return shiftedTick % FeintWindowTicks < FeintActiveTicks;
        }
    }
}
