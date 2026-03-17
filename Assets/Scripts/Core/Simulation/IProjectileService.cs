using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public interface IProjectileService
    {
        void FireProjectile(
            BrawlerController owner,
            Vector3 origin,
            Vector3 direction,
            float speed,
            float range,
            float damage,
            TeamType attackerTeam,
            float superChargeOnHit
        );

        void ManualTick(uint currentTick);
    }
}