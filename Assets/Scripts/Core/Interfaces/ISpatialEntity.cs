using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface ISpatialEntity
    {
        int EntityID { get; }
        Vector3 Position { get; }
        float CollisionRadius { get; }
        TeamType Team { get; }
        void TakeDamage(float amount);
    }
}