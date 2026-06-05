using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIObjectiveIntentContext
    {
        public readonly AIGameModeMacroState MacroState;
        public readonly int SelfCarriedGems;
        public readonly bool HasLiveTarget;
        public readonly bool HasLaneHold;
        public readonly bool HasGemPickup;
        public readonly bool ShouldPickupGem;
        public readonly float GemPickupScore;
        public readonly bool IsCarrierPlaybook;
        public readonly bool SelfIsCarrierAnchor;

        public AIObjectiveIntentContext(
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            bool hasLiveTarget,
            bool hasLaneHold,
            bool hasGemPickup,
            bool shouldPickupGem,
            float gemPickupScore,
            bool isCarrierPlaybook,
            bool selfIsCarrierAnchor)
        {
            MacroState = macroState;
            SelfCarriedGems = Mathf.Max(0, selfCarriedGems);
            HasLiveTarget = hasLiveTarget;
            HasLaneHold = hasLaneHold;
            HasGemPickup = hasGemPickup;
            ShouldPickupGem = shouldPickupGem;
            GemPickupScore = Mathf.Max(0f, gemPickupScore);
            IsCarrierPlaybook = isCarrierPlaybook;
            SelfIsCarrierAnchor = selfIsCarrierAnchor;
        }

        public bool SelfIsCarrier => SelfCarriedGems > 0;
        public bool EnemyCountdownReset =>
            MacroState.Call == AIGameModeMacroCall.Reset &&
            MacroState.EnemyTeamHasCountdown;
        public bool CarrierSafetyPhase =>
            SelfIsCarrier &&
            (MacroState.Call == AIGameModeMacroCall.Hold ||
             MacroState.OwnTeamHasCountdown ||
             SelfIsCarrierAnchor ||
             IsCarrierPlaybook);
    }

    public readonly struct AIObjectiveIntentArbitrationResult
    {
        public readonly float Delta;
        public readonly string Reason;

        public AIObjectiveIntentArbitrationResult(float delta, string reason)
        {
            Delta = delta;
            Reason = string.IsNullOrEmpty(reason) ? "intent_none" : reason;
        }

        public bool HasDelta => Mathf.Abs(Delta) > 0.01f;

        public static AIObjectiveIntentArbitrationResult None =>
            new AIObjectiveIntentArbitrationResult(0f, "intent_none");
    }

    public static class AIObjectiveIntentArbitrationUtility
    {
        public static AIObjectiveIntentArbitrationResult Evaluate(
            AIActionType actionType,
            in AIObjectiveIntentContext context)
        {
            float delta = 0f;
            string reason = string.Empty;

            ApplyCarrierSafety(actionType, context, ref delta, ref reason);
            ApplyEnemyCountdownReset(actionType, context, ref delta, ref reason);
            ApplyGemPickupIntent(actionType, context, ref delta, ref reason);
            ApplyLaneHoldIntent(actionType, context, ref delta, ref reason);
            ApplyCombatLock(actionType, context, ref delta, ref reason);

            if (Mathf.Abs(delta) <= 0.01f)
                return AIObjectiveIntentArbitrationResult.None;

            return new AIObjectiveIntentArbitrationResult(delta, reason);
        }

        private static void ApplyCarrierSafety(
            AIActionType actionType,
            in AIObjectiveIntentContext context,
            ref float delta,
            ref string reason)
        {
            if (!context.CarrierSafetyPhase)
                return;

            float gemPressure = Mathf.Min(30f, context.SelfCarriedGems * 5f);

            switch (actionType)
            {
                case AIActionType.Retreat:
                    Add(ref delta, ref reason, gemPressure, "carrier_safety");
                    break;

                case AIActionType.Regroup:
                    Add(ref delta, ref reason, Mathf.Min(24f, context.SelfCarriedGems * 4f), "carrier_safety");
                    break;

                case AIActionType.HoldRange:
                    Add(ref delta, ref reason, 8f, "carrier_safety");
                    break;

                case AIActionType.Approach:
                    Add(ref delta, ref reason, -16f, "carrier_safety");
                    break;

                case AIActionType.Search:
                    Add(ref delta, ref reason, -18f, "carrier_safety");
                    break;

                case AIActionType.Objective:
                    Add(ref delta, ref reason, -24f, "carrier_safety");
                    break;
            }
        }

        private static void ApplyEnemyCountdownReset(
            AIActionType actionType,
            in AIObjectiveIntentContext context,
            ref float delta,
            ref string reason)
        {
            if (!context.EnemyCountdownReset || context.SelfIsCarrier)
                return;

            switch (actionType)
            {
                case AIActionType.Search:
                    Add(ref delta, ref reason, 18f, "countdown_reset");
                    break;

                case AIActionType.Objective:
                    Add(ref delta, ref reason, 14f, "countdown_reset");
                    break;

                case AIActionType.Approach:
                    Add(ref delta, ref reason, 10f, "countdown_reset");
                    break;

                case AIActionType.UseSuper:
                    Add(ref delta, ref reason, 6f, "countdown_reset");
                    break;

                case AIActionType.Retreat:
                case AIActionType.Regroup:
                    Add(ref delta, ref reason, -8f, "countdown_reset");
                    break;
            }
        }

        private static void ApplyGemPickupIntent(
            AIActionType actionType,
            in AIObjectiveIntentContext context,
            ref float delta,
            ref string reason)
        {
            if (!context.HasGemPickup || context.HasLiveTarget)
                return;

            if (context.ShouldPickupGem)
            {
                float gemBonus = Mathf.Clamp(context.GemPickupScore * 0.22f, 8f, 24f);

                if (actionType == AIActionType.Search)
                    Add(ref delta, ref reason, gemBonus, "gem_pickup");
                else if (actionType == AIActionType.Wander)
                    Add(ref delta, ref reason, -10f, "gem_pickup");

                return;
            }

            if (actionType == AIActionType.Search && context.GemPickupScore > 0f)
                Add(ref delta, ref reason, Mathf.Clamp(context.GemPickupScore * 0.06f, 0f, 6f), "gem_watch");
        }

        private static void ApplyLaneHoldIntent(
            AIActionType actionType,
            in AIObjectiveIntentContext context,
            ref float delta,
            ref string reason)
        {
            if (!context.HasLaneHold ||
                context.HasLiveTarget ||
                context.EnemyCountdownReset ||
                context.CarrierSafetyPhase)
            {
                return;
            }

            switch (actionType)
            {
                case AIActionType.Search:
                    Add(ref delta, ref reason, 8f, "lane_hold");
                    break;

                case AIActionType.Objective:
                    Add(ref delta, ref reason, 5f, "lane_hold");
                    break;

                case AIActionType.Wander:
                    Add(ref delta, ref reason, -6f, "lane_hold");
                    break;
            }
        }

        private static void ApplyCombatLock(
            AIActionType actionType,
            in AIObjectiveIntentContext context,
            ref float delta,
            ref string reason)
        {
            if (!context.HasLiveTarget)
                return;

            switch (actionType)
            {
                case AIActionType.Objective:
                case AIActionType.Search:
                case AIActionType.Wander:
                    Add(ref delta, ref reason, -20f, "combat_lock");
                    break;
            }
        }

        private static void Add(
            ref float delta,
            ref string reason,
            float amount,
            string label)
        {
            if (Mathf.Abs(amount) <= 0.01f)
                return;

            delta += amount;
            reason = string.IsNullOrEmpty(reason)
                ? label
                : $"{reason}|{label}";
        }
    }
}
