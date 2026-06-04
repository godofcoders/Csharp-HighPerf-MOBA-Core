using UnityEngine;
using MOBA.Core.Definitions;
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

    public enum AITeamEscortFormationRole
    {
        None,
        CarrierAnchor,
        Shadow,
        Screen,
        PressureFlank
    }

    public struct AITeamPlaybookContext
    {
        public int BotEntityId;
        public uint Tick;
        public Vector3 SelfPosition;
        public BrawlerArchetype Archetype;
        public float HealthRatio;
        public AIGameModeMacroState MacroState;
        public bool HasLaneOwnership;
        public AITeamLaneOwnershipSnapshot LaneOwnership;

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
        public readonly AITeamEscortFormationRole EscortRole;
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
            AITeamEscortFormationRole escortRole,
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
            EscortRole = escortRole;
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
                AITeamEscortFormationRole.None,
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
                $"EscortRole={EscortRole} " +
                $"Reason={Reason}";
        }
    }

    public static class AITeamPlaybookDirector
    {
        private const float ShadowEscortDistance = 1.35f;
        private const float ScreenEscortDistance = 1.85f;
        private const float FlankEscortDistance = 1.70f;
        private const float FlankEscortSideOffset = 1.55f;

        public static AITeamPlaybookState Resolve(AITeamPlaybookContext context)
        {
            AITeamPlaybookCall call = ResolveCall(context, out string reason);
            if (call == AITeamPlaybookCall.None)
                return AITeamPlaybookState.None(context.Tick);

            AITeamLaneAssignment lane = ResolveLane(call, context);
            Vector3 anchorPoint = ResolveAnchorPoint(call, context, out bool hasAnchorPoint);
            Vector3 pressurePoint = ResolvePressurePoint(context, out bool hasPressurePoint);
            AITeamEscortFormationRole escortRole = ResolveEscortRole(call, lane, context);
            Vector3 escortTargetPoint = ResolveEscortTargetPoint(
                context,
                escortRole,
                pressurePoint,
                hasPressurePoint,
                out bool hasEscortTargetPoint);
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
                escortRole,
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
            AITeamLaneAssignment baseLane = ResolveBaseLane(context);

            switch (call)
            {
                case AITeamPlaybookCall.EscortCarrier:
                    if (context.SelfIsCarrier)
                        return AITeamLaneAssignment.Anchor;

                    if (ShouldScreenCarrier(context) ||
                        ShouldFlankFromCarrier(context))
                    {
                        return AITeamLaneAssignment.Flank;
                    }

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

        private static AITeamLaneAssignment ResolveBaseLane(
            AITeamPlaybookContext context)
        {
            if (context.HasLaneOwnership &&
                context.LaneOwnership.HasRecommendedLane)
            {
                return context.LaneOwnership.RecommendedLane;
            }

            return ResolveStableBaseLane(context.BotEntityId);
        }

        private static AITeamLaneAssignment ResolveStableBaseLane(int botEntityId)
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
            AITeamEscortFormationRole escortRole,
            Vector3 pressurePoint,
            bool hasPressurePoint,
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
                return ResolveAllyCarrierFormationPoint(
                    context,
                    escortRole,
                    pressurePoint,
                    hasPressurePoint);
            }

            hasEscortTargetPoint = false;
            return Vector3.zero;
        }

        private static AITeamEscortFormationRole ResolveEscortRole(
            AITeamPlaybookCall call,
            AITeamLaneAssignment lane,
            AITeamPlaybookContext context)
        {
            if (call != AITeamPlaybookCall.EscortCarrier)
                return AITeamEscortFormationRole.None;

            if (context.SelfIsCarrier)
                return AITeamEscortFormationRole.CarrierAnchor;

            if (!context.HasAllyCarrier)
                return AITeamEscortFormationRole.None;

            if (ShouldScreenCarrier(context))
                return AITeamEscortFormationRole.Screen;

            if (ShouldFlankFromCarrier(context) || lane == AITeamLaneAssignment.Flank)
                return AITeamEscortFormationRole.PressureFlank;

            return AITeamEscortFormationRole.Shadow;
        }

        private static bool ShouldScreenCarrier(AITeamPlaybookContext context)
        {
            if (!HasCarrierPressure(context))
                return false;

            return context.Archetype == BrawlerArchetype.Tank ||
                   context.Archetype == BrawlerArchetype.Fighter ||
                   context.Archetype == BrawlerArchetype.Controller ||
                   context.Archetype == BrawlerArchetype.Support;
        }

        private static bool ShouldFlankFromCarrier(AITeamPlaybookContext context)
        {
            if (!HasCarrierPressure(context))
                return false;

            return context.Archetype == BrawlerArchetype.Assassin ||
                   context.Archetype == BrawlerArchetype.Sniper ||
                   context.Archetype == BrawlerArchetype.Artillery;
        }

        private static bool HasCarrierPressure(AITeamPlaybookContext context)
        {
            return context.HasThreatCenter ||
                   context.HasEnemyHotspot ||
                   context.MacroState.EnemyTeamHasCountdown;
        }

        private static Vector3 ResolveAllyCarrierFormationPoint(
            AITeamPlaybookContext context,
            AITeamEscortFormationRole escortRole,
            Vector3 pressurePoint,
            bool hasPressurePoint)
        {
            Vector3 carrier = context.AllyCarrierPosition;
            Vector3 toPressure = ResolveCarrierPressureDirection(
                carrier,
                pressurePoint,
                hasPressurePoint,
                context.BotEntityId);
            Vector3 side = new Vector3(toPressure.z, 0f, -toPressure.x);
            if ((context.BotEntityId & 1) != 0)
                side = -side;

            switch (escortRole)
            {
                case AITeamEscortFormationRole.Screen:
                    return carrier + toPressure * ScreenEscortDistance;

                case AITeamEscortFormationRole.PressureFlank:
                    return carrier +
                           toPressure * FlankEscortDistance +
                           side * FlankEscortSideOffset;

                case AITeamEscortFormationRole.Shadow:
                default:
                    return carrier - toPressure * ShadowEscortDistance;
            }
        }

        private static Vector3 ResolveCarrierPressureDirection(
            Vector3 carrier,
            Vector3 pressurePoint,
            bool hasPressurePoint,
            int botEntityId)
        {
            Vector3 direction = hasPressurePoint
                ? pressurePoint - carrier
                : Vector3.zero;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
                return direction.normalized;

            int lane = (botEntityId & 0x7fffffff) % 3;
            switch (lane)
            {
                case 0:
                    return Vector3.left;

                case 2:
                    return Vector3.right;

                default:
                    return Vector3.forward;
            }
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
