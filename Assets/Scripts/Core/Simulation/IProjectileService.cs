namespace MOBA.Core.Simulation
{
    public interface IProjectileService
    {
        void FireProjectile(in ProjectileSpawnContext context);
        // ManualTick() removed — ProjectileManager now implements ITickable and
        // is driven by SimulationRegistry on the Collision phase. Systems that
        // used to poke this should just let the simulation tick handle it.
    }
}