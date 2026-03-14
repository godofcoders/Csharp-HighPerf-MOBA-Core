using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface IAbilityUser
    {
        // The Logic calls this to tell Unity to manifest a projectile
        void FireProjectile(Vector3 origin, Vector3 direction, float speed, float range, float damage);

        // Allows logic to know the user's current position/state
        Vector3 CurrentPosition { get; }
        BrawlerState State { get; }
    }
}