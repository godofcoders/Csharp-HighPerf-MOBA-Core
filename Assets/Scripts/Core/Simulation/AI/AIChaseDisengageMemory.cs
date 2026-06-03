using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public struct AIChaseDisengageContext
    {
        public int TargetEntityId;
        public uint Tick;
        public float Distance;
        public float TargetHealthRatio;
        public float ChaseHealthThreshold;
        public float MaxChaseDistance;
        public int SelfCarriedGems;
        public int TargetCarriedGems;
        public bool PreserveLaneShape;
        public bool TargetInBadMapPosition;
        public uint CommitTicks;
        public uint MaxTicks;
        public uint CooldownTicks;
        public float BreakDistanceMultiplier;
        public float CommitScoreBonus;
        public float DisengageScorePenalty;
        public float BadMapPenalty;
    }

    public readonly struct AIChaseDisengageDecision
    {
        public readonly int TargetEntityId;
        public readonly bool IsActive;
        public readonly bool ShouldChase;
        public readonly bool ShouldDisengage;
        public readonly float ScoreDelta;
        public readonly uint ElapsedTicks;
        public readonly string Reason;

        public AIChaseDisengageDecision(
            int targetEntityId,
            bool isActive,
            bool shouldChase,
            bool shouldDisengage,
            float scoreDelta,
            uint elapsedTicks,
            string reason)
        {
            TargetEntityId = targetEntityId;
            IsActive = isActive;
            ShouldChase = shouldChase;
            ShouldDisengage = shouldDisengage;
            ScoreDelta = scoreDelta;
            ElapsedTicks = elapsedTicks;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
        }

        public static AIChaseDisengageDecision None(string reason = "none")
        {
            return new AIChaseDisengageDecision(
                0,
                false,
                false,
                false,
                0f,
                0u,
                reason);
        }

        public string GetDebugSummary()
        {
            if (!IsActive && !ShouldDisengage && Mathf.Abs(ScoreDelta) <= 0.01f)
                return $"Chase=None Reason={Reason}";

            return
                $"Chase={(IsActive ? "Active" : "Off")} " +
                $"Target={TargetEntityId} " +
                $"Elapsed={ElapsedTicks} " +
                $"Delta={ScoreDelta:+0.0;-0.0;0.0} " +
                $"Disengage={ShouldDisengage} " +
                $"Reason={Reason}";
        }
    }

    public sealed class AIChaseDisengageMemory
    {
        private int _activeTargetEntityId;
        private uint _startedTick;
        private uint _cooldownUntilTick;

        public bool HasActiveChase => _activeTargetEntityId != 0;
        public int ActiveTargetEntityId => _activeTargetEntityId;

        public void Reset()
        {
            _activeTargetEntityId = 0;
            _startedTick = 0u;
            _cooldownUntilTick = 0u;
        }

        public AIChaseDisengageDecision Evaluate(
            in AIChaseDisengageContext context)
        {
            if (context.TargetEntityId == 0)
            {
                Reset();
                return AIChaseDisengageDecision.None("no_target");
            }

            float threshold = Mathf.Clamp(context.ChaseHealthThreshold, 0.05f, 0.85f);
            float maxDistance = Mathf.Max(1f, context.MaxChaseDistance);
            float breakDistance = maxDistance *
                                  Mathf.Clamp(context.BreakDistanceMultiplier, 1.05f, 2.5f);
            uint commitTicks = context.CommitTicks == 0u ? 1u : context.CommitTicks;
            uint maxTicks = context.MaxTicks == 0u ? commitTicks : context.MaxTicks;
            bool targetLow = context.TargetHealthRatio <= threshold;
            bool targetValuable = context.TargetCarriedGems > 0;
            bool selfCarrier = context.SelfCarriedGems > 0;
            bool activeSameTarget = _activeTargetEntityId == context.TargetEntityId;

            if (HasActiveChase && !activeSameTarget)
            {
                Stop(context.Tick, context.CooldownTicks);
                activeSameTarget = false;
            }

            bool cooldownActive = !HasActiveChase && context.Tick < _cooldownUntilTick;
            if (cooldownActive && !targetValuable)
            {
                return new AIChaseDisengageDecision(
                    context.TargetEntityId,
                    false,
                    false,
                    false,
                    -context.DisengageScorePenalty * 0.35f,
                    0u,
                    "cooldown");
            }

            float valuableDistanceBoost = targetValuable ? 1.25f : 1f;
            float effectiveMaxDistance = maxDistance * valuableDistanceBoost;
            bool overSoftDistance = context.Distance > effectiveMaxDistance;
            bool overHardDistance = context.Distance > breakDistance * valuableDistanceBoost;
            bool unsafeMap = context.TargetInBadMapPosition &&
                             context.PreserveLaneShape &&
                             !targetValuable;
            bool unsafeLaneBreak = context.PreserveLaneShape &&
                                   !targetValuable &&
                                   (selfCarrier || overSoftDistance);

            if (activeSameTarget)
            {
                uint elapsed = context.Tick - _startedTick;

                if (unsafeMap)
                    return EndChase(context, elapsed, "bad_map", 1f);

                if (overHardDistance)
                    return EndChase(context, elapsed, "break_distance", 1f);

                if (elapsed >= maxTicks && !targetValuable)
                    return EndChase(context, elapsed, "timebox", 0.85f);

                if (!targetLow && !targetValuable && elapsed >= commitTicks)
                    return EndChase(context, elapsed, "target_recovered", 0.65f);

                float commitFactor = elapsed <= commitTicks ? 1f : 0.55f;
                float valueBonus = targetValuable ? context.TargetCarriedGems * 2f : 0f;
                float badMapTax = context.TargetInBadMapPosition
                    ? context.BadMapPenalty * (targetValuable ? 0.35f : 0.75f)
                    : 0f;

                return new AIChaseDisengageDecision(
                    context.TargetEntityId,
                    true,
                    true,
                    false,
                    context.CommitScoreBonus * commitFactor + valueBonus - badMapTax,
                    elapsed,
                    targetValuable ? "continue_valuable" : "continue");
            }

            if (!targetLow && !targetValuable)
                return AIChaseDisengageDecision.None("not_worth_chase");

            if (unsafeMap || unsafeLaneBreak)
            {
                return new AIChaseDisengageDecision(
                    context.TargetEntityId,
                    false,
                    false,
                    true,
                    -context.DisengageScorePenalty,
                    0u,
                    unsafeMap ? "deny_bad_map" : "deny_lane_break");
            }

            if (context.Distance > effectiveMaxDistance)
            {
                float valuableDiscount = targetValuable ? 0.45f : 0.75f;
                return new AIChaseDisengageDecision(
                    context.TargetEntityId,
                    false,
                    false,
                    true,
                    -context.DisengageScorePenalty * valuableDiscount,
                    0u,
                    "deny_distance");
            }

            _activeTargetEntityId = context.TargetEntityId;
            _startedTick = context.Tick;

            return new AIChaseDisengageDecision(
                context.TargetEntityId,
                true,
                true,
                false,
                context.CommitScoreBonus + (targetValuable ? context.TargetCarriedGems * 2f : 0f),
                0u,
                targetValuable ? "start_valuable" : "start_low");
        }

        private AIChaseDisengageDecision EndChase(
            in AIChaseDisengageContext context,
            uint elapsedTicks,
            string reason,
            float penaltyScale)
        {
            Stop(context.Tick, context.CooldownTicks);

            return new AIChaseDisengageDecision(
                context.TargetEntityId,
                false,
                false,
                true,
                -context.DisengageScorePenalty * Mathf.Max(0.1f, penaltyScale),
                elapsedTicks,
                reason);
        }

        private void Stop(uint currentTick, uint cooldownTicks)
        {
            _activeTargetEntityId = 0;
            _startedTick = 0u;
            _cooldownUntilTick = currentTick + cooldownTicks;
        }
    }
}
