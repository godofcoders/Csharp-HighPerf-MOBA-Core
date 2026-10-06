using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class ShowdownCrateTargetUtility
    {
        public static bool IsTargetableCrate(ISpatialEntity entity)
        {
            return entity is PowerCubeCrateController crate &&
                   !crate.IsDestroyed &&
                   crate.CurrentHealth > 0f;
        }

        public static float CalculateAimPriorityScore(
            ISpatialEntity entity,
            Vector3 origin,
            float maxRange)
        {
            if (!IsTargetableCrate(entity))
                return float.MinValue;

            Vector3 offset = entity.Position - origin;
            offset.y = 0f;
            float reach = Mathf.Max(0f, maxRange) +
                          Mathf.Max(0f, entity.CollisionRadius);
            float distanceSq = offset.sqrMagnitude;
            if (distanceSq > reach * reach)
                return float.MinValue;

            return -distanceSq;
        }
    }
}
