using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public struct AIMapNavigationRequest
    {
        public Vector3 DesiredDestination;
        public AIMapRouteIntent Intent;
        public bool HasThreatPosition;
        public Vector3 ThreatPosition;
        public float PreferredThreatDistance;
        public bool PreferBush;
        public bool PreferCover;
        public bool AvoidChokepoints;
        public bool PreferFlank;
        public float SearchRadius;
        public float BushWeight;
        public float CoverWeight;
        public float ChokepointPenalty;
        public float ThreatWeight;
        public float PathCostWeight;
    }

    public struct AIMapNavigationDecision
    {
        public Vector3 RawDestination;
        public Vector3 ResolvedDestination;
        public AIMapRouteIntent Intent;
        public string Reason;
        public float Score;
        public bool UsedMap;
    }

    public static class AIMapNavigationUtility
    {
        public static Vector3 ResolveDestination(
            BrawlerController self,
            BrawlerAIProfile profile,
            in AIMapNavigationRequest request,
            out AIMapNavigationDecision decision)
        {
            decision = new AIMapNavigationDecision
            {
                RawDestination = request.DesiredDestination,
                ResolvedDestination = request.DesiredDestination,
                Intent = request.Intent,
                Reason = "map_unavailable",
                Score = 0f,
                UsedMap = false
            };

            if (self == null || profile == null || !profile.UseMapIntelligence || SimulationClock.Pathfinder == null)
                return request.DesiredDestination;

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            float searchRadius = Mathf.Max(pathfinder.CellSize, request.SearchRadius);
            int maxOffsetMagnitude = Mathf.Max(1, Mathf.CeilToInt(searchRadius / Mathf.Max(0.1f, pathfinder.CellSize)));
            Vector2Int selfCoords = pathfinder.GetGridCoords(self.Position);
            Vector2Int desiredCoords = pathfinder.GetGridCoords(request.DesiredDestination);

            if (!pathfinder.IsWalkable(selfCoords) &&
                !pathfinder.TryGetNearestWalkableCoords(selfCoords, maxOffsetMagnitude, out selfCoords))
            {
                decision.Reason = "self_not_walkable";
                return request.DesiredDestination;
            }

            Vector3 bestDestination = request.DesiredDestination;
            float bestScore = float.MinValue;
            string bestReason = "no_candidate";
            bool foundCandidate = false;
            int maxOffsetMagnitudeSq = maxOffsetMagnitude * maxOffsetMagnitude;

            for (int x = -maxOffsetMagnitude; x <= maxOffsetMagnitude; x++)
            {
                for (int y = -maxOffsetMagnitude; y <= maxOffsetMagnitude; y++)
                {
                    if ((x * x) + (y * y) > maxOffsetMagnitudeSq)
                        continue;

                    Vector2Int coords = desiredCoords + new Vector2Int(x, y);
                    if (!pathfinder.IsWalkable(coords))
                        continue;

                    Vector3 candidate = pathfinder.GetWorldPos(coords);
                    float score = ScoreCandidate(
                        self,
                        pathfinder,
                        selfCoords,
                        coords,
                        candidate,
                        request,
                        out string reason);

                    if (score == float.MinValue)
                        continue;

                    if (!foundCandidate || score > bestScore)
                    {
                        foundCandidate = true;
                        bestScore = score;
                        bestDestination = candidate;
                        bestReason = reason;
                    }
                }
            }

            if (!foundCandidate)
            {
                bestDestination = pathfinder.GetNearestWalkableWorldPos(
                    request.DesiredDestination,
                    Mathf.Max(1, maxOffsetMagnitude));
                bestReason = "nearest_walkable_fallback";
            }

            decision.ResolvedDestination = bestDestination;
            decision.Reason = bestReason;
            decision.Score = bestScore;
            decision.UsedMap = foundCandidate;

            return bestDestination;
        }

        private static float ScoreCandidate(
            BrawlerController self,
            AStarSolver pathfinder,
            Vector2Int selfCoords,
            Vector2Int coords,
            Vector3 candidate,
            in AIMapNavigationRequest request,
            out string reason)
        {
            float score = 100f;
            reason = "base";

            float desiredDistance = Vector3.Distance(candidate, request.DesiredDestination);
            score -= desiredDistance * 10f;

            List<PathNode> path = null;
            if (request.Intent != AIMapRouteIntent.Evade)
            {
                path = pathfinder.FindPath(selfCoords.x, selfCoords.y, coords.x, coords.y);
            }
            else
            {
                reason += "|fast_evade";
            }

            if (request.Intent != AIMapRouteIntent.Evade &&
                path == null &&
                (candidate - self.Position).sqrMagnitude > pathfinder.CellSize * pathfinder.CellSize)
            {
                reason += "|no_path";
                return float.MinValue;
            }

            int pathLength = path != null ? path.Count : 0;
            score -= pathLength * Mathf.Max(0f, request.PathCostWeight);

            if (request.PreferBush && pathfinder.IsBush(coords))
            {
                score += request.BushWeight;
                reason += "|bush";
            }

            if (request.PreferCover && pathfinder.IsNearObstacle(coords))
            {
                score += request.CoverWeight;
                reason += "|cover";
            }

            if (request.AvoidChokepoints && pathfinder.IsChokepoint(coords))
            {
                score -= request.ChokepointPenalty;
                reason += "|choke";
            }

            if (request.HasThreatPosition)
            {
                ApplyThreatScore(
                    self,
                    candidate,
                    request,
                    ref score,
                    ref reason);
            }

            return score;
        }

        private static void ApplyThreatScore(
            BrawlerController self,
            Vector3 candidate,
            in AIMapNavigationRequest request,
            ref float score,
            ref string reason)
        {
            Vector3 toThreat = request.ThreatPosition - self.Position;
            toThreat.y = 0f;

            Vector3 candidateFromSelf = candidate - self.Position;
            candidateFromSelf.y = 0f;

            float distanceFromThreat = Vector3.Distance(candidate, request.ThreatPosition);
            float preferredDistance = Mathf.Max(0f, request.PreferredThreatDistance);
            float threatWeight = Mathf.Max(0f, request.ThreatWeight);

            switch (request.Intent)
            {
                case AIMapRouteIntent.CombatRetreat:
                case AIMapRouteIntent.Evade:
                    score += distanceFromThreat * threatWeight;
                    if (preferredDistance > 0f && distanceFromThreat < preferredDistance)
                    {
                        score -= (preferredDistance - distanceFromThreat) * threatWeight * 2f;
                        reason += "|too_close_threat";
                    }
                    else
                    {
                        reason += "|threat_space";
                    }
                    break;

                case AIMapRouteIntent.CombatAdvance:
                case AIMapRouteIntent.CombatReposition:
                case AIMapRouteIntent.Peel:
                    if (preferredDistance > 0f)
                    {
                        score -= Mathf.Abs(distanceFromThreat - preferredDistance) * threatWeight;
                        reason += "|range_band";
                    }

                    if (request.PreferFlank && toThreat.sqrMagnitude > 0.001f && candidateFromSelf.sqrMagnitude > 0.001f)
                    {
                        Vector3 side = new Vector3(toThreat.z, 0f, -toThreat.x).normalized;
                        float flankScore = Mathf.Abs(Vector3.Dot(candidateFromSelf.normalized, side));
                        score += flankScore * threatWeight * 2f;
                        reason += "|flank";
                    }
                    break;

                case AIMapRouteIntent.Search:
                case AIMapRouteIntent.Objective:
                case AIMapRouteIntent.Regroup:
                    if (preferredDistance > 0f && distanceFromThreat < preferredDistance)
                    {
                        score -= (preferredDistance - distanceFromThreat) * threatWeight;
                        reason += "|avoid_hotspot";
                    }
                    break;
            }
        }

        public static bool IsFragileArchetype(BrawlerArchetype archetype)
        {
            return archetype == BrawlerArchetype.Sniper ||
                   archetype == BrawlerArchetype.Support ||
                   archetype == BrawlerArchetype.Artillery;
        }
    }
}
