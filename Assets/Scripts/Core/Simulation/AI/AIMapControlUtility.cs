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
            bool hasCoverBetween = hasThreat &&
                                   AIMapNavigationUtility.HasCoverBetween(
                                       pathfinder,
                                       candidateCoords,
                                       threatCoords);

            if (request.PreferCoverPeek)
            {
                ApplyCoverPeekScore(
                    pathfinder,
                    candidateCoords,
                    nearCover,
                    insideChoke,
                    hasThreat,
                    hasCoverBetween,
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

            return evaluation;
        }

        private static void ApplyCoverPeekScore(
            AStarSolver pathfinder,
            Vector2Int candidateCoords,
            bool nearCover,
            bool insideChoke,
            bool hasThreat,
            bool hasCoverBetween,
            in AIMapNavigationRequest request,
            ref AIMapControlEvaluation evaluation)
        {
            if (!hasThreat || !nearCover || hasCoverBetween)
                return;

            float exitFactor = Mathf.Clamp01(pathfinder.CountWalkableNeighbors(candidateCoords) / 5f);
            float chokeFactor = insideChoke ? 0.65f : 1f;
            float score = Mathf.Max(0f, request.CoverPeekWeight) *
                          Mathf.Max(0.35f, exitFactor) *
                          chokeFactor;

            if (score <= 0f)
                return;

            evaluation.Score += score;
            evaluation.IsCoverPeek = true;
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
