using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class MapData
    {
        public bool[,] WalkabilityGrid;
        public bool[,] BushGrid;
        public AIMapSemanticCell[,] SemanticGrid;
        private readonly List<AIMapSemanticZoneInfo> _semanticZones =
            new List<AIMapSemanticZoneInfo>(16);

        public float CellSize;
        public Vector3 Origin;
        public int Width => WalkabilityGrid != null ? WalkabilityGrid.GetLength(0) : 0;
        public int Height => WalkabilityGrid != null ? WalkabilityGrid.GetLength(1) : 0;
        public int SemanticZoneCount => _semanticZones.Count;

        public MapData(int width, int height, float cellSize, Vector3 origin)
        {
            WalkabilityGrid = new bool[width, height];
            BushGrid = new bool[width, height];
            SemanticGrid = new AIMapSemanticCell[width, height];
            CellSize = cellSize;
            Origin = origin;
        }

        public bool IsInBounds(Vector2Int coords)
        {
            return coords.x >= 0 &&
                   coords.y >= 0 &&
                   coords.x < Width &&
                   coords.y < Height;
        }

        public Vector2Int GetGridCoords(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - Origin.x) / CellSize);
            int y = Mathf.FloorToInt((worldPos.z - Origin.z) / CellSize);

            x = Mathf.Clamp(x, 0, Mathf.Max(0, Width - 1));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, Height - 1));

            return new Vector2Int(x, y);
        }

        public Vector3 GetWorldPos(int x, int y)
        {
            return new Vector3(
                Origin.x + (x * CellSize) + (CellSize * 0.5f),
                0f,
                Origin.z + (y * CellSize) + (CellSize * 0.5f));
        }

        public Vector3 GetWorldPos(Vector2Int coords)
        {
            return GetWorldPos(coords.x, coords.y);
        }

        public bool IsWalkable(Vector2Int coords)
        {
            return IsInBounds(coords) && WalkabilityGrid[coords.x, coords.y];
        }

        public bool IsBush(Vector2Int coords)
        {
            return IsInBounds(coords) &&
                   BushGrid != null &&
                   BushGrid[coords.x, coords.y];
        }

        public int CountWalkableNeighbors(Vector2Int coords)
        {
            int count = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    if (IsWalkable(new Vector2Int(coords.x + x, coords.y + y)))
                        count++;
                }
            }

            return count;
        }

        public int CountBlockedNeighbors(Vector2Int coords)
        {
            int count = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Vector2Int neighbor = new Vector2Int(coords.x + x, coords.y + y);
                    if (!IsInBounds(neighbor) || !IsWalkable(neighbor))
                        count++;
                }
            }

            return count;
        }

        public bool IsNearObstacle(Vector2Int coords)
        {
            return CountBlockedNeighbors(coords) > 0 ||
                   HasSemanticTag(coords, AIMapSemanticTag.CoverCluster);
        }

        public bool IsChokepoint(Vector2Int coords)
        {
            if (!IsWalkable(coords))
                return true;

            return HasSemanticTag(coords, AIMapSemanticTag.Choke) ||
                   CountWalkableNeighbors(coords) <= 3 ||
                   CountBlockedNeighbors(coords) >= 5;
        }

        public int RegisterSemanticZone(
            string zoneName,
            AIMapSemanticTag tags,
            AITeamLaneAssignment lane,
            float influence)
        {
            int id = _semanticZones.Count;
            _semanticZones.Add(new AIMapSemanticZoneInfo(
                id,
                zoneName,
                tags,
                lane,
                influence));

            return id;
        }

        public void ApplySemanticZone(
            Vector2Int coords,
            int zoneId,
            AIMapSemanticTag tags,
            AITeamLaneAssignment lane,
            float influence)
        {
            if (!IsInBounds(coords) || tags == AIMapSemanticTag.None)
                return;

            AIMapSemanticCell cell = SemanticGrid[coords.x, coords.y];
            float clampedInfluence = Mathf.Max(0f, influence);
            bool hadSemantic = cell.HasAny;

            cell.Tags |= tags;

            if (lane != AITeamLaneAssignment.None)
                cell.Lane = lane;

            if (!hadSemantic || clampedInfluence >= cell.Influence)
            {
                cell.PrimaryZoneId = zoneId;
                cell.Influence = clampedInfluence;
            }

            SemanticGrid[coords.x, coords.y] = cell;
        }

        public AIMapSemanticCell GetSemanticCell(Vector2Int coords)
        {
            return IsInBounds(coords) && SemanticGrid != null
                ? SemanticGrid[coords.x, coords.y]
                : default;
        }

        public bool HasSemanticTag(Vector2Int coords, AIMapSemanticTag tag)
        {
            return GetSemanticCell(coords).HasTag(tag);
        }

        public string GetSemanticZoneName(Vector2Int coords)
        {
            AIMapSemanticCell cell = GetSemanticCell(coords);
            if (!cell.HasAny)
                return string.Empty;

            return GetSemanticZoneName(cell.PrimaryZoneId);
        }

        public string GetSemanticZoneName(int zoneId)
        {
            return zoneId >= 0 && zoneId < _semanticZones.Count
                ? _semanticZones[zoneId].Name
                : string.Empty;
        }

        public string GetSemanticSummary(Vector2Int coords)
        {
            AIMapSemanticCell cell = GetSemanticCell(coords);
            if (!cell.HasAny)
                return "none";

            string zoneName = GetSemanticZoneName(cell.PrimaryZoneId);
            string lane = cell.Lane != AITeamLaneAssignment.None
                ? $" lane={cell.Lane}"
                : string.Empty;

            return string.IsNullOrEmpty(zoneName)
                ? $"{cell.Tags}{lane}"
                : $"{zoneName}:{cell.Tags}{lane}";
        }
    }
}
