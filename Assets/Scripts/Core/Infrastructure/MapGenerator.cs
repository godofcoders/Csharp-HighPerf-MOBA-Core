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
        [Tooltip("When enabled, navigation bounds are fitted to the loaded map's ground instead of the fixed Width/Height rectangle.")]
        public bool FitGridToGroundBounds = true;
        [Min(0f)] public float GroundBoundsPadding = 0f;
        public LayerMask ObstacleLayer;

        [Header("Grounding")]
        [Tooltip("Layer mask used to decide whether a grid cell has playable ground under it. If unset, all non-obstacle layers are probed.")]
        public LayerMask GroundLayer;
        public float GroundProbeHeight = 6f;
        public float GroundProbeDistance = 12f;

        [Header("Navigation Clearance")]
        [Tooltip("Approximate brawler body radius used when baking walkable cells. Keeps AI paths away from obstacle edges.")]
        public float AgentRadius = 0.5f;
        [Tooltip("Extra clearance between brawler bodies and baked obstacle cells.")]
        public float AgentObstacleClearance = 0.1f;

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
            float cellSize = Mathf.Max(0.05f, CellSize);
            ResolveGridBakeArea(cellSize, out int gridWidth, out int gridHeight, out Vector3 origin);

            _mapData = new MapData(gridWidth, gridHeight, cellSize, origin);
            bool[,] groundedGrid = new bool[gridWidth, gridHeight];
            int groundedCells = 0;
            int groundMask = ResolveGroundMask();
            int bushMask = ResolveBushMask();
            Vector3 obstacleProbeExtents = GetObstacleProbeExtents();
            Vector3 bushProbeExtents = GetBushProbeExtents();

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3 worldPos = GetWorldPos(x, y);
                    bool hasGround = HasPlayableGround(worldPos, groundMask);
                    groundedGrid[x, y] = hasGround;

                    if (hasGround)
                        groundedCells++;
                }
            }

            bool requireGround = groundedCells > 0;
            if (!requireGround)
            {
                Debug.LogWarning(
                    "[MAP] Bake found no playable ground hits. Falling back to obstacle-only walkability. " +
                    "Assign GroundLayer or add ground colliders to prevent AI from using void cells.");
            }

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3 worldPos = GetWorldPos(x, y);

                    // Check for obstacles using a small box check
                    // We check slightly above the ground to hit walls
                    bool isBlocked = Physics.CheckBox(worldPos + Vector3.up,
                        obstacleProbeExtents,
                        Quaternion.identity,
                        ObstacleLayer,
                        QueryTriggerInteraction.Ignore);

                    _mapData.WalkabilityGrid[x, y] =
                        !isBlocked &&
                        (!requireGround || groundedGrid[x, y]);

                    bool isBush =
                        bushMask != 0 &&
                        Physics.CheckBox(
                            worldPos + Vector3.up,
                            bushProbeExtents,
                            Quaternion.identity,
                            bushMask,
                            QueryTriggerInteraction.Collide);
                    _mapData.BushGrid[x, y] = isBush;
                }
            }

            int semanticZoneCount = BakeSemanticZones ? BakeMapSemantics() : 0;

            Debug.Log($"[MAP] Bake Complete: {gridWidth}x{gridHeight} grid. SemanticZones={semanticZoneCount}");
            return _mapData;
        }

        private void ResolveGridBakeArea(
            float cellSize,
            out int gridWidth,
            out int gridHeight,
            out Vector3 origin)
        {
            if (FitGridToGroundBounds &&
                TryResolvePlayableGroundBounds(out Bounds groundBounds))
            {
                float padding = Mathf.Max(0f, GroundBoundsPadding);
                float minX = Mathf.Floor((groundBounds.min.x - padding) / cellSize) * cellSize;
                float maxX = Mathf.Ceil((groundBounds.max.x + padding) / cellSize) * cellSize;
                float minZ = Mathf.Floor((groundBounds.min.z - padding) / cellSize) * cellSize;
                float maxZ = Mathf.Ceil((groundBounds.max.z + padding) / cellSize) * cellSize;

                gridWidth = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / cellSize));
                gridHeight = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / cellSize));
                origin = new Vector3(minX, transform.position.y, minZ);
                return;
            }

            gridWidth = Mathf.Max(1, Width);
            gridHeight = Mathf.Max(1, Height);
            origin = transform.position - new Vector3(
                gridWidth * cellSize * 0.5f,
                0f,
                gridHeight * cellSize * 0.5f);
        }

        private bool TryResolvePlayableGroundBounds(out Bounds bounds)
        {
            bounds = default;

            MapLoader mapLoader = FindObjectOfType<MapLoader>();
            if (mapLoader == null || mapLoader.SpawnedMapInstance == null)
                return false;

            return TryCollectGroundBounds(mapLoader.SpawnedMapInstance, out bounds);
        }

        private bool TryCollectGroundBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            bool found = false;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.isTrigger ||
                    !IsGroundBoundsCandidate(collider.gameObject))
                {
                    continue;
                }

                EncapsulateBounds(collider.bounds, ref bounds, ref found);
            }

            if (found)
                return true;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !IsGroundBoundsCandidate(renderer.gameObject))
                {
                    continue;
                }

                EncapsulateBounds(renderer.bounds, ref bounds, ref found);
            }

            return found;
        }

        private bool IsGroundBoundsCandidate(GameObject candidate)
        {
            if (candidate == null)
                return false;

            int layerMask = 1 << candidate.layer;
            if ((ResolveGroundBoundsExclusionMask() & layerMask) != 0)
                return false;

            return GroundLayer.value == 0 ||
                   (GroundLayer.value & layerMask) != 0;
        }

        private int ResolveGroundBoundsExclusionMask()
        {
            return ObstacleLayer.value | ResolveBushMask();
        }

        private static void EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool found)
        {
            if (!found)
            {
                bounds = candidate;
                found = true;
            }
            else
            {
                bounds.Encapsulate(candidate);
            }
        }

        private Vector3 GetObstacleProbeExtents()
        {
            float cellSize = _mapData != null ? _mapData.CellSize : Mathf.Max(0.05f, CellSize);
            float baseExtent = cellSize / 2.1f;
            float agentRadius = AgentRadius > 0f ? AgentRadius : 0.5f;
            float obstacleClearance = AgentObstacleClearance > 0f ? AgentObstacleClearance : 0.1f;
            float clearanceExtent = agentRadius + obstacleClearance;
            float horizontalExtent = Mathf.Max(baseExtent, clearanceExtent);
            return new Vector3(horizontalExtent, 0.5f, horizontalExtent);
        }

        private Vector3 GetBushProbeExtents()
        {
            float cellSize = _mapData != null ? _mapData.CellSize : Mathf.Max(0.05f, CellSize);
            return new Vector3(cellSize / 2.1f, 0.5f, cellSize / 2.1f);
        }

        private int ResolveGroundMask()
        {
            if (GroundLayer.value != 0)
                return GroundLayer.value;

            return Physics.DefaultRaycastLayers & ~ObstacleLayer.value;
        }

        private int ResolveBushMask()
        {
            if (BushLayer.value != 0)
                return BushLayer.value;

            int bushesLayer = LayerMask.NameToLayer("Bushes");
            if (bushesLayer >= 0)
                return 1 << bushesLayer;

            int bushLayer = LayerMask.NameToLayer("Bush");
            return bushLayer >= 0 ? 1 << bushLayer : 0;
        }

        private bool HasPlayableGround(Vector3 worldPos, int groundMask)
        {
            if (groundMask == 0)
                return false;

            Vector3 probeOrigin = worldPos + Vector3.up * Mathf.Max(0.1f, GroundProbeHeight);
            float probeDistance = Mathf.Max(0.1f, GroundProbeDistance);
            return Physics.Raycast(
                probeOrigin,
                Vector3.down,
                probeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
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

                for (int x = 0; x < _mapData.Width; x++)
                {
                    for (int y = 0; y < _mapData.Height; y++)
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
            float cellSize = _mapData != null ? _mapData.CellSize : Mathf.Max(0.05f, CellSize);
            Vector3 origin = _mapData != null
                ? _mapData.Origin
                : transform.position - new Vector3(Width * cellSize * 0.5f, 0f, Height * cellSize * 0.5f);
            return origin + new Vector3(x * cellSize + cellSize * 0.5f, 0f, y * cellSize + cellSize * 0.5f);
        }

        // Helper to convert Brawler World Pos -> Grid Coords for A*
        public Vector2Int GetGridCoords(Vector3 worldPos)
        {
            float cellSize = _mapData != null ? _mapData.CellSize : Mathf.Max(0.05f, CellSize);
            Vector3 origin = _mapData != null
                ? _mapData.Origin
                : transform.position - new Vector3(Width * cellSize * 0.5f, 0f, Height * cellSize * 0.5f);
            int x = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
            int y = Mathf.FloorToInt((worldPos.z - origin.z) / cellSize);
            return new Vector2Int(x, y);
        }

        private void OnDrawGizmos()
        {
            if (!ShowDebugGrid || _mapData == null) return;

            for (int x = 0; x < _mapData.Width; x++)
            {
                for (int y = 0; y < _mapData.Height; y++)
                {
                    Gizmos.color = _mapData.WalkabilityGrid[x, y] ? Color.green : Color.red;
                    Gizmos.DrawWireCube(GetWorldPos(x, y), new Vector3(_mapData.CellSize, 0.1f, _mapData.CellSize));
                }
            }
        }
    }
}
