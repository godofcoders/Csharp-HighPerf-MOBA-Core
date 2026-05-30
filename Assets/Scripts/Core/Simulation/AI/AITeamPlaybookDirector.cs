using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public enum AITeamPlaybookCall
    {
        None,
        Push,
        Hold,
        Reset,
        EscortCarrier,
        PinchPressure,
        BaitAndCollapse
    }

    public enum AITeamLaneAssignment
    {
        None,
        Left,
        Mid,
        Right,
        Escort,
        Anchor,
        Flank,
        Bait
    }

    public struct AITeamPlaybookContext
    {
        public int BotEntityId;
        public uint Tick;
        public Vector3 SelfPosition;
        public float HealthRatio;
        public AIGameModeMacroState MacroState;

        public bool SelfIsCarrier;
        public int SelfCarriedGems;

        public bool HasAllyCarrier;
        public int AllyCarrierEntityId;
        public int AllyCarrierGemCount;
        public Vector3 AllyCarrierPosition;

        public bool HasAllyUnderThreat;
        public bool SelfIsThreatenedAlly;
        public int ThreatenedAllyEntityId;
        public Vector3 ThreatenedAllyPosition;

        public bool HasFocusTarget;
        public int FocusTargetEntityId;
        public Vector3 FocusTargetPosition;

        public bool HasEnemyHotspot;
        public Vector3 EnemyHotspotPosition;
        public float EnemyHotspotPressure;

        public bool HasThreatCenter;
        public Vector3 ThreatCenterPosition;
        public float ThreatCenterPressure;

        public int ApproachAllies;
        public int HoldAllies;
        public int RepositionAllies;
        public int PeelAllies;
        public int RegroupAllies;
        public int ObjectiveAllies;
    }

    public readonly struct AITeamPlaybookState
    {
        public readonly AITeamPlaybookCall Call;
        public readonly AITeamLaneAssignment Lane;
        public readonly Vector3 AnchorPoint;
        public readonly Vector3 PressurePoint;
        public readonly Vector3 EscortTargetPoint;
        public readonly int CarrierEntityId;
        public readonly int FocusTargetEntityId;
        public readonly float Urgency;
        public readonly uint Tick;
        public readonly bool HasAnchorPoint;
        public readonly bool HasPressurePoint;
        public readonly bool HasEscortTargetPoint;
        public readonly string Reason;

        public bool IsActive => Call != AITeamPlaybookCall.None;

        public AITeamPlaybookState(
            AITeamPlaybookCall call,
            AITeamLaneAssignment lane,
            Vector3 anchorPoint,
            Vector3 pressurePoint,
            Vector3 escortTargetPoint,
            int carrierEntityId,
            int focusTargetEntityId,
            float urgency,
            uint tick,
            bool hasAnchorPoint,
            bool hasPressurePoint,
            bool hasEscortTargetPoint,
            string reason)
        {
            Call = call;
            Lane = lane;
            AnchorPoint = anchorPoint;
            PressurePoint = pressurePoint;
            EscortTargetPoint = escortTargetPoint;
            CarrierEntityId = carrierEntityId;
            FocusTargetEntityId = focusTargetEntityId;
            Urgency = urgency;
            Tick = tick;
            HasAnchorPoint = hasAnchorPoint;
            HasPressurePoint = hasPressurePoint;
            HasEscortTargetPoint = hasEscortTargetPoint;
            Reason = reason;
        }

        public static AITeamPlaybookState None(uint tick)
        {
            return new AITeamPlaybookState(
                AITeamPlaybookCall.None,
                AITeamLaneAssignment.None,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                0,
                0,
                0f,
                tick,
                false,
                false,
                false,
                "none");
        }

        public string GetDebugSummary()
        {
            if (!IsActive)
                return "Playbook=None";

            return
                $"Playbook={Call} " +
                $"Lane={Lane} " +
                $"Urg={Urgency:0.00} " +
                $"Carrier={CarrierEntityId} " +
                $"Focus={FocusTargetEntityId} " +
                $"Reason={Reason}";
        }
    }

    public static class AITeamPlaybookDirector
    {
        public static AITeamPlaybookState Resolve(AITeamPlaybookContext context)
        {
            AITeamPlaybookCall call = ResolveCall(context, out string reason);
            if (call == AITeamPlaybookCall.None)
                return AITeamPlaybookState.None(context.Tick);

            AITeamLaneAssignment lane = ResolveLane(call, context);
            Vector3 anchorPoint = ResolveAnchorPoint(call, context, out bool hasAnchorPoint);
            Vector3 pressurePoint = ResolvePressurePoint(context, out bool hasPressurePoint);
            Vector3 escortTargetPoint = ResolveEscortTargetPoint(context, out bool hasEscortTargetPoint);
            int carrierEntityId = ResolveCarrierEntityId(context);
            float urgency = ResolveUrgency(call, context);

            return new AITeamPlaybookState(
                call,
                lane,
                anchorPoint,
                pressurePoint,
                escortTargetPoint,
                carrierEntityId,
                context.FocusTargetEntityId,
                urgency,
                context.Tick,
                hasAnchorPoint,
                hasPressurePoint,
                hasEscortTargetPoint,
                reason);
        }

        private static AITeamPlaybookCall ResolveCall(
            AITeamPlaybookContext context,
            out string reason)
        {
            AIGameModeMacroCall macroCall = context.MacroState.Call;
            bool hasCarrier = context.SelfIsCarrier || context.HasAllyCarrier;
            int carrierGems = Mathf.Max(context.SelfCarriedGems, context.AllyCarrierGemCount);
            int carrierProtectionThreshold = context.MacroState.GemsToWin > 0
                ? Mathf.Max(3, context.MacroState.GemsToWin / 3)
                : 3;
            float pressure = GetTeamPressure(context);

            if (macroCall == AIGameModeMacroCall.Reset)
            {
                if (context.MacroState.Mode == GameModeId.Knockout)
                {
                    reason = "knockout_stabilize";
                    return AITeamPlaybookCall.Hold;
                }

                reason = "macro_reset";
                return AITeamPlaybookCall.Reset;
            }

            if (hasCarrier &&
                (macroCall == AIGameModeMacroCall.Hold ||
                 context.MacroState.OwnTeamHasCountdown ||
                 carrierGems >= carrierProtectionThreshold))
            {
                reason = "carrier_protection";
                return AITeamPlaybookCall.EscortCarrier;
            }

            if (context.HasAllyUnderThreat &&
                (context.HasThreatCenter || context.HasEnemyHotspot) &&
                pressure >= 1f)
            {
                reason = "threatened_ally";
                return AITeamPlaybookCall.BaitAndCollapse;
            }

            if (context.HasFocusTarget &&
                (macroCall == AIGameModeMacroCall.Push ||
                 pressure >= 1.25f ||
                 context.ApproachAllies > 0 ||
                 context.RepositionAllies > 0))
            {
                reason = "focus_pinch";
                return AITeamPlaybookCall.PinchPressure;
            }

            if (macroCall == AIGameModeMacroCall.Push)
            {
                reason = "macro_push";
                return AITeamPlaybookCall.Push;
            }

            if (macroCall == AIGameModeMacroCall.Hold)
            {
                reason = "macro_hold";
                return AITeamPlaybookCall.Hold;
            }

            reason = "none";
            return AITeamPlaybookCall.None;
        }

        private static AITeamLaneAssignment ResolveLane(
            AITeamPlaybookCall call,
            AITeamPlaybookContext context)
        {
            AITeamLaneAssignment baseLane = ResolveBaseLane(context.BotEntityId);

            switch (call)
            {
                case AITeamPlaybookCall.EscortCarrier:
                    if (context.SelfIsCarrier)
                        return AITeamLaneAssignment.Anchor;

                    return AITeamLaneAssignment.Escort;

                case AITeamPlaybookCall.BaitAndCollapse:
                    if (context.SelfIsThreatenedAlly ||
                        context.SelfIsCarrier ||
                        context.HealthRatio <= 0.35f)
                    {
                        return AITeamLaneAssignment.Bait;
                    }

                    return AITeamLaneAssignment.Flank;

                case AITeamPlaybookCall.PinchPressure:
                    if (baseLane == AITeamLaneAssignment.Mid && context.HoldAllies <= 0)
                        return AITeamLaneAssignment.Anchor;

                    return AITeamLaneAssignment.Flank;

                case AITeamPlaybookCall.Reset:
                    if (context.SelfIsCarrier || context.HealthRatio <= 0.35f)
                        return AITeamLaneAssignment.Anchor;

                    return baseLane == AITeamLaneAssignment.Mid
                        ? AITeamLaneAssignment.Mid
                        : AITeamLaneAssignment.Flank;

                case AITeamPlaybookCall.Hold:
                    if (context.SelfIsCarrier)
                        return AITeamLaneAssignment.Anchor;

                    if (context.HasAllyCarrier)
                        return AITeamLaneAssignment.Escort;

                    return baseLane == AITeamLaneAssignment.Mid
                        ? AITeamLaneAssignment.Anchor
                        : baseLane;

                case AITeamPlaybookCall.Push:
                    return baseLane;

                default:
                    return AITeamLaneAssignment.None;
            }
        }

        private static AITeamLaneAssignment ResolveBaseLane(int botEntityId)
        {
            int lane = (botEntityId & 0x7fffffff) % 3;
            switch (lane)
            {
                case 0:
                    return AITeamLaneAssignment.Left;
                case 1:
                    return AITeamLaneAssignment.Mid;
                default:
                    return AITeamLaneAssignment.Right;
            }
        }

        private static Vector3 ResolveAnchorPoint(
            AITeamPlaybookCall call,
            AITeamPlaybookContext context,
            out bool hasAnchorPoint)
        {
            if (call == AITeamPlaybookCall.EscortCarrier)
            {
                hasAnchorPoint = true;
                return context.SelfIsCarrier
                    ? context.SelfPosition
                    : context.AllyCarrierPosition;
            }

            if (call == AITeamPlaybookCall.BaitAndCollapse && context.HasAllyUnderThreat)
            {
                hasAnchorPoint = true;
                return context.ThreatenedAllyPosition;
            }

            hasAnchorPoint = true;
            return context.SelfPosition;
        }

        private static Vector3 ResolvePressurePoint(
            AITeamPlaybookContext context,
            out bool hasPressurePoint)
        {
            if (context.HasFocusTarget)
            {
                hasPressurePoint = true;
                return context.FocusTargetPosition;
            }

            if (context.HasThreatCenter)
            {
                hasPressurePoint = true;
                return context.ThreatCenterPosition;
            }

            if (context.HasEnemyHotspot)
            {
                hasPressurePoint = true;
                return context.EnemyHotspotPosition;
            }

            hasPressurePoint = false;
            return Vector3.zero;
        }

        private static Vector3 ResolveEscortTargetPoint(
            AITeamPlaybookContext context,
            out bool hasEscortTargetPoint)
        {
            if (context.SelfIsCarrier)
            {
                hasEscortTargetPoint = true;
                return context.SelfPosition;
            }

            if (context.HasAllyCarrier)
            {
                hasEscortTargetPoint = true;
                return context.AllyCarrierPosition;
            }

            hasEscortTargetPoint = false;
            return Vector3.zero;
        }

        private static int ResolveCarrierEntityId(AITeamPlaybookContext context)
        {
            if (context.SelfIsCarrier)
                return context.BotEntityId;

            return context.HasAllyCarrier ? context.AllyCarrierEntityId : 0;
        }

        private static float ResolveUrgency(
            AITeamPlaybookCall call,
            AITeamPlaybookContext context)
        {
            float pressure = GetTeamPressure(context);
            int carrierGems = Mathf.Max(context.SelfCarriedGems, context.AllyCarrierGemCount);
            float urgency;

            switch (call)
            {
                case AITeamPlaybookCall.Reset:
                    urgency = 0.95f + pressure * 0.08f;
                    break;

                case AITeamPlaybookCall.EscortCarrier:
                    urgency = 0.75f + carrierGems * 0.08f + pressure * 0.04f;
                    break;

                case AITeamPlaybookCall.BaitAndCollapse:
                    urgency = 0.80f + pressure * 0.08f;
                    break;

                case AITeamPlaybookCall.PinchPressure:
                    urgency = 0.70f + pressure * 0.06f;
                    break;

                case AITeamPlaybookCall.Push:
                    urgency = 0.65f;
                    break;

                case AITeamPlaybookCall.Hold:
                    urgency = 0.60f + carrierGems * 0.04f;
                    break;

                default:
                    urgency = 0f;
                    break;
            }

            return Mathf.Clamp(urgency, 0f, 1.35f);
        }

        private static float GetTeamPressure(AITeamPlaybookContext context)
        {
            return Mathf.Max(
                context.HasThreatCenter ? context.ThreatCenterPressure : 0f,
                context.HasEnemyHotspot ? context.EnemyHotspotPressure : 0f);
        }
    }
}
