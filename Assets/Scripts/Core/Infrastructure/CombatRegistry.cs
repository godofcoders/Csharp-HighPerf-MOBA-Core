using System.Collections.Generic;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class CombatRegistry
    {
        private static readonly Dictionary<int, ISpatialEntity> _entities = new Dictionary<int, ISpatialEntity>(128);

        public static void Register(ISpatialEntity entity)
        {
            if (entity == null)
                return;

            _entities[entity.EntityID] = entity;
        }

        public static void Unregister(ISpatialEntity entity)
        {
            if (entity == null)
                return;

            _entities.Remove(entity.EntityID);
        }

        public static ISpatialEntity GetEntity(int entityId)
        {
            if (_entities.TryGetValue(entityId, out var entity))
                return entity;

            return null;
        }

        public static bool TryGetEntity(int entityId, out ISpatialEntity entity)
        {
            return _entities.TryGetValue(entityId, out entity);
        }
    }
}