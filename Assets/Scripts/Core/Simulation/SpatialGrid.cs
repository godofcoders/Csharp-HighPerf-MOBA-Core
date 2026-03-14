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
    }
}