using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public struct AIOpponentResourceSnapshot
    {
        public bool HasTarget;
        public int AvailableAmmo;
        public int MaxAmmo;
        public float CurrentAmmo;
        public float AmmoPressure;
        public bool CanUseMainAttack;
        public bool SuperReady;
        public bool CanUseSuper;
        public float SuperChargePercent;

        public string GetDebugSummary()
        {
            if (!HasTarget)
                return "ResAware=None";

            return
                $"ResAware=ammo:{CurrentAmmo:0.0}/{MaxAmmo} bars={AvailableAmmo} pressure={AmmoPressure:0.00} " +
                $"attack={CanUseMainAttack} super={SuperChargePercent:0.00}/{SuperReady}";
        }
    }

    public static class AIOpponentResourceUtility
    {
        public static AIOpponentResourceSnapshot Evaluate(
            BrawlerController target,
            uint currentTick)
        {
            if (target == null || target.State == null || target.State.IsDead)
                return default;

            int maxAmmo = target.State.Ammo != null
                ? Mathf.Max(1, target.State.Ammo.MaxAmmo)
                : 1;
            float currentAmmo = target.State.Ammo != null
                ? Mathf.Clamp(target.State.Ammo.CurrentAmmo, 0f, maxAmmo)
                : maxAmmo;
            int availableAmmo = target.State.Ammo != null
                ? Mathf.Clamp(target.State.Ammo.AvailableBars, 0, maxAmmo)
                : maxAmmo;
            float ammoPressure = Mathf.Clamp01(1f - currentAmmo / maxAmmo);
            float superCharge = target.State.SuperCharge != null
                ? Mathf.Clamp01(target.State.SuperCharge.ChargePercent)
                : 0f;

            return new AIOpponentResourceSnapshot
            {
                HasTarget = true,
                AvailableAmmo = availableAmmo,
                MaxAmmo = maxAmmo,
                CurrentAmmo = currentAmmo,
                AmmoPressure = ammoPressure,
                CanUseMainAttack = target.State.CanUseMainAttack(currentTick),
                SuperReady = target.State.SuperCharge != null && target.State.SuperCharge.IsReady,
                CanUseSuper = target.State.CanUseSuper(currentTick),
                SuperChargePercent = superCharge
            };
        }

        public static float GetTargetOpportunityScore(
            BrawlerController target,
            uint currentTick,
            BrawlerAIProfile profile,
            out string reason)
        {
            reason = "resource_none";

            if (profile == null)
                return 0f;

            AIOpponentResourceSnapshot snapshot = Evaluate(target, currentTick);
            if (!snapshot.HasTarget)
                return 0f;

            float awareness = Mathf.Max(0f, profile.OpponentResourceAwarenessWeight);
            if (awareness <= 0f)
                return 0f;

            float score = 0f;
            string parts = string.Empty;

            if (snapshot.AmmoPressure > 0.35f)
            {
                float lowAmmoBonus =
                    snapshot.AmmoPressure * profile.EnemyLowAmmoOpportunityBonus;
                score += lowAmmoBonus;
                parts = Append(parts, $"low_ammo_{lowAmmoBonus:0.0}");
            }

            if (!snapshot.CanUseMainAttack || snapshot.AvailableAmmo <= 0)
            {
                float noAttackBonus = profile.EnemyNoAttackApproachBonus;
                score += noAttackBonus;
                parts = Append(parts, $"no_attack_{noAttackBonus:0.0}");
            }

            if (snapshot.SuperReady)
            {
                float superPenalty = profile.EnemySuperReadyThreatPenalty;
                score -= superPenalty;
                parts = Append(parts, $"super_ready_-{superPenalty:0.0}");
            }
            else if (snapshot.SuperChargePercent >= 0.75f)
            {
                float nearSuperPenalty =
                    profile.EnemyNearlySuperThreatPenalty * snapshot.SuperChargePercent;
                score -= nearSuperPenalty;
                parts = Append(parts, $"near_super_-{nearSuperPenalty:0.0}");
            }

            reason = string.IsNullOrEmpty(parts)
                ? snapshot.GetDebugSummary()
                : $"resource_{parts}";
            return Mathf.Clamp(score * awareness, -40f, 50f);
        }

        private static string Append(string current, string value)
        {
            return string.IsNullOrEmpty(current)
                ? value
                : $"{current}|{value}";
        }
    }
}
