using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class AILaneDisciplineUtility
    {
        public static AITeamLaneAssignment ResolveAssignedLane(int entityId)
        {
            int lane = (entityId & 0x7fffffff) % 3;
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

        public static bool TryResolveLaneHoldPoint(
            BrawlerController self,
            BrawlerAIProfile profile,
            AITeamLaneAssignment lane,
            Vector3 anchorPoint,
            out Vector3 lanePoint,
            out string reason)
        {
            lanePoint = anchorPoint;
            reason = "lane_disabled";

            if (self == null || profile == null || !profile.UseLaneDiscipline)
                return false;

            AITeamLaneAssignment effectiveLane = lane == AITeamLaneAssignment.None
                ? ResolveAssignedLane(self.EntityID)
                : lane;
            AITeamLaneAssignment mapLane = ResolveMapLane(effectiveLane, self.EntityID);

            Vector3 desiredPoint = GetProceduralLanePoint(
                self.Team,
                effectiveLane,
                self.EntityID,
                anchorPoint,
                Mathf.Max(0.5f, profile.LaneSideOffset),
                profile.LaneForwardOffset);

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
            {
                lanePoint = desiredPoint;
                reason = $"lane_{effectiveLane}_procedural_no_map";
                return true;
            }

            Vector2Int desiredCoords = pathfinder.GetGridCoords(desiredPoint);
            int searchRadius = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    Mathf.Max(pathfinder.CellSize, profile.LaneHoldSearchRadius) /
                    Mathf.Max(0.1f, pathfinder.CellSize)));

            if (TryFindBestSemanticLanePoint(
                    pathfinder,
                    desiredCoords,
                    mapLane,
                    searchRadius,
                    out Vector2Int semanticCoords,
                    out string semanticReason))
            {
                lanePoint = pathfinder.GetWorldPos(semanticCoords);
                reason = $"lane_{effectiveLane}_{semanticReason}";
                return true;
            }

            if (pathfinder.IsWalkable(desiredCoords))
            {
                lanePoint = pathfinder.GetWorldPos(desiredCoords);
                reason = $"lane_{effectiveLane}_procedural";
                return true;
            }

            if (pathfinder.TryGetNearestWalkableCoords(
                    desiredCoords,
                    searchRadius,
                    out Vector2Int nearestCoords))
            {
                lanePoint = pathfinder.GetWorldPos(nearestCoords);
                reason = $"lane_{effectiveLane}_nearest";
                return true;
            }

            reason = $"lane_{effectiveLane}_unavailable";
            return false;
        }

        public static Vector3 GetProceduralLanePoint(
            TeamType team,
            AITeamLaneAssignment lane,
            int entityId,
            Vector3 anchorPoint,
            float sideOffset,
            float forwardOffset)
        {
            Vector3 teamForward = GetTeamForward(team);
            Vector3 teamRight = new Vector3(teamForward.z, 0f, -teamForward.x);
            AITeamLaneAssignment mapLane = ResolveMapLane(lane, entityId);

            float sideSign = 0f;
            if (mapLane == AITeamLaneAssignment.Left)
                sideSign = -1f;
            else if (mapLane == AITeamLaneAssignment.Right)
                sideSign = 1f;

            float laneForward = forwardOffset;
            if (lane == AITeamLaneAssignment.Anchor ||
                lane == AITeamLaneAssignment.Escort ||
                lane == AITeamLaneAssignment.Bait)
            {
                laneForward = -forwardOffset;
            }
            else if (lane == AITeamLaneAssignment.Flank)
            {
                sideSign = sideSign == 0f ? GetStableSideSign(entityId) : sideSign;
                laneForward = forwardOffset * 0.75f;
            }

            return anchorPoint +
                   teamRight * sideSign * Mathf.Max(0f, sideOffset) +
                   teamForward * laneForward;
        }

        public static AITeamLaneAssignment ResolveMapLane(
            AITeamLaneAssignment lane,
            int entityId)
        {
            switch (lane)
            {
                case AITeamLaneAssignment.Left:
                case AITeamLaneAssignment.Mid:
                case AITeamLaneAssignment.Right:
                    return lane;

                case AITeamLaneAssignment.Flank:
                    return GetStableSideSign(entityId) < 0
                        ? AITeamLaneAssignment.Left
                        : AITeamLaneAssignment.Right;

                case AITeamLaneAssignment.Anchor:
                case AITeamLaneAssignment.Escort:
                case AITeamLaneAssignment.Bait:
                    return AITeamLaneAssignment.Mid;

                default:
                    return ResolveAssignedLane(entityId);
            }
        }

        public static Vector3 GetTeamForward(TeamType team)
        {
            return team == TeamType.Blue ? Vector3.forward : Vector3.back;
        }

        private static bool TryFindBestSemanticLanePoint(
            AStarSolver pathfinder,
            Vector2Int desiredCoords,
            AITeamLaneAssignment lane,
            int searchRadius,
            out Vector2Int bestCoords,
            out string reason)
        {
            bestCoords = desiredCoords;
            reason = "semantic_none";

            bool found = false;
            float bestScore = float.MinValue;
            int radius = Mathf.Max(1, searchRadius);
            int radiusSq = radius * radius;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int distSq = x * x + y * y;
                    if (distSq > radiusSq)
                        continue;

                    Vector2Int coords = desiredCoords + new Vector2Int(x, y);
                    if (!pathfinder.IsWalkable(coords))
                        continue;

                    AIMapSemanticCell cell = pathfinder.GetSemanticCell(coords);
                    bool laneMatch = cell.Lane == lane;
                    bool laneTagged = cell.HasTag(AIMapSemanticTag.Lane);
                    if (!laneMatch && !laneTagged)
                        continue;

                    float score = 0f;
                    if (laneMatch)
                        score += 45f;

                    if (laneTagged)
                        score += 15f;

                    score += cell.Influence * 10f;
                    score -= Mathf.Sqrt(distSq) * 3f;

                    if (cell.HasTag(AIMapSemanticTag.DangerCorridor))
                        score -= 22f;

                    if (cell.HasTag(AIMapSemanticTag.Choke))
                        score -= 6f;

                    if (!found || score > bestScore)
                    {
                        found = true;
                        bestScore = score;
                        bestCoords = coords;
                    }
                }
            }

            if (found)
                reason = $"semantic_{lane}";

            return found;
        }

        private static int GetStableSideSign(int entityId)
        {
            return (entityId & 1) == 0 ? 1 : -1;
        }
    }
}
