using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class AStarSolver
    {
        private PathNode[,] _grid;
        private int _width, _height;

        public AStarSolver(bool[,] walkableMap)
        {
            _width = walkableMap.GetLength(0);
            _height = walkableMap.GetLength(1);
            _grid = new PathNode[_width, _height];

            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                    _grid[x, y] = new PathNode(x, y, walkableMap[x, y]);
        }

        public List<PathNode> FindPath(int startX, int startY, int endX, int endY)
        {
            PathNode startNode = GetNode(startX, startY);
            PathNode endNode = GetNode(endX, endY);

            if (startNode == null || endNode == null || !endNode.IsWalkable) return null;

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

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (!neighbor.IsWalkable || closedList.Contains(neighbor)) continue;

                    int newCostToNeighbor = current.GCost + GetDistance(current, neighbor);
                    if (newCostToNeighbor < neighbor.GCost || !openList.Contains(neighbor))
                    {
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

        private List<PathNode> GetNeighbors(PathNode node)
        {
            List<PathNode> neighbors = new List<PathNode>();
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    PathNode n = GetNode(node.X + x, node.Y + y);
                    if (n != null) neighbors.Add(n);
                }
            return neighbors;
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
    }
}