using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface IProjectileService
    {
        void FireProjectile(Vector3 origin, Vector3 direction, float speed, float range, float damage, TeamType team);
        void ManualTick(uint currentTick);
    }
}