using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class SpatialGrid
    {
        private readonly float _cellSize;
        // Map: Cell Coordinate -> List of Entities in that cell
        private readonly Dictionary<Vector2Int, List<ISpatialEntity>> _cells = new Dictionary<Vector2Int, List<ISpatialEntity>>();

        public SpatialGrid(float cellSize) => _cellSize = cellSize;

        // Converts a 3D world position into a 2D Grid Coordinate
        private Vector2Int GetCellCoords(Vector3 position) => new Vector2Int(
            Mathf.FloorToInt(position.x / _cellSize),
            Mathf.FloorToInt(position.z / _cellSize)
        );

        public void Add(ISpatialEntity entity)
        {
            var cell = GetCellCoords(entity.Position);
            if (!_cells.ContainsKey(cell)) _cells[cell] = new List<ISpatialEntity>();
            _cells[cell].Add(entity);
        }

        public void Remove(ISpatialEntity entity, Vector3 lastKnownPos)
        {
            var cell = GetCellCoords(lastKnownPos);
            if (_cells.TryGetValue(cell, out var list))
            {
                list.Remove(entity);
            }
        }

        public void UpdateEntity(ISpatialEntity entity, Vector3 oldPos, Vector3 newPos)
        {
            Vector2Int oldCell = GetCellCoords(oldPos);
            Vector2Int newCell = GetCellCoords(newPos);

            if (oldCell != newCell)
            {
                Remove(entity, oldPos);
                Add(entity);
            }
        }

        public List<ISpatialEntity> GetEntitiesInCell(Vector3 position)
        {
            return _cells.TryGetValue(GetCellCoords(position), out var list) ? list : null;
        }

        public ISpatialEntity CheckCollision(Vector3 position, float radius, TeamType attackerTeam)
        {
            Vector2Int cell = GetCellCoords(position);
            float sqrRadius = radius * radius;

            // Check current cell and immediate neighbors for high precision
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Vector2Int neighbor = cell + new Vector2Int(x, y);
                    if (_cells.TryGetValue(neighbor, out var entities))
                    {
                        for (int i = 0; i < entities.Count; i++)
                        {
                            var target = entities[i];

                            // 1. Team Check: Don't hit teammates
                            if (target.Team == attackerTeam) continue;

                            // 2. Distance Check: circle vs point/circle
                            float distSq = (target.Position - position).sqrMagnitude;

                            // We add the target's collision radius for better feel
                            float combinedRadius = radius + target.CollisionRadius;
                            if (distSq <= (combinedRadius * combinedRadius))
                            {
                                return target;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public List<ISpatialEntity> GetEntitiesInRadius(Vector3 position, float radius)
        {
            List<ISpatialEntity> results = new List<ISpatialEntity>();
            Vector2Int centerCell = GetCellCoords(position);
            int cellRange = Mathf.CeilToInt(radius / _cellSize);

            for (int x = -cellRange; x <= cellRange; x++)
            {
                for (int y = -cellRange; y <= cellRange; y++)
                {
                    if (_cells.TryGetValue(centerCell + new Vector2Int(x, y), out var entities))
                    {
                        results.AddRange(entities);
                    }
                }
            }
            return results;
        }
    }
}