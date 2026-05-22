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
