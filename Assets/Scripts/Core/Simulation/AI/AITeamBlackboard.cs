using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public static class AITeamBlackboard
    {
        private struct TeamData
        {
            public BrawlerController FocusTarget;
            public uint FocusTargetTick;

            public Vector3 RegroupPoint;
            public uint RegroupTick;

            public BrawlerController AllyUnderThreat;
            public uint PeelTick;

            public Dictionary<int, int> FocusCounts;
            public Dictionary<int, int> BotFocusTargets;
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

        public static void ReportRegroupPoint(TeamType team, Vector3 point, uint currentTick)
        {
            ref TeamData data = ref GetData(team);
            data.RegroupPoint = point;
            data.RegroupTick = currentTick;
        }

        public static bool TryGetRegroupPoint(TeamType team, uint currentTick, uint maxAgeTicks, out Vector3 point)
        {
            ref TeamData data = ref GetData(team);

            if ((currentTick - data.RegroupTick) <= maxAgeTicks)
            {
                point = data.RegroupPoint;
                return true;
            }

            point = default;
            return false;
        }

        public static void ReportAllyUnderThreat(TeamType team, BrawlerController ally, uint currentTick)
        {
            ref TeamData data = ref GetData(team);
            data.AllyUnderThreat = ally;
            data.PeelTick = currentTick;
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

        public static void ReportTargetFocusCount(
            TeamType team,
            int botEntityId,
            int targetEntityId)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureFocusMaps(ref data);

            ClearTargetFocusCountInternal(ref data, botEntityId);

            if (targetEntityId == 0)
                return;

            data.BotFocusTargets[botEntityId] = targetEntityId;

            if (!data.FocusCounts.ContainsKey(targetEntityId))
                data.FocusCounts[targetEntityId] = 0;

            data.FocusCounts[targetEntityId]++;
        }

        public static void ClearTargetFocusCount(TeamType team, int botEntityId)
        {
            if (botEntityId == 0)
                return;

            ref TeamData data = ref GetData(team);
            EnsureFocusMaps(ref data);

            ClearTargetFocusCountInternal(ref data, botEntityId);
        }

        public static int GetTargetFocusCount(TeamType team, int targetEntityId)
        {
            if (targetEntityId == 0)
                return 0;

            ref TeamData data = ref GetData(team);
            EnsureFocusMaps(ref data);

            return data.FocusCounts.TryGetValue(targetEntityId, out int count)
                ? count
                : 0;
        }

        public static void ClearTeamFocusCounts(TeamType team)
        {
            ref TeamData data = ref GetData(team);
            EnsureFocusMaps(ref data);

            data.FocusCounts.Clear();
            data.BotFocusTargets.Clear();
        }

        private static void EnsureFocusMaps(ref TeamData data)
        {
            if (data.FocusCounts == null)
                data.FocusCounts = new Dictionary<int, int>(16);

            if (data.BotFocusTargets == null)
                data.BotFocusTargets = new Dictionary<int, int>(16);
        }

        private static void ClearTargetFocusCountInternal(ref TeamData data, int botEntityId)
        {
            if (data.BotFocusTargets == null || data.FocusCounts == null)
                return;

            if (!data.BotFocusTargets.TryGetValue(botEntityId, out int previousTargetId))
                return;

            data.BotFocusTargets.Remove(botEntityId);

            if (!data.FocusCounts.TryGetValue(previousTargetId, out int count))
                return;

            count--;

            if (count <= 0)
                data.FocusCounts.Remove(previousTargetId);
            else
                data.FocusCounts[previousTargetId] = count;
        }

        private static ref TeamData GetData(TeamType team)
        {
            if (team == TeamType.Blue)
                return ref _blue;

            return ref _red;
        }
    }
}