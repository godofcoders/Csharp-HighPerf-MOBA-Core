using UnityEngine;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Infrastructure
{
    public class MapGenerator : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int Width = 50;
        public int Height = 50;
        public float CellSize = 1.0f;
        public LayerMask ObstacleLayer;

        [Header("Visualization")]
        public bool ShowDebugGrid = true;

        private MapData _mapData;
        [Header("Stealth Settings")]
        public LayerMask BushLayer;
        public MapData BakeMap()
        {
            // Calculate origin so the grid is centered on this GameObject
            Vector3 origin = transform.position - new Vector3(Width * CellSize / 2, 0, Height * CellSize / 2);
            _mapData = new MapData(Width, Height, CellSize, origin);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Vector3 worldPos = GetWorldPos(x, y);

                    // Check for obstacles using a small box check
                    // We check slightly above the ground to hit walls
                    bool isBlocked = Physics.CheckBox(worldPos + Vector3.up,
                        new Vector3(CellSize / 2.1f, 0.5f, CellSize / 2.1f),
                        Quaternion.identity,
                        ObstacleLayer);

                    _mapData.WalkabilityGrid[x, y] = !isBlocked;

                    bool isBush = Physics.CheckBox(worldPos + Vector3.up, new Vector3(CellSize / 2.1f, 0.5f, CellSize / 2.1f), Quaternion.identity, BushLayer);
                    _mapData.BushGrid[x, y] = isBush;
                }
            }

            Debug.Log($"[MAP] Bake Complete: {Width}x{Height} grid.");
            return _mapData;
        }

        public Vector3 GetWorldPos(int x, int y)
        {
            return _mapData.Origin + new Vector3(x * CellSize + CellSize / 2, 0, y * CellSize + CellSize / 2);
        }

        // Helper to convert Brawler World Pos -> Grid Coords for A*
        public Vector2Int GetGridCoords(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - _mapData.Origin.x) / CellSize);
            int y = Mathf.FloorToInt((worldPos.z - _mapData.Origin.z) / CellSize);
            return new Vector2Int(x, y);
        }

        private void OnDrawGizmos()
        {
            if (!ShowDebugGrid || _mapData == null) return;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Gizmos.color = _mapData.WalkabilityGrid[x, y] ? Color.green : Color.red;
                    Gizmos.DrawWireCube(GetWorldPos(x, y), new Vector3(CellSize, 0.1f, CellSize));
                }
            }
        }
    }
}