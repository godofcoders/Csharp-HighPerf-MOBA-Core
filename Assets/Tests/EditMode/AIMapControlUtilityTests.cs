using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIMapControlUtilityTests
    {
        [Test]
        public void EvaluateCandidate_IdentifiesCoverPeekWithClearShot()
        {
            MapData map = MakeMap(5, 3, true);
            map.WalkabilityGrid[1, 0] = false;
            AStarSolver solver = new AStarSolver(map);

            AIMapControlEvaluation evaluation = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(4, 1),
                new Vector2Int(4, 1),
                MakeRequest(hasThreat: true, coverPeek: true),
                BrawlerArchetype.Sniper);

            Assert.IsTrue(evaluation.IsCoverPeek);
            Assert.Greater(evaluation.Score, 0f);
            StringAssert.Contains("cover_peek", evaluation.Reason);
        }

        [Test]
        public void EvaluateCandidate_IdentifiesThrowerSafeWallPocket()
        {
            MapData map = MakeMap(5, 3, true);
            map.WalkabilityGrid[2, 1] = false;
            AStarSolver solver = new AStarSolver(map);

            AIMapControlEvaluation evaluation = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(4, 1),
                new Vector2Int(4, 1),
                MakeRequest(hasThreat: true, throwerSafe: true, wallPressure: true),
                BrawlerArchetype.Artillery);

            Assert.IsTrue(evaluation.IsThrowerSafe);
            Assert.IsTrue(evaluation.HasWallPressure);
            Assert.Greater(evaluation.Score, 0f);
            StringAssert.Contains("thrower_safe", evaluation.Reason);
        }

        [Test]
        public void EvaluateCandidate_RewardsLaneOwnershipOverOffLaneCells()
        {
            MapData map = MakeMap(5, 5, true);
            AStarSolver solver = new AStarSolver(map);
            AIMapNavigationRequest request = MakeRequest(hasThreat: false, laneControl: true);

            AIMapControlEvaluation lane = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 2),
                new Vector2Int(2, 2),
                new Vector2Int(4, 2),
                default,
                request,
                BrawlerArchetype.Controller);

            AIMapControlEvaluation offLane = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 2),
                new Vector2Int(2, 4),
                new Vector2Int(4, 2),
                default,
                request,
                BrawlerArchetype.Controller);

            Assert.IsTrue(lane.IsLaneControl);
            Assert.Greater(lane.Score, offLane.Score);
            Assert.IsFalse(offLane.IsLaneControl);
        }

        [Test]
        public void EvaluateCandidate_RewardsChokeControlEdge()
        {
            MapData map = MakeMap(5, 5, true);
            map.WalkabilityGrid[1, 1] = false;
            map.WalkabilityGrid[1, 3] = false;
            map.WalkabilityGrid[2, 1] = false;
            map.WalkabilityGrid[2, 3] = false;
            map.WalkabilityGrid[3, 1] = false;
            map.WalkabilityGrid[3, 3] = false;
            AStarSolver solver = new AStarSolver(map);

            AIMapControlEvaluation evaluation = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 2),
                new Vector2Int(1, 2),
                new Vector2Int(4, 2),
                default,
                MakeRequest(hasThreat: false, chokeControl: true),
                BrawlerArchetype.Controller);

            Assert.IsTrue(solver.IsChokepoint(new Vector2Int(2, 2)));
            Assert.IsFalse(solver.IsChokepoint(new Vector2Int(1, 2)));
            Assert.IsTrue(evaluation.IsChokeControl);
            Assert.Greater(evaluation.Score, 0f);
            StringAssert.Contains("choke_control", evaluation.Reason);
        }

        [Test]
        public void EvaluateCandidate_DirectFireLikesOpenLinesButThrowersLikeWalls()
        {
            MapData map = MakeMap(5, 3, true);
            map.WalkabilityGrid[2, 1] = false;
            AStarSolver solver = new AStarSolver(map);
            AIMapNavigationRequest request = MakeRequest(hasThreat: true, wallPressure: true);

            AIMapControlEvaluation directFire = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(4, 1),
                new Vector2Int(4, 1),
                request,
                BrawlerArchetype.Sniper);

            AIMapControlEvaluation thrower = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(4, 1),
                new Vector2Int(4, 1),
                request,
                BrawlerArchetype.Artillery);

            Assert.Less(directFire.Score, 0f);
            Assert.Greater(thrower.Score, 0f);
            StringAssert.Contains("wall_block", directFire.Reason);
            StringAssert.Contains("wall_throw", thrower.Reason);
        }

        [Test]
        public void EvaluateCandidate_RewardsDesignerAuthoredLane()
        {
            MapData map = MakeMap(5, 5, true);
            int zoneId = map.RegisterSemanticZone(
                "Right Pressure Lane",
                AIMapSemanticTag.Lane,
                AITeamLaneAssignment.Right,
                1.2f);
            map.ApplySemanticZone(
                new Vector2Int(2, 4),
                zoneId,
                AIMapSemanticTag.Lane,
                AITeamLaneAssignment.Right,
                1.2f);

            AStarSolver solver = new AStarSolver(map);

            AIMapControlEvaluation evaluation = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 2),
                new Vector2Int(2, 4),
                new Vector2Int(4, 2),
                default,
                MakeRequest(hasThreat: false, laneControl: true),
                BrawlerArchetype.Controller);

            Assert.IsTrue(evaluation.IsLaneControl);
            Assert.Greater(evaluation.Score, 0f);
            StringAssert.Contains("sem_lane:Right Pressure Lane", evaluation.Reason);
        }

        [Test]
        public void EvaluateCandidate_RewardsDesignerThrowerSafeZone()
        {
            MapData map = MakeMap(5, 3, true);
            int zoneId = map.RegisterSemanticZone(
                "Safe Pocket",
                AIMapSemanticTag.ThrowerSafeZone,
                AITeamLaneAssignment.None,
                1.3f);
            map.ApplySemanticZone(
                new Vector2Int(1, 1),
                zoneId,
                AIMapSemanticTag.ThrowerSafeZone,
                AITeamLaneAssignment.None,
                1.3f);

            AStarSolver solver = new AStarSolver(map);

            AIMapControlEvaluation evaluation = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(4, 1),
                new Vector2Int(4, 1),
                MakeRequest(hasThreat: true, throwerSafe: true),
                BrawlerArchetype.Artillery);

            Assert.IsTrue(evaluation.IsThrowerSafe);
            Assert.Greater(evaluation.Score, 0f);
            StringAssert.Contains("sem_thrower_safe:Safe Pocket", evaluation.Reason);
        }

        [Test]
        public void EvaluateCandidate_PenalizesDesignerDangerCorridor()
        {
            MapData map = MakeMap(5, 3, true);
            int zoneId = map.RegisterSemanticZone(
                "Open Mid Run",
                AIMapSemanticTag.DangerCorridor,
                AITeamLaneAssignment.Mid,
                1.1f);
            map.ApplySemanticZone(
                new Vector2Int(2, 1),
                zoneId,
                AIMapSemanticTag.DangerCorridor,
                AITeamLaneAssignment.Mid,
                1.1f);

            AStarSolver solver = new AStarSolver(map);
            AIMapNavigationRequest request = MakeRequest(hasThreat: false);
            request.Intent = AIMapRouteIntent.Objective;
            request.ChokepointPenalty = 10f;
            request.ExposedPositionPenalty = 8f;

            AIMapControlEvaluation evaluation = AIMapControlUtility.EvaluateCandidate(
                solver,
                new Vector2Int(0, 1),
                new Vector2Int(2, 1),
                new Vector2Int(4, 1),
                default,
                request,
                BrawlerArchetype.Support);

            Assert.IsTrue(evaluation.IsDangerCorridor);
            Assert.Less(evaluation.Score, 0f);
            StringAssert.Contains("sem_danger:Open Mid Run", evaluation.Reason);
        }

        private static AIMapNavigationRequest MakeRequest(
            bool hasThreat,
            bool coverPeek = false,
            bool laneControl = false,
            bool chokeControl = false,
            bool throwerSafe = false,
            bool wallPressure = false)
        {
            return new AIMapNavigationRequest
            {
                HasThreatPosition = hasThreat,
                PreferCoverPeek = coverPeek,
                PreferLaneControl = laneControl,
                PreferChokeControl = chokeControl,
                PreferThrowerSafePosition = throwerSafe,
                PreferWallAwarePressure = wallPressure,
                CoverPeekWeight = 10f,
                LaneControlWeight = 10f,
                ChokeControlWeight = 10f,
                ThrowerSafePositionWeight = 12f,
                WallPressureWeight = 8f,
                ChokepointPenalty = 10f,
                ExposedPositionPenalty = 8f
            };
        }

        private static MapData MakeMap(int width, int height, bool walkable)
        {
            MapData map = new MapData(width, height, 1f, Vector3.zero);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    map.WalkabilityGrid[x, y] = walkable;
                    map.BushGrid[x, y] = false;
                }
            }

            return map;
        }
    }
}
