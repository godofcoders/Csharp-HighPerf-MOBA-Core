using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIMacroActionContext
    {
        public readonly AIGameModeMacroState MacroState;
        public readonly int SelfCarriedGems;
        public readonly int TargetCarriedGems;
        public readonly int AllyCarriedGems;

        public AIMacroActionContext(
            AIGameModeMacroState macroState,
            int selfCarriedGems = 0,
            int targetCarriedGems = 0,
            int allyCarriedGems = 0)
        {
            MacroState = macroState;
            SelfCarriedGems = selfCarriedGems > 0 ? selfCarriedGems : 0;
            TargetCarriedGems = targetCarriedGems > 0 ? targetCarriedGems : 0;
            AllyCarriedGems = allyCarriedGems > 0 ? allyCarriedGems : 0;
        }

        public bool SelfIsCarrier => SelfCarriedGems > 0;
    }

    public readonly struct AIMacroActionPolicyResult
    {
        public readonly float Delta;
        public readonly string Reason;

        public AIMacroActionPolicyResult(float delta, string reason)
        {
            Delta = delta;
            Reason = string.IsNullOrEmpty(reason) ? "macro_none" : reason;
        }

        public bool HasDelta => Delta < -0.01f || Delta > 0.01f;

        public static AIMacroActionPolicyResult None =>
            new AIMacroActionPolicyResult(0f, "macro_none");
    }

    public static class AIMacroActionPolicy
    {
        public static AIMacroActionPolicyResult Evaluate(
            AIActionType actionType,
            AIMacroActionContext context)
        {
            if (context.MacroState.Call == AIGameModeMacroCall.Neutral)
                return AIMacroActionPolicyResult.None;

            switch (actionType)
            {
                case AIActionType.Approach:
                    return EvaluateApproach(context);

                case AIActionType.Search:
                    return EvaluateSearch(context);

                case AIActionType.Objective:
                    return EvaluateObjective(context);

                case AIActionType.Retreat:
                case AIActionType.Regroup:
                    return EvaluateCarrierSafety(context);

                case AIActionType.Peel:
                    return EvaluatePeel(context);

                case AIActionType.UseSuper:
                    return EvaluateUseSuper(context);

                default:
                    return AIMacroActionPolicyResult.None;
            }
        }

        private static AIMacroActionPolicyResult EvaluateApproach(
            AIMacroActionContext context)
        {
            AIGameModeMacroCall call = context.MacroState.Call;

            switch (context.MacroState.Mode)
            {
                case GameModeId.Knockout:
                    if (call == AIGameModeMacroCall.Push) return Result(14f, "knockout_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(-16f, "knockout_stabilize");
                    if (call == AIGameModeMacroCall.Hold) return Result(-8f, "knockout_hold");
                    break;

                case GameModeId.BrawlBall:
                    if (call == AIGameModeMacroCall.Push) return Result(18f, "ball_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(10f, "ball_defend");
                    if (call == AIGameModeMacroCall.Hold) return Result(-4f, "ball_hold");
                    break;

                case GameModeId.HotZone:
                    if (call == AIGameModeMacroCall.Push) return Result(10f, "zone_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(12f, "zone_deny");
                    if (call == AIGameModeMacroCall.Hold) return Result(-4f, "zone_hold");
                    break;

                case GameModeId.GemGrab:
                default:
                    if (call == AIGameModeMacroCall.Push) return Result(12f, "macro_push");
                    if (call == AIGameModeMacroCall.Reset)
                    {
                        if (context.MacroState.EnemyTeamHasCountdown)
                        {
                            return Result(
                                28f + GetCountdownUrgency(context.MacroState) * 10f,
                                "countdown_reset");
                        }

                        return Result(18f, "macro_reset");
                    }

                    if (call == AIGameModeMacroCall.Hold && context.SelfIsCarrier)
                        return Result(-18f, "carrier_hold");
                    break;
            }

            return AIMacroActionPolicyResult.None;
        }

        private static AIMacroActionPolicyResult EvaluateSearch(
            AIMacroActionContext context)
        {
            AIGameModeMacroCall call = context.MacroState.Call;

            switch (context.MacroState.Mode)
            {
                case GameModeId.Knockout:
                    if (call == AIGameModeMacroCall.Push) return Result(6f, "knockout_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(-4f, "knockout_stabilize");
                    if (call == AIGameModeMacroCall.Hold) return Result(-8f, "knockout_hold");
                    break;

                case GameModeId.BrawlBall:
                    if (call == AIGameModeMacroCall.Push) return Result(14f, "ball_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(10f, "ball_defend");
                    if (call == AIGameModeMacroCall.Hold) return Result(-4f, "ball_hold");
                    break;

                case GameModeId.HotZone:
                    if (call == AIGameModeMacroCall.Push) return Result(14f, "zone_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(16f, "zone_deny");
                    if (call == AIGameModeMacroCall.Hold) return Result(4f, "zone_hold");
                    break;

                case GameModeId.GemGrab:
                default:
                    if (call == AIGameModeMacroCall.Push) return Result(12f, "macro_push");
                    if (call == AIGameModeMacroCall.Reset)
                    {
                        if (context.MacroState.EnemyTeamHasCountdown)
                        {
                            return Result(
                                12f + GetCountdownUrgency(context.MacroState) * 8f,
                                "countdown_reset");
                        }

                        return Result(8f, "macro_reset");
                    }

                    if (call == AIGameModeMacroCall.Hold) return Result(-10f, "macro_hold");
                    break;
            }

            return AIMacroActionPolicyResult.None;
        }

        private static AIMacroActionPolicyResult EvaluateObjective(
            AIMacroActionContext context)
        {
            AIGameModeMacroCall call = context.MacroState.Call;

            switch (context.MacroState.Mode)
            {
                case GameModeId.Knockout:
                    if (call == AIGameModeMacroCall.Push) return Result(4f, "knockout_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(-10f, "knockout_stabilize");
                    if (call == AIGameModeMacroCall.Hold) return Result(-8f, "knockout_hold");
                    break;

                case GameModeId.BrawlBall:
                    if (call == AIGameModeMacroCall.Push) return Result(14f, "ball_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(10f, "ball_defend");
                    if (call == AIGameModeMacroCall.Hold) return Result(4f, "ball_hold");
                    break;

                case GameModeId.HotZone:
                    if (call == AIGameModeMacroCall.Push) return Result(20f, "zone_push");
                    if (call == AIGameModeMacroCall.Reset) return Result(22f, "zone_deny");
                    if (call == AIGameModeMacroCall.Hold) return Result(14f, "zone_hold");
                    break;

                case GameModeId.GemGrab:
                default:
                    if (call == AIGameModeMacroCall.Push) return Result(16f, "macro_push");
                    if (call == AIGameModeMacroCall.Reset)
                    {
                        if (context.MacroState.EnemyTeamHasCountdown)
                        {
                            return Result(
                                16f + GetCountdownUrgency(context.MacroState) * 10f,
                                "countdown_reset");
                        }

                        return Result(10f, "macro_reset");
                    }

                    if (call == AIGameModeMacroCall.Hold)
                        return context.SelfIsCarrier
                            ? Result(-18f, "carrier_hold")
                            : Result(8f, "zone_hold");
                    break;
            }

            return AIMacroActionPolicyResult.None;
        }

        private static AIMacroActionPolicyResult EvaluateCarrierSafety(
            AIMacroActionContext context)
        {
            if (context.SelfCarriedGems <= 0)
                return AIMacroActionPolicyResult.None;

            if (context.MacroState.Call == AIGameModeMacroCall.Hold)
            {
                if (context.MacroState.OwnTeamHasCountdown)
                {
                    float urgency = GetCountdownUrgency(context.MacroState);
                    return Result(
                        (12f + urgency * 4f) * context.SelfCarriedGems,
                        "countdown_carrier_hold");
                }

                return Result(8f * context.SelfCarriedGems, "carrier_hold");
            }

            if (context.MacroState.Call == AIGameModeMacroCall.Reset)
                return Result(4f * context.SelfCarriedGems, "carrier_reset");

            return AIMacroActionPolicyResult.None;
        }

        private static AIMacroActionPolicyResult EvaluatePeel(
            AIMacroActionContext context)
        {
            if (context.AllyCarriedGems <= 0 ||
                context.MacroState.Call != AIGameModeMacroCall.Hold)
            {
                return AIMacroActionPolicyResult.None;
            }

            if (context.MacroState.OwnTeamHasCountdown)
            {
                float urgency = GetCountdownUrgency(context.MacroState);
                return Result(
                    (12f + urgency * 4f) * context.AllyCarriedGems,
                    "countdown_protect_carrier");
            }

            return Result(10f * context.AllyCarriedGems, "protect_carrier");
        }

        private static AIMacroActionPolicyResult EvaluateUseSuper(
            AIMacroActionContext context)
        {
            if (context.TargetCarriedGems <= 0)
                return AIMacroActionPolicyResult.None;

            if (context.MacroState.Call == AIGameModeMacroCall.Reset)
            {
                if (context.MacroState.EnemyTeamHasCountdown)
                {
                    float urgency = GetCountdownUrgency(context.MacroState);
                    return Result(
                        (8f + urgency * 2f) * context.TargetCarriedGems,
                        "countdown_carrier_reset");
                }

                return Result(6f * context.TargetCarriedGems, "carrier_reset");
            }

            if (context.MacroState.Call == AIGameModeMacroCall.Push)
                return Result(8f, "carrier_pressure");

            return AIMacroActionPolicyResult.None;
        }

        private static float GetCountdownUrgency(AIGameModeMacroState state)
        {
            if (!state.OwnTeamHasCountdown && !state.EnemyTeamHasCountdown)
                return 0f;

            if (state.WinTimerRemainingSeconds <= 0f)
                return 0.35f;

            return Clamp01((10f - state.WinTimerRemainingSeconds) / 10f);
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;

            return value >= 1f ? 1f : value;
        }

        private static AIMacroActionPolicyResult Result(float delta, string reason)
        {
            return new AIMacroActionPolicyResult(delta, reason);
        }
    }
}
