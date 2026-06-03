using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public struct AIMapControlEvaluation
    {
        public float Score;
        public string Reason;
        public bool IsCoverPeek;
        public bool IsLaneControl;
        public bool IsChokeControl;
        public bool IsThrowerSafe;
        public bool HasWallPressure;
        public bool IsCornerPeek;
        public bool HasEscapeSpace;
        public bool IsWallHug;
        public bool HasCoverDance;
        public bool HasFireLanePressure;
        public bool HasThrowerSpacing;
        public bool IsDangerCorridor;
    }

    public static class AIMapControlUtility
    {
        private const float LaneControlWidthCells = 1.35f;

        public static AIMapControlEvaluation EvaluateCandidate(
            AStarSolver pathfinder,
            Vector2Int selfCoords,
            Vector2Int candidateCoords,
            Vector2Int desiredCoords,
            Vector2Int threatCoords,
            in AIMapNavigationRequest request,
            BrawlerArchetype archetype)
        {
            AIMapControlEvaluation evaluation = new AIMapControlEvaluation
            {
                Reason = "none"
            };

            if (pathfinder == null || !pathfinder.IsWalkable(candidateCoords))
                return evaluation;

            bool hasThreat = request.HasThreatPosition;
            bool nearCover = pathfinder.IsNearObstacle(candidateCoords);
            bool insideChoke = pathfinder.IsChokepoint(candidateCoords);
            bool adjacentChoke = HasAdjacentChoke(pathfinder, candidateCoords);
            int walkableNeighbors = pathfinder.CountWalkableNeighbors(candidateCoords);
            int blockedNeighbors = pathfinder.CountBlockedNeighbors(candidateCoords);
            int cardinalExits = CountCardinalWalkableNeighbors(pathfinder, candidateCoords);
            AIMapSemanticCell semantic = pathfinder.GetSemanticCell(candidateCoords);
            bool hasCoverBetween = hasThreat &&
                                   AIMapNavigationUtility.HasCoverBetween(
                                       pathfinder,
                                       candidateCoords,
                                       threatCoords);

            if (request.PreferCoverPeek)
            {
                ApplyCoverPeekScore(
                    nearCover,
                    insideChoke,
                    hasThreat,
                    hasCoverBetween,
                    walkableNeighbors,
                    blockedNeighbors,
                    cardinalExits,
                    request,
                    ref evaluation);
            }

            if (request.PreferLaneControl)
            {
                ApplyLaneControlScore(
                    selfCoords,
                    candidateCoords,
                    hasThreat ? threatCoords : desiredCoords,
                    insideChoke,
                    request,
                    ref evaluation);
            }

            if (request.PreferChokeControl)
            {
                ApplyChokeControlScore(
                    insideChoke,
                    adjacentChoke,
                    archetype,
                    request,
                    ref evaluation);
            }

            if (request.PreferThrowerSafePosition)
            {
                ApplyThrowerSafeScore(
                    pathfinder,
                    candidateCoords,
                    hasThreat,
                    nearCover,
                    hasCoverBetween,
                    request,
                    ref evaluation);
            }

            if (request.PreferWallAwarePressure)
            {
                ApplyWallAwarePressureScore(
                    hasThreat,
                    hasCoverBetween,
                    archetype,
                    request,
                    ref evaluation);
            }

            if (request.PenalizeWallHug ||
                request.PreferEscapeSpace ||
                request.PreferCoverDance ||
                request.PreferFireLanePressure ||
                request.PreferThrowerSpacing)
            {
                ApplyCombatGeometryScore(
                    pathfinder,
                    candidateCoords,
                    threatCoords,
                    hasThreat,
                    nearCover,
                    insideChoke,
                    hasCoverBetween,
                    walkableNeighbors,
                    blockedNeighbors,
                    cardinalExits,
                    request,
                    archetype,
                    ref evaluation);
            }

            if (semantic.HasAny)
            {
                ApplySemanticScore(
                    pathfinder,
                    candidateCoords,
                    semantic,
                    hasThreat,
                    hasCoverBetween,
                    request,
                    archetype,
                    ref evaluation);
            }

            return evaluation;
        }

        private static void ApplyCoverPeekScore(
            bool nearCover,
            bool insideChoke,
            bool hasThreat,
            bool hasCoverBetween,
            int walkableNeighbors,
            int blockedNeighbors,
            int cardinalExits,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            if (!hasThreat || !nearCover || hasCoverBetween)
                return;

            if (cardinalExits <= 0)
                return;

            bool cornerPeek = blockedNeighbors > 0 &&
                              cardinalExits >= 2 &&
                              walkableNeighbors >= 4 &&
                              !insideChoke;
            float exitFactor = Mathf.Clamp01((cardinalExits * 0.70f + walkableNeighbors * 0.30f) / 5f);
            float chokeFactor = insideChoke ? 0.65f : 1f;
            float cornerFactor = cornerPeek ? 1.12f : 1f;
            float score = Mathf.Max(0f, request.CoverPeekWeight) *
                          Mathf.Max(0.35f, exitFactor) *
                          chokeFactor *
                          cornerFactor;

            if (score <= 0f)
                return;

            evaluation.Score += score;
            evaluation.IsCoverPeek = true;
            evaluation.IsCornerPeek = cornerPeek;
            AppendReason(ref evaluation, $"cover_peek+{score:0.0}");
        }

        private static void ApplyLaneControlScore(
            Vector2Int selfCoords,
            Vector2Int candidateCoords,
            Vector2Int anchorCoords,
            bool insideChoke,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            Vector2 lane = new Vector2(
                anchorCoords.x - selfCoords.x,
                anchorCoords.y - selfCoords.y);

            if (lane.sqrMagnitude <= 0.001f)
                return;

            Vector2 candidate = new Vector2(
                candidateCoords.x - selfCoords.x,
                candidateCoords.y - selfCoords.y);

            Vector2 laneDir = lane.normalized;
            float projection = Vector2.Dot(candidate, laneDir);
            if (projection < -0.25f)
                return;

            float lateral = Mathf.Abs(Cross(candidate, laneDir));
            if (lateral > LaneControlWidthCells)
                return;

            float laneProgress = Mathf.Clamp01(projection / Mathf.Max(1f, lane.magnitude));
            float lateralScore = 1f - (lateral / LaneControlWidthCells);
            float chokeFactor = insideChoke ? 0.75f : 1f;
            float score = Mathf.Max(0f, request.LaneControlWeight) *
                          (0.45f + laneProgress * 0.35f + lateralScore * 0.20f) *
                          chokeFactor;

            if (score <= 0f)
                return;

            evaluation.Score += score;
            evaluation.IsLaneControl = true;
            AppendReason(ref evaluation, $"lane+{score:0.0}");
        }

        private static void ApplyChokeControlScore(
            bool insideChoke,
            bool adjacentChoke,
            BrawlerArchetype archetype,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            float weight = Mathf.Max(0f, request.ChokeControlWeight);
            if (weight <= 0f)
                return;

            if (adjacentChoke && !insideChoke)
            {
                evaluation.Score += weight;
                evaluation.IsChokeControl = true;
                AppendReason(ref evaluation, $"choke_control+{weight:0.0}");
                return;
            }

            if (!insideChoke)
                return;

            if (archetype == BrawlerArchetype.Tank)
            {
                float score = weight * 0.35f;
                evaluation.Score += score;
                evaluation.IsChokeControl = true;
                AppendReason(ref evaluation, $"choke_anchor+{score:0.0}");
                return;
            }

            float penalty = weight * 0.45f;
            evaluation.Score -= penalty;
            AppendReason(ref evaluation, $"inside_choke-{penalty:0.0}");
        }

        private static void ApplyThrowerSafeScore(
            AStarSolver pathfinder,
            Vector2Int candidateCoords,
            bool hasThreat,
            bool nearCover,
            bool hasCoverBetween,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            if (!hasThreat)
                return;

            float weight = Mathf.Max(0f, request.ThrowerSafePositionWeight);
            if (weight <= 0f)
                return;

            int exits = pathfinder.CountWalkableNeighbors(candidateCoords);
            if (hasCoverBetween)
            {
                float coverFactor = nearCover ? 1f : 0.75f;
                float exitFactor = Mathf.Clamp01(exits / 5f);
                float score = weight * coverFactor * Mathf.Max(0.45f, exitFactor);

                evaluation.Score += score;
                evaluation.IsThrowerSafe = true;
                AppendReason(ref evaluation, $"thrower_safe+{score:0.0}");
                return;
            }

            float penalty = weight * 0.45f;
            evaluation.Score -= penalty;
            AppendReason(ref evaluation, $"thrower_exposed-{penalty:0.0}");
        }

        private static void ApplyWallAwarePressureScore(
            bool hasThreat,
            bool hasCoverBetween,
            BrawlerArchetype archetype,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            if (!hasThreat)
                return;

            float weight = Mathf.Max(0f, request.WallPressureWeight);
            if (weight <= 0f)
                return;

            bool thrower = archetype == BrawlerArchetype.Artillery;
            if (thrower)
            {
                float score = hasCoverBetween ? weight : -(weight * 0.30f);
                evaluation.Score += score;
                evaluation.HasWallPressure = hasCoverBetween;
                AppendReason(ref evaluation, hasCoverBetween
                    ? $"wall_throw+{score:0.0}"
                    : $"wall_throw_exposed{score:0.0}");
                return;
            }

            if (!hasCoverBetween)
            {
                evaluation.Score += weight;
                evaluation.HasWallPressure = true;
                AppendReason(ref evaluation, $"wall_line+{weight:0.0}");
                return;
            }

            float penalty = weight * 0.40f;
            evaluation.Score -= penalty;
            AppendReason(ref evaluation, $"wall_block-{penalty:0.0}");
        }

        private static void ApplyCombatGeometryScore(
            AStarSolver pathfinder,
            Vector2Int candidateCoords,
            Vector2Int threatCoords,
            bool hasThreat,
            bool nearCover,
            bool insideChoke,
            bool hasCoverBetween,
            int walkableNeighbors,
            int blockedNeighbors,
            int cardinalExits,
            in AIMapNavigationRequest request,
            BrawlerArchetype archetype,
            ref AIMapControlEvaluation evaluation)
        {
            bool wallHug = nearCover &&
                           (cardinalExits <= 1 ||
                            walkableNeighbors <= 3 ||
                            blockedNeighbors >= 5);
            bool hasEscapeSpace = cardinalExits >= 3 &&
                                  walkableNeighbors >= 5 &&
                                  !insideChoke;

            if (request.PenalizeWallHug && wallHug)
            {
                float weight = Mathf.Max(0f, request.WallHugPenalty);
                if (weight > 0f)
                {
                    float blockedFactor = Mathf.Clamp01(blockedNeighbors / 6f);
                    float exitFactor = cardinalExits <= 1 ? 1f : 0.70f;
                    float roleFactor = archetype == BrawlerArchetype.Tank
                        ? 0.65f
                        : 1f;

                    if (archetype == BrawlerArchetype.Artillery &&
                        hasThreat &&
                        hasCoverBetween &&
                        cardinalExits >= 2)
                    {
                        roleFactor = 0.45f;
                    }

                    float penalty = weight *
                                    (0.60f + blockedFactor * 0.40f) *
                                    exitFactor *
                                    roleFactor;

                    evaluation.Score -= penalty;
                    evaluation.IsWallHug = true;
                    AppendReason(ref evaluation, $"wall_hug-{penalty:0.0}");
                }
            }

            if (request.PreferEscapeSpace && hasEscapeSpace)
            {
                float weight = Mathf.Max(0f, request.EscapeSpaceWeight);
                if (weight > 0f)
                {
                    float roleFactor = AIMapNavigationUtility.IsFragileArchetype(archetype)
                        ? 1.15f
                        : 1f;
                    float exitFactor = Mathf.Clamp01((cardinalExits + walkableNeighbors * 0.25f) / 5f);
                    float score = weight *
                                  (0.55f + exitFactor * 0.45f) *
                                  roleFactor;

                    evaluation.Score += score;
                    evaluation.HasEscapeSpace = true;
                    AppendReason(ref evaluation, $"escape_space+{score:0.0}");
                }
            }

            if (request.PreferCoverDance &&
                hasThreat &&
                nearCover &&
                cardinalExits >= 2 &&
                !insideChoke)
            {
                float weight = Mathf.Max(0f, request.CoverDanceWeight);
                if (weight > 0f)
                {
                    bool cornerPeek = !hasCoverBetween &&
                                      blockedNeighbors > 0 &&
                                      walkableNeighbors >= 4;
                    float coverFactor = hasCoverBetween ? 0.82f : 1f;
                    float cornerFactor = cornerPeek ? 1.18f : 1f;
                    float exitFactor = Mathf.Clamp01((cardinalExits + walkableNeighbors * 0.20f) / 5f);
                    float score = weight *
                                  (0.50f + exitFactor * 0.50f) *
                                  coverFactor *
                                  cornerFactor;

                    evaluation.Score += score;
                    evaluation.HasCoverDance = true;
                    if (cornerPeek)
                        evaluation.IsCornerPeek = true;

                    AppendReason(ref evaluation, $"cover_dance+{score:0.0}");
                }
            }

            if (request.PreferFireLanePressure &&
                hasThreat &&
                !hasCoverBetween &&
                !insideChoke &&
                cardinalExits >= 2)
            {
                float weight = Mathf.Max(0f, request.FireLanePressureWeight);
                if (weight > 0f)
                {
                    float distance = GridDistanceWorld(candidateCoords, threatCoords, pathfinder.CellSize);
                    float preferredDistance = Mathf.Max(1f, request.PreferredThreatDistance);
                    float distanceFactor = Mathf.Clamp01(distance / preferredDistance);
                    float exitFactor = Mathf.Clamp01((cardinalExits + walkableNeighbors * 0.20f) / 5f);
                    float coverFactor = nearCover ? 1.08f : 0.90f;
                    float wallHugFactor = wallHug ? 0.72f : 1f;
                    float score = weight *
                                  (0.45f + distanceFactor * 0.35f + exitFactor * 0.20f) *
                                  coverFactor *
                                  wallHugFactor;

                    evaluation.Score += score;
                    evaluation.HasFireLanePressure = true;
                    AppendReason(ref evaluation, $"fire_lane+{score:0.0}");
                }
            }

            if (request.PreferThrowerSpacing &&
                hasThreat &&
                archetype == BrawlerArchetype.Artillery)
            {
                ApplyThrowerSpacingScore(
                    pathfinder,
                    candidateCoords,
                    threatCoords,
                    hasCoverBetween,
                    insideChoke,
                    walkableNeighbors,
                    cardinalExits,
                    request,
                    ref evaluation);
            }
        }

        private static void ApplyThrowerSpacingScore(
            AStarSolver pathfinder,
            Vector2Int candidateCoords,
            Vector2Int threatCoords,
            bool hasCoverBetween,
            bool insideChoke,
            int walkableNeighbors,
            int cardinalExits,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            float weight = Mathf.Max(0f, request.ThrowerSpacingWeight);
            if (weight <= 0f)
                return;

            if (!hasCoverBetween)
            {
                float penalty = weight * 0.25f;
                evaluation.Score -= penalty;
                AppendReason(ref evaluation, $"thrower_spacing_exposed-{penalty:0.0}");
                return;
            }

            if (insideChoke || cardinalExits < 2)
                return;

            float distance = GridDistanceWorld(candidateCoords, threatCoords, pathfinder.CellSize);
            float preferredDistance = Mathf.Max(1f, request.PreferredThreatDistance);
            float distanceFactor = Mathf.Clamp01(distance / preferredDistance);
            float exitFactor = Mathf.Clamp01((cardinalExits + walkableNeighbors * 0.20f) / 5f);
            float score = weight *
                          (0.45f + distanceFactor * 0.35f + exitFactor * 0.20f);

            evaluation.Score += score;
            evaluation.HasThrowerSpacing = true;
            AppendReason(ref evaluation, $"thrower_spacing+{score:0.0}");
        }

        private static bool HasAdjacentChoke(AStarSolver pathfinder, Vector2Int coords)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Vector2Int neighbor = new Vector2Int(coords.x + x, coords.y + y);
                    if (pathfinder.IsWalkable(neighbor) && pathfinder.IsChokepoint(neighbor))
                        return true;
                }
            }

            return false;
        }

        private static void ApplySemanticScore(
            AStarSolver pathfinder,
            Vector2Int candidateCoords,
            AIMapSemanticCell semantic,
            bool hasThreat,
            bool hasCoverBetween,
            in AIMapNavigationRequest request,
            BrawlerArchetype archetype,
            ref AIMapControlEvaluation evaluation)
        {
            float influence = Mathf.Max(0.25f, semantic.Influence);
            string zoneName = pathfinder.GetSemanticZoneName(candidateCoords);
            if (string.IsNullOrEmpty(zoneName))
                zoneName = "zone";

            if (request.PreferLaneControl && semantic.HasTag(AIMapSemanticTag.Lane))
            {
                float score = Mathf.Max(0f, request.LaneControlWeight) * influence * 0.85f;
                if (score > 0f)
                {
                    evaluation.Score += score;
                    evaluation.IsLaneControl = true;
                    AppendReason(ref evaluation, $"sem_lane:{zoneName}+{score:0.0}");
                }
            }

            if (request.PreferChokeControl && semantic.HasTag(AIMapSemanticTag.Choke))
            {
                float roleFactor = archetype == BrawlerArchetype.Tank ||
                                   archetype == BrawlerArchetype.Controller
                    ? 0.85f
                    : 0.45f;
                float score = Mathf.Max(0f, request.ChokeControlWeight) * influence * roleFactor;
                if (score > 0f)
                {
                    evaluation.Score += score;
                    evaluation.IsChokeControl = true;
                    AppendReason(ref evaluation, $"sem_choke:{zoneName}+{score:0.0}");
                }
            }

            if (request.PreferCoverPeek &&
                hasThreat &&
                !hasCoverBetween &&
                semantic.HasTag(AIMapSemanticTag.CoverCluster))
            {
                float score = Mathf.Max(0f, request.CoverPeekWeight) * influence * 0.70f;
                if (score > 0f)
                {
                    evaluation.Score += score;
                    evaluation.IsCoverPeek = true;
                    AppendReason(ref evaluation, $"sem_cover:{zoneName}+{score:0.0}");
                }
            }

            if (request.PreferThrowerSafePosition &&
                semantic.HasTag(AIMapSemanticTag.ThrowerSafeZone))
            {
                float threatFactor = hasThreat ? 1f : 0.65f;
                float coverFactor = hasCoverBetween ? 1f : 0.75f;
                float score = Mathf.Max(0f, request.ThrowerSafePositionWeight) *
                              influence *
                              threatFactor *
                              coverFactor;
                if (score > 0f)
                {
                    evaluation.Score += score;
                    evaluation.IsThrowerSafe = true;
                    AppendReason(ref evaluation, $"sem_thrower_safe:{zoneName}+{score:0.0}");
                }
            }

            if (semantic.HasTag(AIMapSemanticTag.DangerCorridor))
            {
                float basePenalty = Mathf.Max(
                    Mathf.Max(0f, request.ChokepointPenalty),
                    Mathf.Max(0f, request.ExposedPositionPenalty));
                float routeFactor = IsSensitiveToDangerCorridor(request.Intent) ? 1.25f : 0.70f;
                float penalty = basePenalty * influence * routeFactor;

                if (penalty > 0f)
                {
                    evaluation.Score -= penalty;
                    evaluation.IsDangerCorridor = true;
                    AppendReason(ref evaluation, $"sem_danger:{zoneName}-{penalty:0.0}");
                }
            }
        }

        private static bool IsSensitiveToDangerCorridor(AIMapRouteIntent intent)
        {
            return intent == AIMapRouteIntent.CombatRetreat ||
                   intent == AIMapRouteIntent.Evade ||
                   intent == AIMapRouteIntent.Objective ||
                   intent == AIMapRouteIntent.Search ||
                   intent == AIMapRouteIntent.Regroup;
        }

        private static int CountCardinalWalkableNeighbors(AStarSolver pathfinder, Vector2Int coords)
        {
            int count = 0;

            if (pathfinder.IsWalkable(new Vector2Int(coords.x + 1, coords.y)))
                count++;

            if (pathfinder.IsWalkable(new Vector2Int(coords.x - 1, coords.y)))
                count++;

            if (pathfinder.IsWalkable(new Vector2Int(coords.x, coords.y + 1)))
                count++;

            if (pathfinder.IsWalkable(new Vector2Int(coords.x, coords.y - 1)))
                count++;

            return count;
        }

        private static float GridDistanceWorld(Vector2Int from, Vector2Int to, float cellSize)
        {
            int dx = from.x - to.x;
            int dy = from.y - to.y;
            return Mathf.Sqrt((dx * dx) + (dy * dy)) * Mathf.Max(0.1f, cellSize);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        private static void AppendReason(ref AIMapControlEvaluation evaluation, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;

            evaluation.Reason = evaluation.Reason == "none"
                ? reason
                : $"{evaluation.Reason}|{reason}";
        }
    }
}
