using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIMapNavigationTests
    {
        [Test]
        public void MapData_ReportsBushCoverAndChokepoints()
        {
            MapData map = MakeMap(3, 3, true);
            map.BushGrid[1, 1] = true;
            map.WalkabilityGrid[0, 1] = false;

            Vector2Int center = new Vector2Int(1, 1);

            Assert.IsTrue(map.IsBush(center));
            Assert.IsTrue(map.IsNearObstacle(center));
            Assert.IsFalse(map.IsChokepoint(center));

            MapData chokeMap = MakeMap(3, 3, false);
            chokeMap.WalkabilityGrid[1, 1] = true;
            chokeMap.WalkabilityGrid[1, 0] = true;
            chokeMap.WalkabilityGrid[1, 2] = true;

            Assert.IsTrue(chokeMap.IsChokepoint(center));
        }

        [Test]
        public void MapData_ReportsDesignerAuthoredSemanticCells()
        {
            MapData map = MakeMap(3, 3, true);
            Vector2Int center = new Vector2Int(1, 1);
            int zoneId = map.RegisterSemanticZone(
                "Mid Choke",
                AIMapSemanticTag.Lane | AIMapSemanticTag.Choke,
                AITeamLaneAssignment.Mid,
                1.25f);

            map.ApplySemanticZone(
                center,
                zoneId,
                AIMapSemanticTag.Lane | AIMapSemanticTag.Choke,
                AITeamLaneAssignment.Mid,
                1.25f);

            Assert.IsTrue(map.HasSemanticTag(center, AIMapSemanticTag.Lane));
            Assert.IsTrue(map.HasSemanticTag(center, AIMapSemanticTag.Choke));
            Assert.IsTrue(map.IsChokepoint(center));
            Assert.AreEqual("Mid Choke", map.GetSemanticZoneName(center));
            StringAssert.Contains("Mid", map.GetSemanticSummary(center));
        }

        [Test]
        public void AStarSolver_UsesDesignerSemanticCoverAndChokes()
        {
            MapData map = MakeMap(3, 3, true);
            int coverZoneId = map.RegisterSemanticZone(
                "Left Cover",
                AIMapSemanticTag.CoverCluster,
                AITeamLaneAssignment.Left,
                1f);
            int chokeZoneId = map.RegisterSemanticZone(
                "Right Choke",
                AIMapSemanticTag.Choke,
                AITeamLaneAssignment.Right,
                1f);

            map.ApplySemanticZone(
                new Vector2Int(1, 1),
                coverZoneId,
                AIMapSemanticTag.CoverCluster,
                AITeamLaneAssignment.Left,
                1f);
            map.ApplySemanticZone(
                new Vector2Int(2, 2),
                chokeZoneId,
                AIMapSemanticTag.Choke,
                AITeamLaneAssignment.Right,
                1f);

            AStarSolver solver = new AStarSolver(map);

            Assert.IsTrue(solver.IsNearObstacle(new Vector2Int(1, 1)));
            Assert.IsTrue(solver.IsChokepoint(new Vector2Int(2, 2)));
            StringAssert.Contains("Left Cover", solver.GetSemanticSummary(new Vector2Int(1, 1)));
        }

        [Test]
        public void AStarSolver_DoesNotCutDiagonalThroughBlockedCorners()
        {
            MapData map = MakeMap(2, 2, true);
            map.WalkabilityGrid[1, 0] = false;
            map.WalkabilityGrid[0, 1] = false;

            AStarSolver solver = new AStarSolver(map);

            Assert.IsNull(solver.FindPath(0, 0, 1, 1));
        }

        [Test]
        public void AStarSolver_ResetsNodeStateBetweenQueries()
        {
            MapData map = MakeMap(5, 5, true);
            AStarSolver solver = new AStarSolver(map);

            Assert.IsNotNull(solver.FindPath(0, 0, 4, 4));
            Assert.IsNotNull(solver.FindPath(4, 4, 0, 0));
            Assert.IsNotNull(solver.FindPath(0, 4, 4, 0));
        }

        [Test]
        public void AStarSolver_DoesNotStartFromBlockedCell()
        {
            MapData map = MakeMap(3, 3, true);
            map.WalkabilityGrid[0, 0] = false;
            AStarSolver solver = new AStarSolver(map);

            Assert.IsNull(solver.FindPath(0, 0, 2, 2));
        }

        [Test]
        public void AStarSolver_FindsNearestWalkableCell()
        {
            MapData map = MakeMap(3, 3, false);
            map.WalkabilityGrid[2, 1] = true;
            AStarSolver solver = new AStarSolver(map);

            Assert.IsTrue(solver.TryGetNearestWalkableCoords(new Vector2Int(1, 1), 2, out Vector2Int coords));
            Assert.AreEqual(new Vector2Int(2, 1), coords);
            Assert.AreEqual(new Vector3(2.5f, 0f, 1.5f), solver.GetNearestWalkableWorldPos(new Vector3(1.5f, 0f, 1.5f), 2));
        }

        [Test]
        public void AStarSolver_PrefersBoundaryClearEndpoint_WhenEdgeCellIsWalkable()
        {
            MapData map = MakeMap(5, 5, true);
            AStarSolver solver = new AStarSolver(map);

            Assert.IsFalse(solver.IsWalkableWithBoundaryClearance(new Vector2Int(4, 4)));
            Assert.IsTrue(solver.TryGetNearestWalkableCoordsWithBoundaryClearance(
                new Vector2Int(4, 4),
                3,
                out Vector2Int coords));
            Assert.AreEqual(new Vector2Int(3, 3), coords);
        }

        [Test]
        public void AStarSolver_FindsNearestNavigationClearCell_AwayFromObstacle()
        {
            MapData map = MakeMap(7, 7, true);
            map.WalkabilityGrid[3, 3] = false;
            AStarSolver solver = new AStarSolver(map);

            Vector2Int nearObstacle = new Vector2Int(3, 2);

            Assert.IsTrue(solver.IsWalkableWithBoundaryClearance(nearObstacle));
            Assert.IsFalse(solver.IsWalkableWithNavigationClearance(nearObstacle));
            Assert.IsTrue(solver.TryGetNearestWalkableCoordsWithNavigationClearance(
                nearObstacle,
                3,
                out Vector2Int repaired));
            Assert.IsTrue(solver.HasObstacleClearance(repaired));
        }

        [Test]
        public void AStarSolver_NavigationClearPath_AvoidsObstacleAdjacentCells()
        {
            MapData map = MakeMap(7, 7, true);
            map.WalkabilityGrid[3, 3] = false;
            AStarSolver solver = new AStarSolver(map);

            var path = solver.FindPathWithNavigationClearance(1, 3, 5, 3);

            Assert.IsNotNull(path);
            foreach (PathNode node in path)
            {
                Assert.IsTrue(solver.HasObstacleClearance(new Vector2Int(node.X, node.Y)));
            }
        }

        [Test]
        public void AIMapNavigationUtility_DetectsCoverBetweenPositions()
        {
            MapData map = MakeMap(5, 3, true);
            map.WalkabilityGrid[2, 1] = false;
            AStarSolver solver = new AStarSolver(map);

            Assert.IsTrue(AIMapNavigationUtility.HasCoverBetween(
                solver,
                map.GetWorldPos(1, 1),
                map.GetWorldPos(3, 1)));
        }

        [Test]
        public void AIMapNavigationUtility_ReportsClearMapLineWhenUnblocked()
        {
            MapData map = MakeMap(5, 3, true);
            AStarSolver solver = new AStarSolver(map);

            Assert.IsFalse(AIMapNavigationUtility.HasCoverBetween(
                solver,
                map.GetWorldPos(1, 1),
                map.GetWorldPos(3, 1)));
        }

        [Test]
        public void AIMapNavigationUtility_BudgetSafeDestination_PrefersBoundaryClearCell()
        {
            MapData map = MakeMap(5, 5, true);
            AStarSolver solver = new AStarSolver(map);

            Vector3 resolved = AIMapNavigationUtility.ResolveBudgetSafeDestination(
                solver,
                null,
                map.GetWorldPos(4, 4));

            Assert.AreEqual(map.GetWorldPos(3, 3), resolved);
        }

        [Test]
        public void AIMapNavigationUtility_BudgetSafeDestination_PrefersObstacleClearCell()
        {
            MapData map = MakeMap(7, 7, true);
            map.WalkabilityGrid[3, 3] = false;
            AStarSolver solver = new AStarSolver(map);

            Vector3 resolved = AIMapNavigationUtility.ResolveBudgetSafeDestination(
                solver,
                null,
                map.GetWorldPos(3, 2));

            Assert.IsTrue(solver.HasObstacleClearance(solver.GetGridCoords(resolved)));
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
