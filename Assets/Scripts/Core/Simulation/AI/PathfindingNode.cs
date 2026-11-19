namespace MOBA.Core.Simulation.AI
{
    public class PathNode
    {
        private readonly int _cellIndex;
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
        private int[] _openHeapIndex;
        private int _openHeapIndexValue = -1;

        public int OpenHeapIndex
        {
            get => _openHeapIndex != null ? _openHeapIndex[_cellIndex] : _openHeapIndexValue;
            set
            {
                if (_openHeapIndex != null)
                    _openHeapIndex[_cellIndex] = value;
                else
                    _openHeapIndexValue = value;
            }
        }
        private int[] _openOrder;
        private int _openOrderValue = 0;

        public int OpenOrder
        {
            get => _openOrder != null ? _openOrder[_cellIndex] : _openOrderValue;
            set
            {
                if (_openOrder != null)
                    _openOrder[_cellIndex] = value;
                else
                    _openOrderValue = value;
            }
        }

        internal PathNode(int x, int y, bool walkable, int cellIndex, int[] heapIndices, int[] openOrders)
            : this(x, y, walkable)
        {
            _cellIndex = cellIndex;
            _openHeapIndex = heapIndices;
            _openOrder = openOrders;
            OpenHeapIndex = -1;
        }

        public PathNode(int x, int y, bool walkable)
        {
            X = x;
            Y = y;
            IsWalkable = walkable;
            GCost = int.MaxValue;
        }
    }
}
