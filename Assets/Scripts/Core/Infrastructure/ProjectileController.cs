using UnityEngine;
using MOBA.Core.Simulation;
using System.Collections.Generic;

namespace MOBA.Core.Infrastructure
{
    public class ProjectileController : SimulationEntity
    {
        private Vector3 _direction;
        private float _speed;
        private float _range;
        private float _damage;
        private float _projectileRadius = 0.2f; // How "fat" is the bullet?

        private int _ownerID; // To prevent self-damage
        private float _distanceTraveled;
        private SimpleObjectPool _originPool;

        public void Initialize(Vector3 origin, Vector3 direction, float speed, float range, float damage, int ownerID, SimpleObjectPool pool)
        {
            transform.position = origin;
            _direction = direction.normalized;
            _speed = speed;
            _range = range;
            _damage = damage;
            _ownerID = ownerID;
            _originPool = pool;
            _distanceTraveled = 0;
        }

        public override void Tick(uint currentTick)
        {
            float tickDelta = 1f / 30f;
            Vector3 step = _direction * (_speed * tickDelta);

            transform.position += step;
            _distanceTraveled += step.magnitude;

            // 1. Optimized Grid Collision Check
            CheckCollision();

            // 2. Lifecycle Check
            if (_distanceTraveled >= _range)
            {
                Deactivate();
            }
        }

        private void CheckCollision()
        {
            // Query the grid for entities in the CURRENT cell
            List<ISpatialEntity> nearbyEntities = SimulationClock.Grid?.GetEntitiesInCell(transform.position);

            if (nearbyEntities == null) return;

            for (int i = 0; i < nearbyEntities.Count; i++)
            {
                ISpatialEntity target = nearbyEntities[i];

                // Don't hit the person who fired this bullet
                if (target.EntityID == _ownerID) continue;

                // Calculate distance (Squared distance is faster - no Square Root)
                float distSq = (transform.position - target.Position).sqrMagnitude;
                float combinedRadius = _projectileRadius + target.CollisionRadius;

                if (distSq < (combinedRadius * combinedRadius))
                {
                    // WE HAVE A HIT
                    target.TakeDamage(_damage);

                    // Trigger visual impact (VFX) here if needed
                    Deactivate();
                    break;
                }
            }
        }

        private void Deactivate()
        {
            if (_originPool != null) _originPool.ReturnToPool(this.gameObject);
            else gameObject.SetActive(false);
        }
    }
}