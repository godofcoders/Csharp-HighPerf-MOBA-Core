using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using UnityEngine;

public class SimulationClock : MonoBehaviour, ISimulationClock
{
    private TickProcessor _processor;
    private CombatEventRouter _combatEventRouter;
    private SimulationRegistry _registry;
    private VisibilityTickSystem _visibilityTickSystem;
    public const int TicksPerSecond = SimulationTiming.TicksPerSecond;
    public const float TickDeltaTime = SimulationTiming.TickDeltaSeconds;

    /// <summary>
    /// Compatibility entry point for older callers. New simulation code should
    /// use <see cref="SimulationTiming.SecondsToTicks"/> directly so timing does
    /// not depend on the Unity-facing clock component.
    /// </summary>
    public static uint SecondsToTicks(float seconds)
    {
        return SimulationTiming.SecondsToTicks(seconds);
    }
    public static SimulationRegistry Registry { get; private set; }
    public static SpatialGrid Grid { get; private set; }
    public static AStarSolver Pathfinder { get; private set; }

    public uint CurrentTick => _processor?.CurrentTick ?? 0;
    public float TickDelta => SimulationTiming.TickDeltaSeconds;

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

        _processor = new TickProcessor(SimulationTiming.TicksPerSecond);
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
            Pathfinder = new AStarSolver(data);
            _visibilityTickSystem = new VisibilityTickSystem(data);
            _registry.Register(_visibilityTickSystem, TickPhase.PreTick);
        }
    }

    private void OnDestroy()
    {
        if (_registry != null && _visibilityTickSystem != null)
            _registry.Unregister(_visibilityTickSystem, TickPhase.PreTick);

        if (_instance == this)
            _instance = null;
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
