namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Explicit ordering of simulation phases within a single tick.
    /// Enum values are spaced by 10 so new phases can be inserted between
    /// existing ones without renumbering (important for save-game / replay compatibility later).
    /// </summary>
    public enum TickPhase
    {
        PreTick = 0,    // Reset per-frame flags, capture snapshots
        InputApply = 10,   // Buffered inputs become pending actions
        AbilityCast = 20,   // Execute ability logic, spawn projectiles
        Movement = 30,   // Integrate position, apply velocity
        Collision = 40,   // Spatial-grid queries, queue hits
        DamageResolution = 50,   // Apply queued damage, shields, lifesteal, death
        StatusEffectTick = 60,   // Buff/debuff duration countdown, expiry
        Cleanup = 70,   // Remove dead entities, despawn projectiles
        PostTick = 80,   // Event-bus flush, debug snapshots
    }
}