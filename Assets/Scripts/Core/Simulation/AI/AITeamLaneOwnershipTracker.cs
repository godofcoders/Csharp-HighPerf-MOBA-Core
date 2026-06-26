using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AITeamLaneOwnershipTracker
    {
        private const int LaneCount = 3;
        private const uint RotationConfirmTicks = 14;
        private const uint MidRotationExtraConfirmTicks = 8;
        private const uint RotationCooldownTicks = 42;

        private struct LaneRecord
        {
            public AITeamLaneAssignment Lane;
            public Vector3 Position;
            public uint Tick;
        }

        private struct RotationRecord
        {
            public AITeamLaneAssignment CandidateLane;
            public uint CandidateFirstSeenTick;
            public AITeamLaneAssignment LastCommittedLane;
            public uint LastCommitTick;
        }

        private readonly int[] _laneCounts = new int[LaneCount];
        private readonly Dictionary<int, LaneRecord> _botToLane =
            new Dictionary<int, LaneRecord>(8);
        private readonly Dictionary<int, RotationRecord> _rotationRecords =
            new Dictionary<int, RotationRecord>(8);
        private readonly List<int> _staleBotBuffer = new List<int>(8);

        public void ReportLane(
            int botEntityId,
            AITeamLaneAssignment lane,
            Vector3 position,
            uint currentTick)
        {
            if (botEntityId == 0)
                return;

            ClearLaneRecord(botEntityId, clearRotation: false);

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
            ClearLaneRecord(botEntityId, clearRotation: true);
        }

        private void ClearLaneRecord(int botEntityId, bool clearRotation)
        {
            if (botEntityId == 0)
                return;

            if (!_botToLane.TryGetValue(botEntityId, out LaneRecord previousRecord))
            {
                if (clearRotation)
                    _rotationRecords.Remove(botEntityId);

                return;
            }

            _botToLane.Remove(botEntityId);

            if (!TryGetLaneIndex(previousRecord.Lane, out int laneIndex))
            {
                if (clearRotation)
                    _rotationRecords.Remove(botEntityId);

                return;
            }

            _laneCounts[laneIndex] = Mathf.Max(0, _laneCounts[laneIndex] - 1);
            if (clearRotation)
                _rotationRecords.Remove(botEntityId);
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
                assignedLane,
                currentLane,
                counts);
            AITeamLaneAssignment overOwnedLane = SelectOverOwnedLane(counts);

            int currentCount = GetLaneCount(counts, currentLane);
            int assignedCount = GetLaneCount(counts, assignedLane);
            int underOwnedCount = GetLaneCount(counts, underOwnedLane);
            bool currentLaneOverOwned =
                currentCount > 1 &&
                underOwnedLane != AITeamLaneAssignment.None &&
                GetLaneCount(counts, underOwnedLane) < currentCount;
            bool assignedLaneAbandoned =
                assignedLane != currentLane &&
                assignedCount <= 0;
            string laneIdentityReason = "stable";
            bool shouldRotate =
                currentLaneOverOwned &&
                !IsStableAnchorForLane(botEntityId, currentLane) &&
                CanRotateFromLaneIdentity(
                    assignedLane,
                    currentLane,
                    currentCount,
                    underOwnedCount,
                    out laneIdentityReason);

            AITeamLaneAssignment recommendedLane = currentLane;
            string reason = "stable";
            bool rotationPending = false;
            uint rotationAgeTicks = 0u;
            uint rotationCooldownRemainingTicks = 0u;

            if (assignedLaneAbandoned)
            {
                recommendedLane = assignedLane;
                reason = "recover_assigned";
                _rotationRecords.Remove(botEntityId);
            }
            else if (shouldRotate)
            {
                if (TryConfirmRotation(
                        botEntityId,
                        currentLane,
                        underOwnedLane,
                        currentTick,
                        out rotationPending,
                        out rotationAgeTicks,
                        out rotationCooldownRemainingTicks,
                        out string rotationReason))
                {
                    recommendedLane = underOwnedLane;
                    reason = rotationReason;
                }
                else
                {
                    shouldRotate = false;
                    reason = rotationReason;
                }
            }
            else if (currentLaneOverOwned)
            {
                reason = IsStableAnchorForLane(botEntityId, currentLane)
                    ? "anchor_overowned"
                    : laneIdentityReason;
                ClearRotationCandidate(botEntityId, currentLane);
            }
            else
            {
                ClearRotationCandidate(botEntityId, currentLane);
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
                rotationPending,
                rotationAgeTicks,
                rotationCooldownRemainingTicks,
                reason);
        }

        public void Clear()
        {
            for (int i = 0; i < _laneCounts.Length; i++)
            {
                _laneCounts[i] = 0;
            }

            _botToLane.Clear();
            _rotationRecords.Clear();
            _staleBotBuffer.Clear();
        }

        private bool TryConfirmRotation(
            int botEntityId,
            AITeamLaneAssignment currentLane,
            AITeamLaneAssignment candidateLane,
            uint currentTick,
            out bool pending,
            out uint ageTicks,
            out uint cooldownRemainingTicks,
            out string reason)
        {
            pending = false;
            ageTicks = 0u;
            cooldownRemainingTicks = 0u;
            reason = "rebalance_underowned";

            if (candidateLane == AITeamLaneAssignment.None ||
                candidateLane == currentLane)
            {
                ClearRotationCandidate(botEntityId, currentLane);
                reason = "stable";
                return false;
            }

            _rotationRecords.TryGetValue(botEntityId, out RotationRecord record);

            if (record.LastCommittedLane != AITeamLaneAssignment.None &&
                currentLane == record.LastCommittedLane)
            {
                record.CandidateLane = AITeamLaneAssignment.None;
                _rotationRecords[botEntityId] = record;
            }

            if (record.LastCommittedLane == candidateLane &&
                currentLane != candidateLane)
            {
                ageTicks = GetRequiredRotationConfirmTicks(currentLane);
                reason = "rebalance_underowned";
                return true;
            }

            if (record.LastCommitTick > 0u)
            {
                uint elapsed = currentTick - record.LastCommitTick;
                if (elapsed < RotationCooldownTicks)
                {
                    cooldownRemainingTicks = RotationCooldownTicks - elapsed;
                    reason = "rotation_cooldown";
                    return false;
                }
            }

            if (record.CandidateLane != candidateLane)
            {
                record.CandidateLane = candidateLane;
                record.CandidateFirstSeenTick = currentTick;
                _rotationRecords[botEntityId] = record;
                pending = true;
                reason = "rebalance_pending";
                return false;
            }

            ageTicks = currentTick - record.CandidateFirstSeenTick;
            uint requiredConfirmTicks = GetRequiredRotationConfirmTicks(currentLane);
            if (ageTicks < requiredConfirmTicks)
            {
                pending = true;
                reason = "rebalance_pending";
                _rotationRecords[botEntityId] = record;
                return false;
            }

            record.LastCommittedLane = candidateLane;
            record.LastCommitTick = currentTick;
            _rotationRecords[botEntityId] = record;
            reason = "rebalance_underowned";
            return true;
        }

        private static bool CanRotateFromLaneIdentity(
            AITeamLaneAssignment assignedLane,
            AITeamLaneAssignment currentLane,
            int currentCount,
            int underOwnedCount,
            out string reason)
        {
            reason = "rebalance_underowned";

            if (currentLane == AITeamLaneAssignment.Mid &&
                currentCount <= 2 &&
                underOwnedCount <= 0)
            {
                reason = "mid_control_hold";
                return false;
            }

            if (assignedLane == currentLane &&
                currentCount <= 2 &&
                underOwnedCount <= 0)
            {
                reason = "assigned_lane_hold";
                return false;
            }

            if (assignedLane == currentLane &&
                currentCount - underOwnedCount < 2)
            {
                reason = "assigned_lane_sticky";
                return false;
            }

            return true;
        }

        private static uint GetRequiredRotationConfirmTicks(
            AITeamLaneAssignment currentLane)
        {
            return currentLane == AITeamLaneAssignment.Mid
                ? RotationConfirmTicks + MidRotationExtraConfirmTicks
                : RotationConfirmTicks;
        }

        private void ClearRotationCandidate(
            int botEntityId,
            AITeamLaneAssignment currentLane)
        {
            if (!_rotationRecords.TryGetValue(botEntityId, out RotationRecord record))
                return;

            if (record.LastCommittedLane != AITeamLaneAssignment.None &&
                currentLane == record.LastCommittedLane)
            {
                record.CandidateLane = AITeamLaneAssignment.None;
                _rotationRecords[botEntityId] = record;
                return;
            }

            if (record.LastCommitTick == 0u)
            {
                _rotationRecords.Remove(botEntityId);
                return;
            }

            record.CandidateLane = AITeamLaneAssignment.None;
            _rotationRecords[botEntityId] = record;
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
            AITeamLaneAssignment assignedLane,
            AITeamLaneAssignment currentLane,
            int[] counts)
        {
            int currentCount = GetLaneCount(counts, currentLane);
            if (currentCount <= 1)
                return AITeamLaneAssignment.None;

            AITeamLaneAssignment bestLane = AITeamLaneAssignment.None;
            int bestCount = int.MaxValue;
            int bestPriority = int.MaxValue;

            for (int i = 0; i < LaneCount; i++)
            {
                AITeamLaneAssignment lane = GetLaneByIndex(i);
                if (lane == currentLane || counts[i] >= currentCount)
                    continue;

                int priority = GetRotationLanePriority(
                    botEntityId,
                    assignedLane,
                    lane,
                    counts[i]);
                if (counts[i] < bestCount ||
                    (counts[i] == bestCount && priority < bestPriority))
                {
                    bestLane = lane;
                    bestCount = counts[i];
                    bestPriority = priority;
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

        private static int GetRotationLanePriority(
            int botEntityId,
            AITeamLaneAssignment assignedLane,
            AITeamLaneAssignment candidateLane,
            int candidateCount)
        {
            int priority = candidateCount * 1000;

            if (candidateLane == assignedLane)
                priority -= 240;

            if (candidateLane == AITeamLaneAssignment.Mid)
                priority -= candidateCount <= 0 ? 120 : 40;

            priority += GetStableLaneScore(botEntityId, candidateLane) % 100;
            return priority;
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
