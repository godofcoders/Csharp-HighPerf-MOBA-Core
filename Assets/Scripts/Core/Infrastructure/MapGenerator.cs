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

        [Header("Semantic Authoring")]
        [Tooltip("Optional root that contains AIMapSemanticZone components. If unset, active semantic zones in the scene are discovered once during map bake.")]
        public Transform SemanticZoneRoot;
        public bool BakeSemanticZones = true;

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

            int semanticZoneCount = BakeSemanticZones ? BakeMapSemantics() : 0;

            Debug.Log($"[MAP] Bake Complete: {Width}x{Height} grid. SemanticZones={semanticZoneCount}");
            return _mapData;
        }

        private int BakeMapSemantics()
        {
            AIMapSemanticZone[] zones = SemanticZoneRoot != null
                ? SemanticZoneRoot.GetComponentsInChildren<AIMapSemanticZone>(false)
                : FindObjectsOfType<AIMapSemanticZone>(false);
            if (zones == null || zones.Length == 0)
                return 0;

            int bakedZones = 0;
            for (int i = 0; i < zones.Length; i++)
            {
                AIMapSemanticZone zone = zones[i];
                if (zone == null || zone.Tags == AIMapSemanticTag.None || zone.Influence <= 0f)
                    continue;

                int zoneId = _mapData.RegisterSemanticZone(
                    zone.ZoneName,
                    zone.Tags,
                    zone.Lane,
                    zone.Influence);

                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        Vector3 worldPos = GetWorldPos(x, y);
                        if (!zone.ContainsWorldPosition(worldPos))
                            continue;

                        _mapData.ApplySemanticZone(
                            new Vector2Int(x, y),
                            zoneId,
                            zone.Tags,
                            zone.Lane,
                            zone.Influence);
                    }
                }

                bakedZones++;
            }

            return bakedZones;
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
