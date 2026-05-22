using System.Collections.Generic;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class CombatRegistry
    {
        private static readonly Dictionary<int, ISpatialEntity> _entities = new Dictionary<int, ISpatialEntity>(128);

        public static void Register(ISpatialEntity entity)
        {
            if (!SpatialEntityUtility.IsAlive(entity))
                return;

            _entities[entity.EntityID] = entity;
        }

        public static void Unregister(ISpatialEntity entity)
        {
            if (entity == null)
                return;

            if (!SpatialEntityUtility.TryGetEntityIdEvenIfDestroyed(entity, out int entityId))
                return;

            _entities.Remove(entityId);
        }

        public static ISpatialEntity GetEntity(int entityId)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                if (SpatialEntityUtility.IsAlive(entity))
                    return entity;

                _entities.Remove(entityId);
            }

            return null;
        }

        public static bool TryGetEntity(int entityId, out ISpatialEntity entity)
        {
            if (!_entities.TryGetValue(entityId, out entity))
                return false;

            if (SpatialEntityUtility.IsAlive(entity))
                return true;

            _entities.Remove(entityId);
            entity = null;
            return false;
        }
    }
}
