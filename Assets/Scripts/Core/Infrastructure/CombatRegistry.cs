using System.Collections.Generic;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class CombatRegistry
    {
        private static readonly Dictionary<int, ISpatialEntity> _entities = new Dictionary<int, ISpatialEntity>(128);
        private static readonly List<int> _staleEntityIds = new List<int>(16);

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

        public static void GetBrawlersNonAlloc(List<BrawlerController> results)
        {
            if (results == null)
                return;

            results.Clear();
            _staleEntityIds.Clear();

            foreach (var pair in _entities)
            {
                ISpatialEntity entity = pair.Value;
                if (!SpatialEntityUtility.IsAlive(entity))
                {
                    _staleEntityIds.Add(pair.Key);
                    continue;
                }

                if (entity is BrawlerController brawler)
                    results.Add(brawler);
            }

            for (int i = 0; i < _staleEntityIds.Count; i++)
                _entities.Remove(_staleEntityIds[i]);
        }
    }
}
