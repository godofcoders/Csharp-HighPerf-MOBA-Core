using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIWinConditionTargetContext
    {
        public readonly AIGameModeMacroState MacroState;
        public readonly int SelfCarriedGems;
        public readonly int TargetCarriedGems;
        public readonly float TargetHealthRatio;
        public readonly float Distance;
        public readonly bool IsCurrentTarget;
        public readonly bool IsTeamFocusTarget;
        public readonly int AlliedFocusCount;

        public AIWinConditionTargetContext(
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            int targetCarriedGems,
            float targetHealthRatio,
            float distance,
            bool isCurrentTarget,
            bool isTeamFocusTarget,
            int alliedFocusCount)
        {
            MacroState = macroState;
            SelfCarriedGems = Mathf.Max(0, selfCarriedGems);
            TargetCarriedGems = Mathf.Max(0, targetCarriedGems);
            TargetHealthRatio = Mathf.Clamp01(targetHealthRatio);
            Distance = Mathf.Max(0f, distance);
            IsCurrentTarget = isCurrentTarget;
            IsTeamFocusTarget = isTeamFocusTarget;
            AlliedFocusCount = Mathf.Max(0, alliedFocusCount);
        }
    }

    public readonly struct AIWinConditionTargetEvaluation
    {
        public readonly float ScoreDelta;
        public readonly bool IsHighValueTarget;
        public readonly bool ShouldCollapse;
        public readonly string Reason;

        public AIWinConditionTargetEvaluation(
            float scoreDelta,
            bool isHighValueTarget,
            bool shouldCollapse,
            string reason)
        {
            ScoreDelta = scoreDelta;
            IsHighValueTarget = isHighValueTarget;
            ShouldCollapse = shouldCollapse;
            Reason = string.IsNullOrEmpty(reason) ? "win_none" : reason;
        }

        public bool HasDelta => ScoreDelta < -0.01f || ScoreDelta > 0.01f;

        public static AIWinConditionTargetEvaluation None =>
            new AIWinConditionTargetEvaluation(0f, false, false, "win_none");
    }

    public readonly struct AIWinConditionActionContext
    {
        public readonly AIGameModeMacroState MacroState;
        public readonly int SelfCarriedGems;
        public readonly int TargetCarriedGems;
        public readonly float TargetHealthRatio;
        public readonly bool HasLiveTarget;

        public AIWinConditionActionContext(
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            int targetCarriedGems,
            float targetHealthRatio,
            bool hasLiveTarget)
        {
            MacroState = macroState;
            SelfCarriedGems = Mathf.Max(0, selfCarriedGems);
            TargetCarriedGems = Mathf.Max(0, targetCarriedGems);
            TargetHealthRatio = Mathf.Clamp01(targetHealthRatio);
            HasLiveTarget = hasLiveTarget;
        }
    }

    public readonly struct AIWinConditionActionEvaluation
    {
        public readonly float Delta;
        public readonly string Reason;

        public AIWinConditionActionEvaluation(float delta, string reason)
        {
            Delta = delta;
            Reason = string.IsNullOrEmpty(reason) ? "win_none" : reason;
        }

        public bool HasDelta => Delta < -0.01f || Delta > 0.01f;

        public static AIWinConditionActionEvaluation None =>
            new AIWinConditionActionEvaluation(0f, "win_none");
    }

    public static class AIWinConditionUtility
    {
        private const float MaxTargetDelta = 95f;
        private const float MaxActionDelta = 34f;
        private const float CloseConfirmDistance = 4.50f;
        private const float ReliablePressureDistance = 7.00f;
        private const float LongCollapseDistance = 11.00f;

        public static AIWinConditionTargetEvaluation EvaluateTarget(
            AIWinConditionTargetContext context)
        {
            if (context.MacroState.Phase == AIGameModeObjectivePhase.None &&
                context.TargetCarriedGems <= 0 &&
                context.TargetHealthRatio > 0.35f)
            {
                return AIWinConditionTargetEvaluation.None;
            }

            float score = 0f;
            bool isHighValue = false;
            bool shouldCollapse = false;
            string reason = string.Empty;

            AddLowHealthTargetPressure(
                context.TargetHealthRatio,
                ref score,
                ref isHighValue,
                ref reason);

            switch (context.MacroState.Mode)
            {
                case GameModeId.GemGrab:
                    AddGemGrabTargetPressure(
                        context,
                        ref score,
                        ref isHighValue,
                        ref shouldCollapse,
                        ref reason);
                    break;

                case GameModeId.Knockout:
                    AddKnockoutTargetPressure(
                        context,
                        ref score,
                        ref isHighValue,
                        ref shouldCollapse,
                        ref reason);
                    break;

                default:
                    if (context.MacroState.Call == AIGameModeMacroCall.Push &&
                        context.TargetHealthRatio <= 0.40f)
                    {
                        score += 8f;
                        reason = AppendReason(reason, "push_confirm");
                    }

                    break;
            }

            ApplyDistanceReliability(
                context,
                ref score,
                ref shouldCollapse,
                ref reason);

            if (context.IsTeamFocusTarget && isHighValue)
            {
                score += shouldCollapse ? 10f : 6f;
                reason = AppendReason(reason, "team_focus");
            }

            if (context.IsCurrentTarget && isHighValue)
            {
                score += 4f;
                reason = AppendReason(reason, "commit");
            }

            if (score <= 0.01f)
                return AIWinConditionTargetEvaluation.None;

            return new AIWinConditionTargetEvaluation(
                Mathf.Clamp(score, 0f, MaxTargetDelta),
                isHighValue,
                shouldCollapse,
                reason);
        }

        public static AIWinConditionActionEvaluation EvaluateAction(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            switch (context.MacroState.Mode)
            {
                case GameModeId.GemGrab:
                    return EvaluateGemGrabAction(actionType, context);

                case GameModeId.Knockout:
                    return EvaluateKnockoutAction(actionType, context);

                default:
                    return EvaluateGenericAction(actionType, context);
            }
        }

        private static void AddLowHealthTargetPressure(
            float healthRatio,
            ref float score,
            ref bool isHighValue,
            ref string reason)
        {
            if (healthRatio <= 0.20f)
            {
                score += 24f;
                isHighValue = true;
                reason = AppendReason(reason, "execute");
                return;
            }

            if (healthRatio <= 0.35f)
            {
                score += 12f;
                isHighValue = true;
                reason = AppendReason(reason, "low_health");
            }
        }

        private static void AddGemGrabTargetPressure(
            AIWinConditionTargetContext context,
            ref float score,
            ref bool isHighValue,
            ref bool shouldCollapse,
            ref string reason)
        {
            if (context.TargetCarriedGems <= 0)
                return;

            isHighValue = true;

            float gemBonus = Mathf.Min(54f, context.TargetCarriedGems * 9f);
            score += gemBonus;
            reason = AppendReason(reason, "gem_carrier");

            if (context.MacroState.IsBehind)
            {
                score += Mathf.Min(18f, context.TargetCarriedGems * 4f);
                reason = AppendReason(reason, "behind_swing");
            }

            if (context.MacroState.EnemyTeamHasCountdown)
            {
                float urgency = GetCountdownUrgency(context.MacroState);
                score += 32f + urgency * 18f;
                shouldCollapse = true;
                reason = AppendReason(reason, "break_countdown");
            }

            int gemsToWin = Mathf.Max(1, context.MacroState.GemsToWin);
            if (context.TargetCarriedGems >= 3 ||
                context.MacroState.EnemyGems >= gemsToWin - 1)
            {
                shouldCollapse = true;
                reason = AppendReason(reason, "swing_target");
            }

            int enemyLead = context.MacroState.EnemyGems - context.MacroState.OwnGems;
            if (enemyLead > 0 &&
                context.TargetCarriedGems >= enemyLead)
            {
                score += 14f;
                shouldCollapse = true;
                reason = AppendReason(reason, "lead_swing_target");
            }

            if (context.TargetCarriedGems >= Mathf.CeilToInt(gemsToWin * 0.50f))
            {
                score += 8f;
                shouldCollapse = true;
                reason = AppendReason(reason, "primary_carrier");
            }

            if (context.TargetHealthRatio <= 0.35f)
            {
                score += context.TargetHealthRatio <= 0.20f ? 18f : 12f;
                shouldCollapse = true;
                reason = AppendReason(reason, "carrier_finish");
            }

            if (context.AlliedFocusCount > 0 && shouldCollapse)
            {
                score += Mathf.Min(12f, context.AlliedFocusCount * 4f);
                reason = AppendReason(reason, "collapse_ready");
            }

            if (context.SelfCarriedGems > 0 &&
                context.MacroState.OwnTeamHasCountdown)
            {
                score -= Mathf.Min(18f, context.SelfCarriedGems * 3f);
                reason = AppendReason(reason, "protect_own_gems");
            }
        }

        private static void AddKnockoutTargetPressure(
            AIWinConditionTargetContext context,
            ref float score,
            ref bool isHighValue,
            ref bool shouldCollapse,
            ref string reason)
        {
            if (context.TargetHealthRatio > 0.45f)
                return;

            isHighValue = true;
            shouldCollapse = context.TargetHealthRatio <= 0.28f ||
                             context.MacroState.Call == AIGameModeMacroCall.Push;
            score += context.TargetHealthRatio <= 0.28f ? 22f : 12f;

            if (context.TargetHealthRatio <= 0.28f &&
                context.Distance <= ReliablePressureDistance)
            {
                score += 10f;
                reason = AppendReason(reason, "confirm_window");
            }

            if (context.MacroState.Call == AIGameModeMacroCall.Push)
            {
                score += 10f;
                reason = AppendReason(reason, "numbers_confirm");
            }

            if (context.MacroState.Call == AIGameModeMacroCall.Reset)
            {
                score += 6f;
                reason = AppendReason(reason, "safe_trade");
            }

            if (context.MacroState.IsBehind)
            {
                score += 8f;
                reason = AppendReason(reason, "comeback_pick");
            }

            if (context.AlliedFocusCount > 0 && shouldCollapse)
            {
                score += Mathf.Min(10f, context.AlliedFocusCount * 5f);
                reason = AppendReason(reason, "collapse_ready");
            }

            reason = AppendReason(reason, "knockout_pick");
        }

        private static void ApplyDistanceReliability(
            AIWinConditionTargetContext context,
            ref float score,
            ref bool shouldCollapse,
            ref string reason)
        {
            if (score <= 0.01f)
                return;

            bool hasFinishWindow = context.TargetHealthRatio <= 0.35f;
            bool carriesObjectiveValue = context.TargetCarriedGems > 0;

            if (context.Distance <= CloseConfirmDistance)
            {
                score += hasFinishWindow ? 12f : 6f;
                reason = AppendReason(reason, "close_confirm");

                if (hasFinishWindow)
                    shouldCollapse = true;

                return;
            }

            if (context.Distance <= ReliablePressureDistance &&
                (hasFinishWindow || carriesObjectiveValue))
            {
                score += 5f;
                reason = AppendReason(reason, "reachable_target");
                return;
            }

            if (context.Distance < LongCollapseDistance ||
                context.MacroState.EnemyTeamHasCountdown)
            {
                return;
            }

            score -= Mathf.Min(16f, 4f + (context.Distance - LongCollapseDistance) * 2.5f);
            reason = AppendReason(reason, "far_target");

            if (!hasFinishWindow &&
                context.AlliedFocusCount <= 0)
            {
                shouldCollapse = false;
            }
        }

        private static AIWinConditionActionEvaluation EvaluateGemGrabAction(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            if (context.MacroState.OwnTeamHasCountdown &&
                context.SelfCarriedGems > 0)
            {
                switch (actionType)
                {
                    case AIActionType.Retreat:
                    case AIActionType.Regroup:
                        return Result(24f, "win_hold_carrier");

                    case AIActionType.HoldRange:
                    case AIActionType.Reposition:
                        return Result(10f, "win_hold_carrier");

                    case AIActionType.Approach:
                    case AIActionType.Search:
                    case AIActionType.Objective:
                        return Result(-16f, "win_hold_carrier");
                }
            }

            if (context.HasLiveTarget)
            {
                if (context.TargetCarriedGems > 0)
                    return EvaluateGemGrabCarrierAction(actionType, context);

                if (context.TargetHealthRatio <= 0.30f)
                    return EvaluateLowHealthAction(actionType, 10f, "finish_target");

                return AIWinConditionActionEvaluation.None;
            }

            if (ShouldControlGemMine(context.MacroState))
            {
                float urgency = context.MacroState.EnemyTeamHasCountdown
                    ? 1f + GetCountdownUrgency(context.MacroState)
                    : 1f;

                switch (actionType)
                {
                    case AIActionType.Objective:
                        return Result(Mathf.Min(28f, 14f * urgency), "gem_mine_control");

                    case AIActionType.Search:
                        return Result(Mathf.Min(18f, 9f * urgency), "gem_mine_control");

                    case AIActionType.Wander:
                        return Result(-8f, "gem_mine_control");
                }
            }

            return AIWinConditionActionEvaluation.None;
        }

        private static AIWinConditionActionEvaluation EvaluateGemGrabCarrierAction(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            float carrierPressure = Mathf.Min(22f, 6f + context.TargetCarriedGems * 4f);

            if (context.MacroState.EnemyTeamHasCountdown)
                carrierPressure += 10f + GetCountdownUrgency(context.MacroState) * 8f;

            switch (actionType)
            {
                case AIActionType.Approach:
                    return Result(carrierPressure, "carrier_pressure");

                case AIActionType.UseSuper:
                    return Result(carrierPressure + 6f, "carrier_burst");

                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return Result(Mathf.Min(14f, carrierPressure * 0.55f), "carrier_angle");

                case AIActionType.Search:
                case AIActionType.Objective:
                case AIActionType.Wander:
                    return Result(-12f, "carrier_pressure");
            }

            return AIWinConditionActionEvaluation.None;
        }

        private static AIWinConditionActionEvaluation EvaluateKnockoutAction(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            if (context.MacroState.Call == AIGameModeMacroCall.Reset)
            {
                if (actionType == AIActionType.Retreat ||
                    actionType == AIActionType.Regroup ||
                    actionType == AIActionType.Reposition)
                {
                    return Result(10f, "knockout_stabilize");
                }
            }

            if (!context.HasLiveTarget ||
                context.TargetHealthRatio > 0.35f)
            {
                return AIWinConditionActionEvaluation.None;
            }

            return EvaluateLowHealthAction(actionType, 12f, "knockout_confirm");
        }

        private static AIWinConditionActionEvaluation EvaluateGenericAction(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            if (!context.HasLiveTarget ||
                context.TargetHealthRatio > 0.28f ||
                context.MacroState.Call != AIGameModeMacroCall.Push)
            {
                return AIWinConditionActionEvaluation.None;
            }

            return EvaluateLowHealthAction(actionType, 8f, "win_confirm");
        }

        private static AIWinConditionActionEvaluation EvaluateLowHealthAction(
            AIActionType actionType,
            float baseDelta,
            string reason)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return Result(baseDelta + 4f, reason);

                case AIActionType.UseSuper:
                    return Result(baseDelta + 8f, reason);

                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return Result(baseDelta, reason);

                case AIActionType.Search:
                case AIActionType.Objective:
                case AIActionType.Wander:
                    return Result(-8f, reason);
            }

            return AIWinConditionActionEvaluation.None;
        }

        private static bool ShouldControlGemMine(AIGameModeMacroState state)
        {
            if (state.Mode != GameModeId.GemGrab)
                return false;

            if (state.EnemyTeamHasCountdown)
                return true;

            return state.Phase == AIGameModeObjectivePhase.Opening ||
                   state.Phase == AIGameModeObjectivePhase.Contest ||
                   state.Call == AIGameModeMacroCall.Push ||
                   state.Call == AIGameModeMacroCall.Reset ||
                   state.IsBehind;
        }

        private static float GetCountdownUrgency(AIGameModeMacroState state)
        {
            if (!state.OwnTeamHasCountdown && !state.EnemyTeamHasCountdown)
                return 0f;

            if (state.WinTimerRemainingSeconds <= 0f)
                return 0.35f;

            return Mathf.Clamp01((10f - state.WinTimerRemainingSeconds) / 10f);
        }

        private static AIWinConditionActionEvaluation Result(float delta, string reason)
        {
            return new AIWinConditionActionEvaluation(
                Mathf.Clamp(delta, -MaxActionDelta, MaxActionDelta),
                reason);
        }

        private static string AppendReason(string current, string value)
        {
            return string.IsNullOrEmpty(current)
                ? value
                : $"{current}|{value}";
        }
    }
}
