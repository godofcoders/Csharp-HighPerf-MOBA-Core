using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using UnityEngine;

public class SimulationClock : MonoBehaviour, ISimulationClock
{
    [SerializeField] private int _ticksPerSecond = 30;

    private TickProcessor _processor;
    private CombatEventRouter _combatEventRouter;
    private SimulationRegistry _registry;
    public const float TickDeltaTime = 1f / 30f;
    public static SimulationRegistry Registry { get; private set; }
    public static SpatialGrid Grid { get; private set; }
    public static AStarSolver Pathfinder { get; private set; }

    public uint CurrentTick => _processor?.CurrentTick ?? 0;
    public float TickDelta => 1f / _ticksPerSecond;

    private static SimulationClock _instance;


    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;

        ServiceProvider.Register<ISimulationClock>(this);
        ServiceProvider.Register<IDamageService>(new DamageService());
        _combatEventRouter = new CombatEventRouter();
        ServiceProvider.Register<IStatusEffectService>(new StatusEffectService());
        ServiceProvider.Register<ICombatLogService>(new CombatLogService());

        ServiceProvider.Register<IDeployableRegistry>(new DeployableRegistry());
        ServiceProvider.Register<IDeployableService>(new DeployableService());

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
            Pathfinder = new AStarSolver(data.WalkabilityGrid, data.CellSize, data.Origin);
        }
    }

    private void Update()
    {
        if (_processor == null || _registry == null) return;

        int ticks = _processor.Update(Time.deltaTime);
        for (int i = 0; i < ticks; i++)
        {
            uint tickCount = _processor.CurrentTick;

            // One call — the registry walks every phase in order (PreTick →
            // InputApply → AbilityCast → Movement → Collision → DamageResolution
            // → StatusEffectTick → Cleanup → PostTick) and ticks every registered
            // ITickable in that phase.
            //
            // ProjectileManager is now registered in the Collision phase (used to
            // be driven by a separate call here). Deployables and Brawlers are in
            // Movement. AI controllers are in InputApply.
            _registry.TickAll(tickCount);
        }
    }
}