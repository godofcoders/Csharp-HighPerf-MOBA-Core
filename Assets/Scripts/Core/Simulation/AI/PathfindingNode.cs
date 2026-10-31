namespace MOBA.Core.Simulation.AI
{
    public class PathNode
    {
        public int X, Y;
        public bool IsWalkable;

        // A* Heuristics
        public int GCost; // Distance from start
        public int HCost; // Distance to end
        public int FCost => GCost + HCost;

        public PathNode Parent;
        public bool IsPathTouched;

        // Search-local state used by AStarSolver's reusable open heap.
        public bool IsOpen;
        public bool IsClosed;
        public int OpenHeapIndex = -1;
        public int OpenOrder;

        public PathNode(int x, int y, bool walkable)
        {
            X = x;
            Y = y;
            IsWalkable = walkable;
            GCost = int.MaxValue;
        }
    }
}
