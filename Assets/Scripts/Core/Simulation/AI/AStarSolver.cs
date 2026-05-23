using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class AStarSolver
    {
        private PathNode[,] _grid;
        private MapData _mapData;
        private int _width, _height;
        private UnityEngine.Vector3 _origin;
        private float _cellSize;
        private readonly List<PathNode> _touchedNodes = new List<PathNode>(256);
        private readonly List<PathNode> _neighborBuffer = new List<PathNode>(8);

        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        public UnityEngine.Vector3 Origin => _origin;

        public AStarSolver(MapData mapData)
            : this(mapData.WalkabilityGrid, mapData.CellSize, mapData.Origin)
        {
            _mapData = mapData;
        }

        public AStarSolver(bool[,] walkableMap, float cellSize, UnityEngine.Vector3 origin)
        {
            _width = walkableMap.GetLength(0);
            _height = walkableMap.GetLength(1);
            _cellSize = cellSize;
            _origin = origin;

            _grid = new PathNode[_width, _height];

            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                    _grid[x, y] = new PathNode(x, y, walkableMap[x, y]);
        }

        public UnityEngine.Vector2Int GetGridCoords(UnityEngine.Vector3 worldPos)
        {
            int x = UnityEngine.Mathf.FloorToInt((worldPos.x - _origin.x) / _cellSize);
            int y = UnityEngine.Mathf.FloorToInt((worldPos.z - _origin.z) / _cellSize);

            x = UnityEngine.Mathf.Clamp(x, 0, _width - 1);
            y = UnityEngine.Mathf.Clamp(y, 0, _height - 1);

            return new UnityEngine.Vector2Int(x, y);
        }

        public UnityEngine.Vector3 GetWorldPos(int x, int y)
        {
            float worldX = _origin.x + (x * _cellSize) + (_cellSize / 2f);
            float worldZ = _origin.z + (y * _cellSize) + (_cellSize / 2f);

            return new UnityEngine.Vector3(worldX, 0, worldZ);
        }

        public UnityEngine.Vector3 GetWorldPos(UnityEngine.Vector2Int coords)
        {
            return GetWorldPos(coords.x, coords.y);
        }

        public bool IsInBounds(UnityEngine.Vector2Int coords)
        {
            return coords.x >= 0 && coords.y >= 0 && coords.x < _width && coords.y < _height;
        }

        public bool IsWalkable(UnityEngine.Vector2Int coords)
        {
            PathNode node = GetNode(coords.x, coords.y);
            return node != null && node.IsWalkable;
        }

        public bool IsBush(UnityEngine.Vector2Int coords)
        {
            return _mapData != null && _mapData.IsBush(coords);
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

        public UnityEngine.Vector3 GetNearestWalkableWorldPos(UnityEngine.Vector3 worldPos, int searchRadius = 3)
        {
            UnityEngine.Vector2Int center = GetGridCoords(worldPos);

            return TryGetNearestWalkableCoords(center, searchRadius, out UnityEngine.Vector2Int best)
                ? GetWorldPos(best)
                : worldPos;
        }

        public bool TryGetNearestWalkableCoords(
            UnityEngine.Vector2Int center,
            int searchRadius,
            out UnityEngine.Vector2Int result)
        {
            if (IsWalkable(center))
            {
                result = center;
                return true;
            }

            int radius = UnityEngine.Mathf.Max(1, searchRadius);
            float bestDistanceSq = float.MaxValue;
            result = center;
            bool found = false;

            for (int r = 1; r <= radius; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        if (UnityEngine.Mathf.Abs(x) != r && UnityEngine.Mathf.Abs(y) != r)
                            continue;

                        UnityEngine.Vector2Int coords = new UnityEngine.Vector2Int(center.x + x, center.y + y);
                        if (!IsWalkable(coords))
                            continue;

                        int dx = coords.x - center.x;
                        int dy = coords.y - center.y;
                        float distanceSq = (dx * dx) + (dy * dy);

                        if (!found || distanceSq < bestDistanceSq)
                        {
                            found = true;
                            bestDistanceSq = distanceSq;
                            result = coords;
                        }
                    }
                }

                if (found)
                    break;
            }

            return found;
        }

        public List<PathNode> FindPath(int startX, int startY, int endX, int endY)
        {
            ResetPathState();

            PathNode startNode = GetNode(startX, startY);
            PathNode endNode = GetNode(endX, endY);

            if (startNode == null || endNode == null || !startNode.IsWalkable || !endNode.IsWalkable) return null;

            TouchNode(startNode);
            startNode.GCost = 0;
            startNode.HCost = GetDistance(startNode, endNode);

            List<PathNode> openList = new List<PathNode> { startNode };
            HashSet<PathNode> closedList = new HashSet<PathNode>();

            while (openList.Count > 0)
            {
                // Find node with lowest F cost
                PathNode current = openList[0];
                for (int i = 1; i < openList.Count; i++)
                    if (openList[i].FCost < current.FCost) current = openList[i];

                if (current == endNode) return RetracePath(startNode, endNode);

                openList.Remove(current);
                closedList.Add(current);

                GetNeighborsNonAlloc(current, _neighborBuffer);
                for (int i = 0; i < _neighborBuffer.Count; i++)
                {
                    PathNode neighbor = _neighborBuffer[i];
                    if (!neighbor.IsWalkable || closedList.Contains(neighbor)) continue;

                    int newCostToNeighbor = current.GCost + GetDistance(current, neighbor);
                    if (newCostToNeighbor < neighbor.GCost || !openList.Contains(neighbor))
                    {
                        TouchNode(neighbor);
                        neighbor.GCost = newCostToNeighbor;
                        neighbor.HCost = GetDistance(neighbor, endNode);
                        neighbor.Parent = current;
                        if (!openList.Contains(neighbor)) openList.Add(neighbor);
                    }
                }
            }
            return null;
        }

        private int GetDistance(PathNode a, PathNode b)
        {
            int distX = Mathf.Abs(a.X - b.X);
            int distY = Mathf.Abs(a.Y - b.Y);
            return (distX > distY) ? 14 * distY + 10 * (distX - distY) : 14 * distX + 10 * (distY - distX);
        }

        private void GetNeighborsNonAlloc(PathNode node, List<PathNode> results)
        {
            results.Clear();

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    if (x != 0 && y != 0)
                    {
                        PathNode horizontal = GetNode(node.X + x, node.Y);
                        PathNode vertical = GetNode(node.X, node.Y + y);

                        if (horizontal == null || vertical == null || !horizontal.IsWalkable || !vertical.IsWalkable)
                            continue;
                    }

                    PathNode n = GetNode(node.X + x, node.Y + y);
                    if (n != null) results.Add(n);
                }
            }
        }

        private PathNode GetNode(int x, int y) => (x >= 0 && x < _width && y >= 0 && y < _height) ? _grid[x, y] : null;

        private List<PathNode> RetracePath(PathNode start, PathNode end)
        {
            List<PathNode> path = new List<PathNode>();
            PathNode curr = end;
            while (curr != start) { path.Add(curr); curr = curr.Parent; }
            path.Reverse();
            return path;
        }

        private void ResetPathState()
        {
            for (int i = 0; i < _touchedNodes.Count; i++)
            {
                PathNode node = _touchedNodes[i];
                node.GCost = int.MaxValue;
                node.HCost = 0;
                node.Parent = null;
                node.IsPathTouched = false;
            }

            _touchedNodes.Clear();
        }

        private void TouchNode(PathNode node)
        {
            if (node == null || node.IsPathTouched)
                return;

            node.IsPathTouched = true;
            _touchedNodes.Add(node);
        }
    }
}
