namespace MOBA.Core.Simulation.AI
{
    public class MapData
    {
        public bool[,] WalkabilityGrid;
        public bool[,] BushGrid;
        public float CellSize;
        public UnityEngine.Vector3 Origin;
        public int Width => WalkabilityGrid != null ? WalkabilityGrid.GetLength(0) : 0;
        public int Height => WalkabilityGrid != null ? WalkabilityGrid.GetLength(1) : 0;

        public MapData(int width, int height, float cellSize, UnityEngine.Vector3 origin)
        {
            WalkabilityGrid = new bool[width, height];
            BushGrid = new bool[width, height];
            CellSize = cellSize;
            Origin = origin;
        }

        public bool IsInBounds(UnityEngine.Vector2Int coords)
        {
            return coords.x >= 0 &&
                   coords.y >= 0 &&
                   coords.x < Width &&
                   coords.y < Height;
        }

        public UnityEngine.Vector2Int GetGridCoords(UnityEngine.Vector3 worldPos)
        {
            int x = UnityEngine.Mathf.FloorToInt((worldPos.x - Origin.x) / CellSize);
            int y = UnityEngine.Mathf.FloorToInt((worldPos.z - Origin.z) / CellSize);

            x = UnityEngine.Mathf.Clamp(x, 0, UnityEngine.Mathf.Max(0, Width - 1));
            y = UnityEngine.Mathf.Clamp(y, 0, UnityEngine.Mathf.Max(0, Height - 1));

            return new UnityEngine.Vector2Int(x, y);
        }

        public UnityEngine.Vector3 GetWorldPos(int x, int y)
        {
            return new UnityEngine.Vector3(
                Origin.x + (x * CellSize) + (CellSize * 0.5f),
                0f,
                Origin.z + (y * CellSize) + (CellSize * 0.5f));
        }

        public UnityEngine.Vector3 GetWorldPos(UnityEngine.Vector2Int coords)
        {
            return GetWorldPos(coords.x, coords.y);
        }

        public bool IsWalkable(UnityEngine.Vector2Int coords)
        {
            return IsInBounds(coords) && WalkabilityGrid[coords.x, coords.y];
        }

        public bool IsBush(UnityEngine.Vector2Int coords)
        {
            return IsInBounds(coords) &&
                   BushGrid != null &&
                   BushGrid[coords.x, coords.y];
        }

        public int CountWalkableNeighbors(UnityEngine.Vector2Int coords)
        {
            int count = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    if (IsWalkable(new UnityEngine.Vector2Int(coords.x + x, coords.y + y)))
                        count++;
                }
            }

            return count;
        }

        public int CountBlockedNeighbors(UnityEngine.Vector2Int coords)
        {
            int count = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    UnityEngine.Vector2Int neighbor = new UnityEngine.Vector2Int(coords.x + x, coords.y + y);
                    if (!IsInBounds(neighbor) || !IsWalkable(neighbor))
                        count++;
                }
            }

            return count;
        }

        public bool IsNearObstacle(UnityEngine.Vector2Int coords)
        {
            return CountBlockedNeighbors(coords) > 0;
        }

        public bool IsChokepoint(UnityEngine.Vector2Int coords)
        {
            if (!IsWalkable(coords))
                return true;

            return CountWalkableNeighbors(coords) <= 3 || CountBlockedNeighbors(coords) >= 5;
        }
    }
}
