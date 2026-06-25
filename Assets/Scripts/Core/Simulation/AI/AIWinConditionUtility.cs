using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
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
        private const float MaxActionDelta = 34f;

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

    }
}
