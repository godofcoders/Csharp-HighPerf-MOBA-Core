using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AITeamLaneOwnershipTracker
    {
        private const int LaneCount = 3;

        private struct LaneRecord
        {
            public AITeamLaneAssignment Lane;
            public Vector3 Position;
            public uint Tick;
        }

        private readonly int[] _laneCounts = new int[LaneCount];
        private readonly Dictionary<int, LaneRecord> _botToLane =
            new Dictionary<int, LaneRecord>(8);
        private readonly List<int> _staleBotBuffer = new List<int>(8);

        public void ReportLane(
            int botEntityId,
            AITeamLaneAssignment lane,
            Vector3 position,
            uint currentTick)
        {
            if (botEntityId == 0)
                return;

            ClearLane(botEntityId);

            AITeamLaneAssignment mapLane = AILaneDisciplineUtility.ResolveMapLane(
                lane,
                botEntityId);
            if (!TryGetLaneIndex(mapLane, out int laneIndex))
                return;

            _botToLane[botEntityId] = new LaneRecord
            {
                Lane = mapLane,
                Position = position,
                Tick = currentTick
            };
            _laneCounts[laneIndex]++;
        }

        public void ClearLane(int botEntityId)
        {
            if (botEntityId == 0)
                return;

            if (!_botToLane.TryGetValue(botEntityId, out LaneRecord previousRecord))
                return;

            _botToLane.Remove(botEntityId);

            if (!TryGetLaneIndex(previousRecord.Lane, out int laneIndex))
                return;

            _laneCounts[laneIndex] = Mathf.Max(0, _laneCounts[laneIndex] - 1);
        }

        public AITeamLaneOwnershipSnapshot GetSnapshot(
            int botEntityId,
            uint currentTick,
            uint maxAgeTicks)
        {
            if (botEntityId == 0)
                return AITeamLaneOwnershipSnapshot.None(botEntityId, currentTick);

            PurgeStale(currentTick, maxAgeTicks);

            AITeamLaneAssignment assignedLane =
                AILaneDisciplineUtility.ResolveAssignedLane(botEntityId);
            AITeamLaneAssignment currentLane = _botToLane.TryGetValue(
                botEntityId,
                out LaneRecord currentRecord)
                ? currentRecord.Lane
                : assignedLane;

            int[] counts = BuildVirtualCounts(botEntityId, currentLane);
            AITeamLaneAssignment underOwnedLane = SelectUnderOwnedLane(
                botEntityId,
                currentLane,
                counts);
            AITeamLaneAssignment overOwnedLane = SelectOverOwnedLane(counts);

            int currentCount = GetLaneCount(counts, currentLane);
            int assignedCount = GetLaneCount(counts, assignedLane);
            bool currentLaneOverOwned =
                currentCount > 1 &&
                underOwnedLane != AITeamLaneAssignment.None &&
                GetLaneCount(counts, underOwnedLane) < currentCount;
            bool assignedLaneAbandoned =
                assignedLane != currentLane &&
                assignedCount <= 0;
            bool shouldRotate =
                currentLaneOverOwned &&
                !IsStableAnchorForLane(botEntityId, currentLane);

            AITeamLaneAssignment recommendedLane = currentLane;
            string reason = "stable";

            if (assignedLaneAbandoned)
            {
                recommendedLane = assignedLane;
                reason = "recover_assigned";
            }
            else if (shouldRotate)
            {
                recommendedLane = underOwnedLane;
                reason = "rebalance_underowned";
            }
            else if (currentLaneOverOwned)
            {
                reason = "anchor_overowned";
            }

            return new AITeamLaneOwnershipSnapshot(
                botEntityId,
                currentTick,
                assignedLane,
                currentLane,
                recommendedLane,
                underOwnedLane,
                overOwnedLane,
                counts[0],
                counts[1],
                counts[2],
                assignedLaneAbandoned,
                currentLaneOverOwned,
                shouldRotate,
                reason);
        }

        public void Clear()
        {
            for (int i = 0; i < _laneCounts.Length; i++)
            {
                _laneCounts[i] = 0;
            }

            _botToLane.Clear();
            _staleBotBuffer.Clear();
        }

        private int[] BuildVirtualCounts(
            int botEntityId,
            AITeamLaneAssignment currentLane)
        {
            int[] counts =
            {
                _laneCounts[0],
                _laneCounts[1],
                _laneCounts[2]
            };

            if (!_botToLane.ContainsKey(botEntityId) &&
                TryGetLaneIndex(currentLane, out int laneIndex))
            {
                counts[laneIndex]++;
            }

            return counts;
        }

        private AITeamLaneAssignment SelectUnderOwnedLane(
            int botEntityId,
            AITeamLaneAssignment currentLane,
            int[] counts)
        {
            int currentCount = GetLaneCount(counts, currentLane);
            if (currentCount <= 1)
                return AITeamLaneAssignment.None;

            AITeamLaneAssignment bestLane = AITeamLaneAssignment.None;
            int bestCount = int.MaxValue;
            int bestStableScore = int.MaxValue;

            for (int i = 0; i < LaneCount; i++)
            {
                AITeamLaneAssignment lane = GetLaneByIndex(i);
                if (lane == currentLane || counts[i] >= currentCount)
                    continue;

                int stableScore = GetStableLaneScore(botEntityId, lane);
                if (counts[i] < bestCount ||
                    (counts[i] == bestCount && stableScore < bestStableScore))
                {
                    bestLane = lane;
                    bestCount = counts[i];
                    bestStableScore = stableScore;
                }
            }

            return bestLane;
        }

        private static AITeamLaneAssignment SelectOverOwnedLane(int[] counts)
        {
            int bestIndex = -1;
            int bestCount = 1;

            for (int i = 0; i < LaneCount; i++)
            {
                if (counts[i] > bestCount)
                {
                    bestIndex = i;
                    bestCount = counts[i];
                }
            }

            return bestIndex >= 0
                ? GetLaneByIndex(bestIndex)
                : AITeamLaneAssignment.None;
        }

        private bool IsStableAnchorForLane(
            int botEntityId,
            AITeamLaneAssignment lane)
        {
            int anchorId = botEntityId;
            bool found = false;

            foreach (var pair in _botToLane)
            {
                if (pair.Value.Lane != lane)
                    continue;

                if (!found || pair.Key < anchorId)
                    anchorId = pair.Key;

                found = true;
            }

            return botEntityId == anchorId;
        }

        private void PurgeStale(uint currentTick, uint maxAgeTicks)
        {
            _staleBotBuffer.Clear();

            foreach (var pair in _botToLane)
            {
                if ((currentTick - pair.Value.Tick) > maxAgeTicks)
                    _staleBotBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _staleBotBuffer.Count; i++)
            {
                ClearLane(_staleBotBuffer[i]);
            }
        }

        private static int GetLaneCount(
            int[] counts,
            AITeamLaneAssignment lane)
        {
            return TryGetLaneIndex(lane, out int laneIndex)
                ? counts[laneIndex]
                : 0;
        }

        private static int GetStableLaneScore(
            int botEntityId,
            AITeamLaneAssignment lane)
        {
            unchecked
            {
                int value = botEntityId * 397 ^ (int)lane * 101;
                return value & 0x7fffffff;
            }
        }

        private static bool TryGetLaneIndex(
            AITeamLaneAssignment lane,
            out int index)
        {
            switch (lane)
            {
                case AITeamLaneAssignment.Left:
                    index = 0;
                    return true;

                case AITeamLaneAssignment.Mid:
                    index = 1;
                    return true;

                case AITeamLaneAssignment.Right:
                    index = 2;
                    return true;

                default:
                    index = -1;
                    return false;
            }
        }

        private static AITeamLaneAssignment GetLaneByIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return AITeamLaneAssignment.Left;

                case 1:
                    return AITeamLaneAssignment.Mid;

                case 2:
                    return AITeamLaneAssignment.Right;

                default:
                    return AITeamLaneAssignment.None;
            }
        }
    }
}
