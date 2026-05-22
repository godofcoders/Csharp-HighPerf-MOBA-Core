using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class SpatialEntityLifecycleTests
    {
        private sealed class DummySpatialEntity : MonoBehaviour, ISpatialEntity
        {
            private int _entityId;
            private Vector3 _lastKnownPosition;

            public int EntityID => _entityId != 0 ? _entityId : gameObject.GetInstanceID();
            public Vector3 Position => this != null ? transform.position : _lastKnownPosition;
            public float CollisionRadius => 0.5f;
            public TeamType Team { get; set; }

            private void Awake()
            {
                _entityId = gameObject.GetInstanceID();
                _lastKnownPosition = transform.position;
            }

            public void TakeDamage(float amount)
            {
            }
        }

        [Test]
        public void DestroyedUnitySpatialEntity_IsNotAlive_ButKeepsCachedId()
        {
            DummySpatialEntity entity = CreateEntity(Vector3.zero, TeamType.Blue);
            int entityId = entity.EntityID;

            Object.DestroyImmediate(entity.gameObject);

            Assert.IsFalse(SpatialEntityUtility.IsAlive(entity));
            Assert.IsTrue(SpatialEntityUtility.TryGetEntityIdEvenIfDestroyed(entity, out int cachedId));
            Assert.AreEqual(entityId, cachedId);
        }

        [Test]
        public void SpatialGrid_RadiusQuery_PrunesDestroyedUnityEntity()
        {
            SpatialGrid grid = new SpatialGrid(4f);
            DummySpatialEntity entity = CreateEntity(Vector3.zero, TeamType.Red);
            List<ISpatialEntity> results = new List<ISpatialEntity>(4);

            grid.Add(entity);
            Object.DestroyImmediate(entity.gameObject);

            Assert.DoesNotThrow(() => grid.GetEntitiesInRadiusNonAlloc(Vector3.zero, 4f, results));
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void CombatRegistry_GetEntity_PrunesDestroyedUnityEntity()
        {
            DummySpatialEntity entity = CreateEntity(Vector3.zero, TeamType.Red);
            int entityId = entity.EntityID;

            CombatRegistry.Register(entity);
            Object.DestroyImmediate(entity.gameObject);

            Assert.IsNull(CombatRegistry.GetEntity(entityId));
            Assert.IsFalse(CombatRegistry.TryGetEntity(entityId, out _));
        }

        private static DummySpatialEntity CreateEntity(Vector3 position, TeamType team)
        {
            GameObject gameObject = new GameObject("SpatialEntityLifecycleTestEntity");
            gameObject.transform.position = position;

            DummySpatialEntity entity = gameObject.AddComponent<DummySpatialEntity>();
            entity.Team = team;
            return entity;
        }
    }
}
