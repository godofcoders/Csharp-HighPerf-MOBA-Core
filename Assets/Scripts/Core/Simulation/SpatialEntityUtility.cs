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

        public static bool TryGetEntityId(ISpatialEntity entity, out int entityId)
        {
            entityId = 0;

            if (!IsAlive(entity))
                return false;

            entityId = entity.EntityID;
            return entityId != 0;
        }

        public static bool TryGetEntityIdEvenIfDestroyed(ISpatialEntity entity, out int entityId)
        {
            entityId = 0;

            if (entity == null)
                return false;

            try
            {
                entityId = entity.EntityID;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (MissingComponentException)
            {
                return false;
            }

            return entityId != 0;
        }

        public static bool TryGetPosition(ISpatialEntity entity, out Vector3 position)
        {
            position = default;

            if (!IsAlive(entity))
                return false;

            position = entity.Position;
            return true;
        }

        public static bool IsSameEntity(ISpatialEntity left, ISpatialEntity right)
        {
            return TryGetEntityId(left, out int leftId) &&
                   TryGetEntityId(right, out int rightId) &&
                   leftId == rightId;
        }
    }
}
