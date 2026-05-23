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
        public int CandidateCount;
        public int PathValidationCount;
    }

    public static class AIMapNavigationUtility
    {
        private const int MaxPathValidatedCandidates = 4;

        private struct CandidateScore
        {
            public Vector2Int Coords;
            public Vector3 Destination;
            public float Score;
            public string Reason;
            public bool IsValid;
        }

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
                UsedMap = false,
                CandidateCount = 0,
                PathValidationCount = 0
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

            CandidateScore first = default;
            CandidateScore second = default;
            CandidateScore third = default;
            CandidateScore fourth = default;
            int candidateCount = 0;
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

                    candidateCount++;
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

                    InsertCandidate(
                        ref first,
                        ref second,
                        ref third,
                        ref fourth,
                        new CandidateScore
                        {
                            Coords = coords,
                            Destination = candidate,
                            Score = score,
                            Reason = reason,
                            IsValid = true
                        });
                }
            }

            if (TrySelectReachableCandidate(
                self,
                pathfinder,
                selfCoords,
                request,
                first,
                second,
                third,
                fourth,
                out int pathValidationCount,
                out CandidateScore bestCandidate))
            {
                decision.ResolvedDestination = bestCandidate.Destination;
                decision.Reason = bestCandidate.Reason;
                decision.Score = bestCandidate.Score;
                decision.UsedMap = true;
                decision.CandidateCount = candidateCount;
                decision.PathValidationCount = pathValidationCount;

                return bestCandidate.Destination;
            }

            Vector3 bestDestination = pathfinder.GetNearestWalkableWorldPos(
                request.DesiredDestination,
                Mathf.Max(1, maxOffsetMagnitude));

            decision.ResolvedDestination = bestDestination;
            decision.Reason = "nearest_walkable_fallback";
            decision.Score = 0f;
            decision.UsedMap = false;
            decision.CandidateCount = candidateCount;
            decision.PathValidationCount = 0;

            return bestDestination;
        }

        private static void InsertCandidate(
            ref CandidateScore first,
            ref CandidateScore second,
            ref CandidateScore third,
            ref CandidateScore fourth,
            CandidateScore candidate)
        {
            if (!first.IsValid || candidate.Score > first.Score)
            {
                fourth = third;
                third = second;
                second = first;
                first = candidate;
                return;
            }

            if (!second.IsValid || candidate.Score > second.Score)
            {
                fourth = third;
                third = second;
                second = candidate;
                return;
            }

            if (!third.IsValid || candidate.Score > third.Score)
            {
                fourth = third;
                third = candidate;
                return;
            }

            if (!fourth.IsValid || candidate.Score > fourth.Score)
                fourth = candidate;
        }

        private static bool TrySelectReachableCandidate(
            BrawlerController self,
            AStarSolver pathfinder,
            Vector2Int selfCoords,
            in AIMapNavigationRequest request,
            CandidateScore first,
            CandidateScore second,
            CandidateScore third,
            CandidateScore fourth,
            out int pathValidationCount,
            out CandidateScore selected)
        {
            pathValidationCount = 0;
            selected = default;

            return TrySelectReachableCandidate(self, pathfinder, selfCoords, request, first, ref pathValidationCount, out selected) ||
                   TrySelectReachableCandidate(self, pathfinder, selfCoords, request, second, ref pathValidationCount, out selected) ||
                   TrySelectReachableCandidate(self, pathfinder, selfCoords, request, third, ref pathValidationCount, out selected) ||
                   TrySelectReachableCandidate(self, pathfinder, selfCoords, request, fourth, ref pathValidationCount, out selected);
        }

        private static bool TrySelectReachableCandidate(
            BrawlerController self,
            AStarSolver pathfinder,
            Vector2Int selfCoords,
            in AIMapNavigationRequest request,
            CandidateScore candidate,
            ref int pathValidationCount,
            out CandidateScore selected)
        {
            selected = default;

            if (!candidate.IsValid)
                return false;

            if (!RequiresPathValidation(request.Intent) ||
                (candidate.Destination - self.Position).sqrMagnitude <= pathfinder.CellSize * pathfinder.CellSize)
            {
                selected = candidate;
                return true;
            }

            pathValidationCount++;
            if (!pathfinder.TryGetPathLength(
                selfCoords.x,
                selfCoords.y,
                candidate.Coords.x,
                candidate.Coords.y,
                out int pathLength))
            {
                return false;
            }

            candidate.Score -= pathLength * Mathf.Max(0f, request.PathCostWeight);
            candidate.Reason += "|path";
            selected = candidate;
            return true;
        }

        private static bool RequiresPathValidation(AIMapRouteIntent intent)
        {
            return intent != AIMapRouteIntent.Evade;
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

            int estimatedSteps = EstimateGridSteps(selfCoords, coords);
            score -= estimatedSteps * Mathf.Max(0f, request.PathCostWeight);

            if (request.Intent == AIMapRouteIntent.Evade)
                reason += "|fast_evade";

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

        private static int EstimateGridSteps(Vector2Int from, Vector2Int to)
        {
            return Mathf.Max(
                Mathf.Abs(from.x - to.x),
                Mathf.Abs(from.y - to.y));
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
