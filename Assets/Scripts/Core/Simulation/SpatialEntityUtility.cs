using UnityEngine;

namespace MOBA.Core.Simulation
{
    public static class SpatialEntityUtility
    {
        public static bool IsAlive(ISpatialEntity entity)
        {
            if (entity == null)
                return false;

            if (entity is Object unityObject && unityObject == null)
                return false;

            return true;
        }
    }
}
