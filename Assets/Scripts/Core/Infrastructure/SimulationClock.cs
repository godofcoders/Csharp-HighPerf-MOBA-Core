using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using UnityEngine;

public class SimulationClock : MonoBehaviour
{
    [SerializeField] private int _ticksPerSecond = 30;

    private TickProcessor _processor;
    private SimulationRegistry _registry;
    public const float TickDeltaTime = 1f / 30f;
    public static SimulationRegistry Registry { get; private set; }
    public static SpatialGrid Grid { get; private set; }
    public static uint CurrentTick => _instance != null && _instance._processor != null
                ? _instance._processor.CurrentTick
                : 0;

    private static SimulationClock _instance;
    public static AStarSolver Pathfinder { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;

        _processor = new TickProcessor(_ticksPerSecond);
        _registry = new SimulationRegistry();
        Registry = _registry;
        Grid = new SpatialGrid(4f);
    }

    [System.Obsolete]
    private void Start() // Use Start to ensure MapGenerator is ready
    {
        var generator = FindObjectOfType<MapGenerator>();
        if (generator != null)
        {
            var data = generator.BakeMap();
            Pathfinder = new AStarSolver(data.WalkabilityGrid);
        }
    }

    private void Update()
    {
        if (_processor == null || _registry == null) return;

        int ticks = _processor.Update(Time.deltaTime);
        for (int i = 0; i < ticks; i++)
        {
            _registry.TickAll(_processor.CurrentTick);
        }
    }
}