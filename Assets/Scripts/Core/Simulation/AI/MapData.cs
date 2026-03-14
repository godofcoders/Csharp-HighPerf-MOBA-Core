namespace MOBA.Core.Simulation.AI
{
    public class MapData
    {
        public bool[,] WalkabilityGrid;
        public bool[,] BushGrid; // NEW: Stealth layer
        public float CellSize;
        public UnityEngine.Vector3 Origin;

        public MapData(int width, int height, float cellSize, UnityEngine.Vector3 origin)
        {
            WalkabilityGrid = new bool[width, height];
            BushGrid = new bool[width, height];
            CellSize = cellSize;
            Origin = origin;
        }
    }
}