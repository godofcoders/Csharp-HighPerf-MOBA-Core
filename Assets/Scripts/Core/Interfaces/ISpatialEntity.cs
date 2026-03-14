using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface ISpatialEntity
    {
        Vector3 Position { get; }
        float CollisionRadius { get; }
        void TakeDamage(float amount);
        // Helps the grid identify unique entities
        int EntityID { get; }
    }
}