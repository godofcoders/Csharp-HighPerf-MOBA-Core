using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public static class AITeamBlackboard
    {
        private struct TeamPositionSignal
        {
            public bool HasValue;
            public Vector3 Position;
            public float Weight;
            public uint Tick;
        }

        private struct TeamData
        {
            public BrawlerController FocusTarget;
            public uint FocusTargetTick;

            public Vector3 RegroupPoint;
            public uint RegroupTick;
            public float RegroupUrgency;
            public bool HasRegroupPoint;

            public BrawlerController AllyUnderThreat;
            public uint PeelTick;
            public float PeelUrgency;

            public BrawlerController Carrier;
            public uint CarrierTick;
            public int CarrierGemCount;

            public AITeamPlaybookState PlaybookState;
            public uint PlaybookTick;
            public bool HasPlaybookState;

            public AITeamFocusTracker FocusTracker;
            public AITeamActionTracker ActionTracker;
            public AITeamLaneOwnershipTracker LaneOwnershipTracker;
            public TeamPositionSignal EnemyHotspot;
            public TeamPositionSignal ThreatCenter;
        }

        private static TeamData _blue;
        private static TeamData _red;

        public static void ReportFocusTarget(TeamType team, BrawlerController target, uint currentTick)
        {
            ref TeamData data = ref GetData(team);
            data.FocusTarget = target;
            data.FocusTargetTick = currentTick;
        }

        public static bool TryGetFocusTarget(TeamType team, uint currentTick, uint maxAgeTicks, out BrawlerController target)
        {
            ref TeamData data = ref GetData(team);

            if (data.FocusTarget != null &&
                (currentTick - data.FocusTargetTick) <= maxAgeTicks &&
                data.FocusTarget.State != null &&
                !data.FocusTarget.State.IsDead)
            {
                target = data.FocusTarget;
                return true;
            }

            target = null;
            return false;
        }

        public static void ReportRegroupPoint(
            TeamType team,
            Vector3 point,
            uint currentTick,
            float urgency = 1f)
        {
            ref TeamData data = ref GetData(team);
            float clampedUrgency = Mathf.Max(0.1f, urgency);

            if (data.RegroupTick == 0 || currentTick - data.RegroupTick > 30)
            {
                data.RegroupPoint = point;
                data.RegroupUrgency = clampedUrgency;
            }
            else
            {
                float retainedUrgency = data.RegroupUrgency * 0.65f;
                float combinedUrgency = retainedUrgency + clampedUrgency;

                data.RegroupPoint =
                    ((data.RegroupPoint * retainedUrgency) + (point * clampedUrgency)) /
                    Mathf.Max(0.1f, combinedUrgency);

                data.RegroupUrgency = Mathf.Min(combinedUrgency, 10f);
            }

            data.RegroupTick = currentTick;
            data.HasRegroupPoint = true;
        }

        public static bool TryGetRegroupPoint(TeamType team, uint currentTick, uint maxAgeTicks, out Vector3 point)
        {
            ref TeamData data = ref GetData(team);

            if (data.HasRegroupPoint && (currentTick - data.RegroupTick) <= maxAgeTicks)
            {
                point = data.RegroupPoint;
                return true;
            }

            point = default;
            return false;
        }

        public static void ReportAllyUnderThreat(
            TeamType team,
            BrawlerController ally,
            uint currentTick,
            float urgency = 1f)
        {
            if (ally == null)
                return;

            ref TeamData data = ref GetData(team);
            float clampedUrgency = Mathf.Max(0.1f, urgency);

            if (data.AllyUnderThreat != null &&
                currentTick - data.PeelTick <= 20 &&
                clampedUrgency < data.PeelUrgency * 0.85f)
            {
                return;
            }

            data.AllyUnderThreat = ally;
            data.PeelTick = currentTick;
            data.PeelUrgency = clampedUrgency;
        }

        public static bool TryGetAllyUnderThreat(TeamType team, uint currentTick, uint maxAgeTicks, out BrawlerController ally)
        {
            ref TeamData data = ref GetData(team);

            if (data.AllyUnderThreat != null &&
                (currentTick - data.PeelTick) <= maxAgeTicks &&
                data.AllyUnderThreat.State != null &&
                !data.AllyUnderThreat.State.IsDead)
            {
                ally = data.AllyUnderThreat;
                return true;
            }

            ally = null;
            return false;
        }

        public static void ReportCarrier(
            TeamType team,
            BrawlerController carrier,
            int carriedGemCount,
            uint currentTick)
        {
            if (!SpatialEntityUtility.IsAlive(carrier) || carriedGemCount <= 0)
                return;

            ref TeamData data = ref GetData(team);

            if (SpatialEntityUtility.IsAlive(data.Carrier) &&
                currentTick - data.CarrierTick <= 20 &&
                data.CarrierGemCount > carriedGemCount)
            {
                return;
            }

            data.Carrier = carrier;
            data.CarrierTick = currentTick;
            data.CarrierGemCount = carriedGemCount;
        }

        public static bool TryGetCarrier(
            TeamType team,
            uint currentTick,
            uint maxAgeTicks,
            out BrawlerController carrier,
            out int carriedGemCount)
        {
            ref TeamData data = ref GetData(team);

            if (SpatialEntityUtility.IsAlive(data.Carrier) &&
                currentTick - data.CarrierTick <= maxAgeTicks &&
                data.Carrier.State != null &&
                !data.Carrier.State.IsDead &&
                data.Carrier.State.CarriedGemCount > 0)
            {
                carrier = data.Carrier;
                carriedGemCount = data.Carrier.State.CarriedGemCount;
                return true;
            }

            carrier = null;
            carriedGemCount = 0;
            return false;
        }

        public static void ReportPlaybookState(
            TeamType team,
            AITeamPlaybookState state,
            uint currentTick)
        {
            ref TeamData data = ref GetData(team);
            data.PlaybookState = state;
            data.PlaybookTick = currentTick;
            data.HasPlaybookState = state.IsActive;
        }

        public static bool TryGetPlaybookState(
            TeamType team,
            uint currentTick,
            uint maxAgeTicks,
            out AITeamPlaybookState state)
        {
            ref TeamData data = ref GetData(team);

            if (data.HasPlaybookState && currentTick - data.PlaybookTick <= maxAgeTicks)
            {
                state = data.PlaybookState;
                return true;
            }

            state = AITeamPlaybookState.None(currentTick);
            return false;
        }

        public static void ReportEnemyHotspot(
            TeamType observerTeam,
            Vector3 position,
            uint currentTick,
            float weight = 1f)
        {
            ref TeamData data = ref GetData(observerTeam);
            ReportPositionSignal(ref data.EnemyHotspot, position, currentTick, weight);
        }

        public static bool TryGetEnemyHotspot(
            TeamType team,
            uint currentTick,
            uint maxAgeTicks,
            out Vector3 position,
            out float pressure)
        {
            ref TeamData data = ref GetData(team);
            return TryGetPositionSignal(
                ref data.EnemyHotspot,
                currentTick,
                maxAgeTicks,
                out position,
                out pressure);
        }

        public static void ReportThreatCenter(
            TeamType threatenedTeam,
            Vector3 threatPosition,
            uint currentTick,
            float weight = 1f)
        {
            ref TeamData data = ref GetData(threatenedTeam);
            ReportPositionSignal(ref data.ThreatCenter, threatPosition, currentTick, weight);
        }

        public static bool TryGetThreatCenter(
            TeamType team,
            uint currentTick,
            uint maxAgeTicks,
            out Vector3 position,
            out float pressure)
        {
            ref TeamData data = ref GetData(team);
            return TryGetPositionSignal(
                ref data.ThreatCenter,
                currentTick,
                maxAgeTicks,
                out position,
                out pressure);
        }

        public static void ReportTargetFocusCount(
            TeamType team,
            int botEntityId,
            int targetEntityId)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureFocusTracker(ref data);
            data.FocusTracker.ReportFocus(botEntityId, targetEntityId);
        }

        public static void ClearTargetFocusCount(TeamType team, int botEntityId)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureFocusTracker(ref data);
            data.FocusTracker.ClearFocus(botEntityId);
        }

        public static int GetTargetFocusCount(TeamType team, int targetEntityId)
        {
            if (targetEntityId == 0)
                return 0;

            ref TeamData data = ref GetData(team);
            EnsureFocusTracker(ref data);

            return data.FocusTracker.GetFocusCount(targetEntityId);
        }

        public static int GetTargetFocusCountExcluding(
            TeamType team,
            int targetEntityId,
            int excludedBotEntityId)
        {
            if (targetEntityId == 0)
                return 0;

            ref TeamData data = ref GetData(team);
            EnsureFocusTracker(ref data);

            return data.FocusTracker.GetFocusCountExcluding(
                targetEntityId,
                excludedBotEntityId);
        }

        public static void ClearTeamFocusCounts(TeamType team)
        {
            ref TeamData data = ref GetData(team);
            EnsureFocusTracker(ref data);

            data.FocusTracker.Clear();
        }

        public static void ReportActionIntent(
            TeamType team,
            int botEntityId,
            AIActionType actionType,
            uint currentTick)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureActionTracker(ref data);
            data.ActionTracker.ReportAction(botEntityId, actionType, currentTick);
        }

        public static void ClearActionIntent(TeamType team, int botEntityId)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureActionTracker(ref data);
            data.ActionTracker.ClearAction(botEntityId);
        }

        public static int GetActionIntentCount(
            TeamType team,
            AIActionType actionType,
            uint currentTick,
            uint maxAgeTicks)
        {
            ref TeamData data = ref GetData(team);
            EnsureActionTracker(ref data);

            return data.ActionTracker.GetActionCount(actionType, currentTick, maxAgeTicks);
        }

        public static int GetActionIntentCountExcluding(
            TeamType team,
            AIActionType actionType,
            int excludedBotEntityId,
            uint currentTick,
            uint maxAgeTicks)
        {
            ref TeamData data = ref GetData(team);
            EnsureActionTracker(ref data);

            return data.ActionTracker.GetActionCountExcluding(
                actionType,
                excludedBotEntityId,
                currentTick,
                maxAgeTicks);
        }

        public static void ClearTeamActionIntents(TeamType team)
        {
            ref TeamData data = ref GetData(team);
            EnsureActionTracker(ref data);

            data.ActionTracker.Clear();
        }

        public static void ReportLaneOwnership(
            TeamType team,
            int botEntityId,
            AITeamLaneAssignment lane,
            Vector3 position,
            uint currentTick)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureLaneOwnershipTracker(ref data);
            data.LaneOwnershipTracker.ReportLane(
                botEntityId,
                lane,
                position,
                currentTick);
        }

        public static bool TryGetLaneOwnership(
            TeamType team,
            int botEntityId,
            uint currentTick,
            uint maxAgeTicks,
            out AITeamLaneOwnershipSnapshot snapshot)
        {
            if (botEntityId == 0)
            {
                snapshot = AITeamLaneOwnershipSnapshot.None(botEntityId, currentTick);
                return false;
            }

            ref TeamData data = ref GetData(team);
            EnsureLaneOwnershipTracker(ref data);
            snapshot = data.LaneOwnershipTracker.GetSnapshot(
                botEntityId,
                currentTick,
                maxAgeTicks);

            return snapshot.HasValue;
        }

        public static void ClearLaneOwnership(TeamType team, int botEntityId)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureLaneOwnershipTracker(ref data);
            data.LaneOwnershipTracker.ClearLane(botEntityId);
        }

        public static void ClearTeamLaneOwnership(TeamType team)
        {
            ref TeamData data = ref GetData(team);
            EnsureLaneOwnershipTracker(ref data);
            data.LaneOwnershipTracker.Clear();
        }

        private static void EnsureFocusTracker(ref TeamData data)
        {
            if (data.FocusTracker == null)
                data.FocusTracker = new AITeamFocusTracker();
        }

        private static void EnsureActionTracker(ref TeamData data)
        {
            if (data.ActionTracker == null)
                data.ActionTracker = new AITeamActionTracker();
        }

        private static void EnsureLaneOwnershipTracker(ref TeamData data)
        {
            if (data.LaneOwnershipTracker == null)
                data.LaneOwnershipTracker = new AITeamLaneOwnershipTracker();
        }

        private static void ReportPositionSignal(
            ref TeamPositionSignal signal,
            Vector3 position,
            uint currentTick,
            float weight)
        {
            float clampedWeight = Mathf.Clamp(weight, 0.1f, 6f);

            if (!signal.HasValue || currentTick - signal.Tick > 45)
            {
                signal.HasValue = true;
                signal.Position = position;
                signal.Weight = clampedWeight;
                signal.Tick = currentTick;
                return;
            }

            float retainedWeight = signal.Weight * 0.70f;
            float combinedWeight = retainedWeight + clampedWeight;

            signal.Position =
                ((signal.Position * retainedWeight) + (position * clampedWeight)) /
                Mathf.Max(0.1f, combinedWeight);

            signal.Weight = Mathf.Min(combinedWeight, 12f);
            signal.Tick = currentTick;
        }

        private static bool TryGetPositionSignal(
            ref TeamPositionSignal signal,
            uint currentTick,
            uint maxAgeTicks,
            out Vector3 position,
            out float pressure)
        {
            if (signal.HasValue && currentTick - signal.Tick <= maxAgeTicks)
            {
                position = signal.Position;
                pressure = signal.Weight;
                return true;
            }

            position = default;
            pressure = 0f;
            return false;
        }

        private static ref TeamData GetData(TeamType team)
        {
            if (team == TeamType.Blue)
                return ref _blue;

            return ref _red;
        }
    }
}
