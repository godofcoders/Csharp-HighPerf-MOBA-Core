using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

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
        private readonly List<PathNode> _openList = new List<PathNode>(256);
        private readonly HashSet<PathNode> _closedSet = new HashSet<PathNode>();

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

        public bool TrySetWalkable(UnityEngine.Vector2Int coords, bool walkable)
        {
            PathNode node = GetNode(coords.x, coords.y);
            if (node == null)
                return false;

            node.IsWalkable = walkable;

            if (_mapData != null &&
                _mapData.WalkabilityGrid != null &&
                _mapData.IsInBounds(coords))
            {
                _mapData.WalkabilityGrid[coords.x, coords.y] = walkable;
            }

            return true;
        }

        public int SetWalkableCircle(UnityEngine.Vector3 worldPos, float radius, bool walkable)
        {
            UnityEngine.Vector2Int center = GetGridCoords(worldPos);
            int cellRadius = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.CeilToInt(radius / _cellSize));
            float radiusSq = radius * radius;
            int changed = 0;

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    UnityEngine.Vector2Int coords = new UnityEngine.Vector2Int(center.x + x, center.y + y);
                    if (!IsInBounds(coords))
                        continue;

                    UnityEngine.Vector3 cellWorld = GetWorldPos(coords);
                    UnityEngine.Vector3 delta = cellWorld - worldPos;
                    delta.y = 0f;

                    if (delta.sqrMagnitude > radiusSq)
                        continue;

                    if (TrySetWalkable(coords, walkable))
                        changed++;
                }
            }

            return changed;
        }

        public bool IsWalkableWithBoundaryClearance(
            UnityEngine.Vector2Int coords,
            int clearanceCells = 1)
        {
            if (!IsWalkable(coords))
                return false;

            int clearance = UnityEngine.Mathf.Max(0, clearanceCells);
            if (clearance == 0)
                return true;

            return coords.x >= clearance &&
                   coords.y >= clearance &&
                   coords.x < _width - clearance &&
                   coords.y < _height - clearance;
        }

        public bool IsWalkableWithNavigationClearance(
            UnityEngine.Vector2Int coords,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            return IsWalkableWithBoundaryClearance(coords, boundaryClearanceCells) &&
                   HasObstacleClearance(coords, obstacleClearanceCells);
        }

        public bool HasObstacleClearance(
            UnityEngine.Vector2Int coords,
            int clearanceCells = 1)
        {
            if (!IsWalkable(coords))
                return false;

            int clearance = UnityEngine.Mathf.Max(0, clearanceCells);
            if (clearance == 0)
                return true;

            for (int x = -clearance; x <= clearance; x++)
            {
                for (int y = -clearance; y <= clearance; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    UnityEngine.Vector2Int neighbor = new UnityEngine.Vector2Int(coords.x + x, coords.y + y);
                    if (!IsInBounds(neighbor) || !IsWalkable(neighbor))
                        return false;
                }
            }

            return true;
        }

        public bool IsBush(UnityEngine.Vector2Int coords)
        {
            return _mapData != null && _mapData.IsBush(coords);
        }

        public AIMapSemanticCell GetSemanticCell(UnityEngine.Vector2Int coords)
        {
            return _mapData != null ? _mapData.GetSemanticCell(coords) : default;
        }

        public bool HasSemanticTag(UnityEngine.Vector2Int coords, AIMapSemanticTag tag)
        {
            return _mapData != null && _mapData.HasSemanticTag(coords, tag);
        }

        public string GetSemanticZoneName(UnityEngine.Vector2Int coords)
        {
            return _mapData != null ? _mapData.GetSemanticZoneName(coords) : string.Empty;
        }

        public string GetSemanticSummary(UnityEngine.Vector2Int coords)
        {
            return _mapData != null ? _mapData.GetSemanticSummary(coords) : "none";
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
            return _mapData != null
                ? _mapData.IsNearObstacle(coords)
                : CountBlockedNeighbors(coords) > 0;
        }

        public bool IsChokepoint(UnityEngine.Vector2Int coords)
        {
            if (_mapData != null)
                return _mapData.IsChokepoint(coords);

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
            return TryGetNearestWalkableCoords(
                center,
                searchRadius,
                requireBoundaryClearance: false,
                out result);
        }

        public bool TryGetNearestWalkableCoordsWithBoundaryClearance(
            UnityEngine.Vector2Int center,
            int searchRadius,
            out UnityEngine.Vector2Int result,
            int clearanceCells = 1)
        {
            if (TryGetNearestWalkableCoords(
                    center,
                    searchRadius,
                    requireBoundaryClearance: true,
                    out result,
                    clearanceCells))
            {
                return true;
            }

            return TryGetNearestWalkableCoords(
                center,
                searchRadius,
                requireBoundaryClearance: false,
                out result);
        }

        public bool TryGetNearestWalkableCoordsWithNavigationClearance(
            UnityEngine.Vector2Int center,
            int searchRadius,
            out UnityEngine.Vector2Int result,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            return TryGetNearestWalkableCoords(
                center,
                searchRadius,
                requireBoundaryClearance: true,
                requireObstacleClearance: true,
                out result,
                boundaryClearanceCells,
                obstacleClearanceCells);
        }

        private bool TryGetNearestWalkableCoords(
            UnityEngine.Vector2Int center,
            int searchRadius,
            bool requireBoundaryClearance,
            out UnityEngine.Vector2Int result,
            int clearanceCells = 1)
        {
            return TryGetNearestWalkableCoords(
                center,
                searchRadius,
                requireBoundaryClearance,
                requireObstacleClearance: false,
                out result,
                clearanceCells,
                obstacleClearanceCells: 0);
        }

        private bool TryGetNearestWalkableCoords(
            UnityEngine.Vector2Int center,
            int searchRadius,
            bool requireBoundaryClearance,
            bool requireObstacleClearance,
            out UnityEngine.Vector2Int result,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            if (IsCandidateWalkable(
                center,
                requireBoundaryClearance,
                requireObstacleClearance,
                boundaryClearanceCells,
                obstacleClearanceCells))
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
                        if (!IsCandidateWalkable(
                            coords,
                            requireBoundaryClearance,
                            requireObstacleClearance,
                            boundaryClearanceCells,
                            obstacleClearanceCells))
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

        private bool IsCandidateWalkable(
            UnityEngine.Vector2Int coords,
            bool requireBoundaryClearance,
            bool requireObstacleClearance,
            int boundaryClearanceCells,
            int obstacleClearanceCells)
        {
            if (requireBoundaryClearance)
            {
                if (!IsWalkableWithBoundaryClearance(coords, boundaryClearanceCells))
                    return false;
            }
            else if (!IsWalkable(coords))
            {
                return false;
            }

            return !requireObstacleClearance ||
                   HasObstacleClearance(coords, obstacleClearanceCells);
        }

        public List<PathNode> FindPath(int startX, int startY, int endX, int endY)
        {
            PathNode endNode = FindEndNode(
                startX,
                startY,
                endX,
                endY,
                requireNavigationClearance: false);
            return endNode != null ? RetracePath(GetNode(startX, startY), endNode) : null;
        }

        public List<PathNode> FindPathWithNavigationClearance(
            int startX,
            int startY,
            int endX,
            int endY,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            PathNode endNode = FindEndNode(
                startX,
                startY,
                endX,
                endY,
                requireNavigationClearance: true,
                boundaryClearanceCells,
                obstacleClearanceCells);
            return endNode != null ? RetracePath(GetNode(startX, startY), endNode) : null;
        }

        public bool TryGetPathLength(
            int startX,
            int startY,
            int endX,
            int endY,
            out int pathLength)
        {
            pathLength = 0;

            PathNode startNode = GetNode(startX, startY);
            PathNode endNode = FindEndNode(
                startX,
                startY,
                endX,
                endY,
                requireNavigationClearance: false);
            if (startNode == null || endNode == null)
                return false;

            pathLength = CountPathLength(startNode, endNode);
            return true;
        }

        public bool TryGetPathLengthWithNavigationClearance(
            int startX,
            int startY,
            int endX,
            int endY,
            out int pathLength,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            pathLength = 0;

            PathNode startNode = GetNode(startX, startY);
            PathNode endNode = FindEndNode(
                startX,
                startY,
                endX,
                endY,
                requireNavigationClearance: true,
                boundaryClearanceCells,
                obstacleClearanceCells);
            if (startNode == null || endNode == null)
                return false;

            pathLength = CountPathLength(startNode, endNode);
            return true;
        }

        private PathNode FindEndNode(
            int startX,
            int startY,
            int endX,
            int endY,
            bool requireNavigationClearance,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            ResetPathState();

            PathNode startNode = GetNode(startX, startY);
            PathNode endNode = GetNode(endX, endY);

            if (!IsTraversalNodeValid(
                    startNode,
                    requireNavigationClearance,
                    boundaryClearanceCells,
                    obstacleClearanceCells) ||
                !IsTraversalNodeValid(
                    endNode,
                    requireNavigationClearance,
                    boundaryClearanceCells,
                    obstacleClearanceCells))
            {
                RecordPathQuery(false);
                return null;
            }

            TouchNode(startNode);
            startNode.GCost = 0;
            startNode.HCost = GetDistance(startNode, endNode);

            _openList.Clear();
            _closedSet.Clear();
            _openList.Add(startNode);

            while (_openList.Count > 0)
            {
                // Find node with lowest F cost
                int currentIndex = 0;
                PathNode current = _openList[0];
                for (int i = 1; i < _openList.Count; i++)
                {
                    if (_openList[i].FCost < current.FCost)
                    {
                        current = _openList[i];
                        currentIndex = i;
                    }
                }

                if (current == endNode)
                {
                    RecordPathQuery(true);
                    return endNode;
                }

                _openList.RemoveAt(currentIndex);
                _closedSet.Add(current);

                GetNeighborsNonAlloc(
                    current,
                    _neighborBuffer,
                    requireNavigationClearance,
                    boundaryClearanceCells,
                    obstacleClearanceCells);
                for (int i = 0; i < _neighborBuffer.Count; i++)
                {
                    PathNode neighbor = _neighborBuffer[i];
                    if (!IsTraversalNodeValid(
                            neighbor,
                            requireNavigationClearance,
                            boundaryClearanceCells,
                            obstacleClearanceCells) ||
                        _closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    int newCostToNeighbor = current.GCost + GetDistance(current, neighbor);
                    bool alreadyOpen = _openList.Contains(neighbor);
                    if (newCostToNeighbor < neighbor.GCost || !alreadyOpen)
                    {
                        TouchNode(neighbor);
                        neighbor.GCost = newCostToNeighbor;
                        neighbor.HCost = GetDistance(neighbor, endNode);
                        neighbor.Parent = current;
                        if (!alreadyOpen) _openList.Add(neighbor);
                    }
                }
            }

            RecordPathQuery(false);
            return null;
        }

        private int GetDistance(PathNode a, PathNode b)
        {
            int distX = Mathf.Abs(a.X - b.X);
            int distY = Mathf.Abs(a.Y - b.Y);
            return (distX > distY) ? 14 * distY + 10 * (distX - distY) : 14 * distX + 10 * (distY - distX);
        }

        private void GetNeighborsNonAlloc(
            PathNode node,
            List<PathNode> results,
            bool requireNavigationClearance,
            int boundaryClearanceCells,
            int obstacleClearanceCells)
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

                        if (!IsTraversalNodeValid(
                                horizontal,
                                requireNavigationClearance,
                                boundaryClearanceCells,
                                obstacleClearanceCells) ||
                            !IsTraversalNodeValid(
                                vertical,
                                requireNavigationClearance,
                                boundaryClearanceCells,
                                obstacleClearanceCells))
                        {
                            continue;
                        }
                    }

                    PathNode n = GetNode(node.X + x, node.Y + y);
                    if (n != null) results.Add(n);
                }
            }
        }

        private PathNode GetNode(int x, int y) => (x >= 0 && x < _width && y >= 0 && y < _height) ? _grid[x, y] : null;

        private bool IsTraversalNodeValid(
            PathNode node,
            bool requireNavigationClearance,
            int boundaryClearanceCells,
            int obstacleClearanceCells)
        {
            if (node == null || !node.IsWalkable)
                return false;

            return !requireNavigationClearance ||
                   IsWalkableWithNavigationClearance(
                       new UnityEngine.Vector2Int(node.X, node.Y),
                       boundaryClearanceCells,
                       obstacleClearanceCells);
        }

        private List<PathNode> RetracePath(PathNode start, PathNode end)
        {
            List<PathNode> path = new List<PathNode>();
            PathNode curr = end;
            while (curr != start) { path.Add(curr); curr = curr.Parent; }
            path.Reverse();
            return path;
        }

        private int CountPathLength(PathNode start, PathNode end)
        {
            int count = 0;
            PathNode curr = end;

            while (curr != null && curr != start)
            {
                count++;
                curr = curr.Parent;
            }

            return count;
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

        private void RecordPathQuery(bool success)
        {
            uint currentTick = 0u;
            if (ServiceProvider.TryGet<ISimulationClock>(out var clock) && clock != null)
                currentTick = clock.CurrentTick;

            AIPerformanceTracker.RecordPathQuery(
                currentTick,
                success,
                _touchedNodes.Count);
        }
    }
}
