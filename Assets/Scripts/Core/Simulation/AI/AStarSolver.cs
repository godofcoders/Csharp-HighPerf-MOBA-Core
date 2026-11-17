using System;
using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public class AStarSolver
    {
        private readonly PathNode[] _grid;
        private readonly MapData _mapData;
        private readonly int _width, _height;
        private readonly UnityEngine.Vector3 _origin;
        private readonly float _cellSize;
        private readonly List<int> _touchedNodes = new List<int>(256);
        private readonly PathNodeMinHeap _openHeap;

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

            _grid = new PathNode[walkableMap.Length];
            _openHeap = new PathNodeMinHeap(_grid, 256);

            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                    _grid[GetCellIndex(x, y)] = new PathNode(x, y, walkableMap[x, y]);
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

        internal bool TryFindPathNonAlloc(
            int startX,
            int startY,
            int endX,
            int endY,
            List<PathNode> results,
            bool requireNavigationClearance,
            int boundaryClearanceCells = 1,
            int obstacleClearanceCells = 1)
        {
            results.Clear();
            PathNode endNode = FindEndNode(
                startX, startY, endX, endY, requireNavigationClearance,
                boundaryClearanceCells, obstacleClearanceCells);
            if (endNode == null)
                return false;

            RetracePath(GetNode(startX, startY), endNode, results);
            return true;
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
            _openHeap.Clear();

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

            int startIndex = GetCellIndex(startX, startY);
            TouchNode(startIndex);
            startNode.GCost = 0;
            startNode.HCost = GetDistance(startNode, endNode);

            _openHeap.Push(startIndex);

            Span<int> neighbors = stackalloc int[8];
            while (_openHeap.Count > 0)
            {
                PathNode current = _grid[_openHeap.Pop()];

                if (current == endNode)
                {
                    RecordPathQuery(true);
                    return endNode;
                }

                current.IsClosed = true;

                int neighborCount = CollectNeighborIndices(
                    current,
                    neighbors,
                    requireNavigationClearance,
                    boundaryClearanceCells,
                    obstacleClearanceCells);
                for (int i = 0; i < neighborCount; i++)
                {
                    int neighborIndex = neighbors[i];
                    PathNode neighbor = _grid[neighborIndex];
                    if (!IsTraversalNodeValid(
                            neighbor,
                            requireNavigationClearance,
                            boundaryClearanceCells,
                            obstacleClearanceCells) ||
                        neighbor.IsClosed)
                    {
                        continue;
                    }

                    int newCostToNeighbor = current.GCost + GetDistance(current, neighbor);
                    bool alreadyOpen = neighbor.IsOpen;
                    if (newCostToNeighbor < neighbor.GCost || !alreadyOpen)
                    {
                        TouchNode(neighborIndex);
                        neighbor.GCost = newCostToNeighbor;
                        neighbor.HCost = GetDistance(neighbor, endNode);
                        neighbor.Parent = current;
                        if (!alreadyOpen)
                            _openHeap.Push(neighborIndex);
                        else
                            _openHeap.DecreaseKey(neighbor);
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

        private int CollectNeighborIndices(
            PathNode node,
            Span<int> results,
            bool requireNavigationClearance,
            int boundaryClearanceCells,
            int obstacleClearanceCells)
        {
            int count = 0;

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

                    int neighborX = node.X + x;
                    int neighborY = node.Y + y;
                    if (neighborX >= 0 && neighborX < _width && neighborY >= 0 && neighborY < _height)
                        results[count++] = GetCellIndex(neighborX, neighborY);
                }
            }
            return count;
        }

        private int GetCellIndex(int x, int y) => x * _height + y;

        private PathNode GetNode(int x, int y) =>
            x >= 0 && x < _width && y >= 0 && y < _height
                ? _grid[GetCellIndex(x, y)]
                : null;

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
            RetracePath(start, end, path);
            return path;
        }

        private static void RetracePath(PathNode start, PathNode end, List<PathNode> path)
        {
            for (PathNode node = end; node != start; node = node.Parent)
                path.Add(node);

            path.Reverse();
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
                PathNode node = _grid[_touchedNodes[i]];
                node.GCost = int.MaxValue;
                node.HCost = 0;
                node.Parent = null;
                node.IsPathTouched = false;
                node.IsOpen = false;
                node.IsClosed = false;
                node.OpenHeapIndex = -1;
                node.OpenOrder = 0;
            }

            _touchedNodes.Clear();
        }

        private void TouchNode(int cellIndex)
        {
            PathNode node = _grid[cellIndex];
            if (node.IsPathTouched)
                return;

            node.IsPathTouched = true;
            _touchedNodes.Add(cellIndex);
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

        /// <summary>
        /// Reusable binary min-heap for A*'s open set. The previous list-based
        /// implementation scanned every open node and then called Contains for
        /// every neighbour, turning a busy path query into O(n²) work. This
        /// keeps the same deterministic F-cost ordering while making pop and
        /// decrease-key O(log n), with no per-query allocations after warm-up.
        /// </summary>
        private sealed class PathNodeMinHeap
        {
            private readonly PathNode[] _grid;
            private readonly List<int> _cells;
            private int _nextOrder;

            public int Count => _cells.Count;

            public PathNodeMinHeap(PathNode[] grid, int capacity)
            {
                _grid = grid;
                _cells = new List<int>(capacity);
            }

            public void Clear()
            {
                _cells.Clear();
                _nextOrder = 0;
            }

            public void Push(int cellIndex)
            {
                PathNode node = _grid[cellIndex];
                node.IsOpen = true;
                node.OpenHeapIndex = _cells.Count;
                node.OpenOrder = _nextOrder++;
                _cells.Add(cellIndex);
                SiftUp(node.OpenHeapIndex);
            }

            public int Pop()
            {
                int cellIndex = _cells[0];
                PathNode result = _grid[cellIndex];
                int lastIndex = _cells.Count - 1;
                result.IsOpen = false;
                result.OpenHeapIndex = -1;

                if (lastIndex == 0)
                {
                    _cells.RemoveAt(0);
                    return cellIndex;
                }

                int last = _cells[lastIndex];
                _cells[0] = last;
                _grid[last].OpenHeapIndex = 0;
                _cells.RemoveAt(lastIndex);
                SiftDown(0);
                return cellIndex;
            }

            public void DecreaseKey(PathNode node)
            {
                if (node.OpenHeapIndex >= 0)
                    SiftUp(node.OpenHeapIndex);
            }

            private void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!ComesBefore(_cells[index], _cells[parent]))
                        break;

                    Swap(index, parent);
                    index = parent;
                }
            }

            private void SiftDown(int index)
            {
                while (true)
                {
                    int left = index * 2 + 1;
                    if (left >= _cells.Count)
                        return;

                    int right = left + 1;
                    int best = left;
                    if (right < _cells.Count && ComesBefore(_cells[right], _cells[left]))
                        best = right;

                    if (!ComesBefore(_cells[best], _cells[index]))
                        return;

                    Swap(index, best);
                    index = best;
                }
            }

            private void Swap(int left, int right)
            {
                int temp = _cells[left];
                _cells[left] = _cells[right];
                _cells[right] = temp;
                _grid[_cells[left]].OpenHeapIndex = left;
                _grid[_cells[right]].OpenHeapIndex = right;
            }

            private bool ComesBefore(int leftIndex, int rightIndex)
            {
                PathNode left = _grid[leftIndex];
                PathNode right = _grid[rightIndex];
                if (left.FCost != right.FCost)
                    return left.FCost < right.FCost;

                // The old list scan retained insertion order for equal FCost
                // nodes. Preserve that tie-breaker for deterministic paths.
                return left.OpenOrder < right.OpenOrder;
            }
        }
    }
}
