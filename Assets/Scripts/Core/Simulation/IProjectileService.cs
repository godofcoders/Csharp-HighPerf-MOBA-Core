namespace MOBA.Core.Simulation
{
    public interface IProjectileService
    {
        void FireProjectile(in ProjectileSpawnContext context);
        void ManualTick(uint currentTick);
    }
}