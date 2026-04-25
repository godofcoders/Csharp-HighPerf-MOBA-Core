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

    /// <summary>
    /// Converts a duration in seconds to an integer tick count, rounded to the
    /// nearest tick. Single source of truth for seconds→ticks conversion across
    /// the entire simulation — gameplay code (cooldowns, status effects,
    /// deployables, hypercharge) MUST go through this helper rather than
    /// open-coding the math, for two reasons:
    ///
    ///  1) IEEE float precision. TickDeltaTime is 1f/30f, which is roughly
    ///     0.033333335f, NOT exactly 1/30. A naive (uint)(seconds / TickDeltaTime)
    ///     truncates 1.0s to 29 ticks instead of 30 — a silent ~33ms underrun
    ///     on every clean-second value a designer types. The `+ 0.5f` here
    ///     rounds to nearest before the truncating uint cast.
    ///
    ///  2) TPS coupling. Some legacy code uses (uint)(seconds * 30f). That
    ///     happens to be correct at 30 TPS for whole-second inputs but breaks
    ///     silently if the simulation ever runs at a different rate (slow-mo,
    ///     server tick variation, future replay scrubbing). Routing every
    ///     conversion through this helper gives a single place to change.
    ///
    /// Behaviour for negative inputs is undefined (the (uint) cast on a
    /// negative float is implementation-defined in C#). Callers that accept
    /// designer-tunable durations should validate non-negative upstream — see
    /// HyperchargeTracker.Activate's `if (durationSeconds &lt;= 0f)` guard.
    ///
    /// Pinned by SimulationClockSecondsToTicksTests.
    /// </summary>
    public static uint SecondsToTicks(float seconds)
    {
        return (uint)(seconds / TickDeltaTime + 0.5f);
    }
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