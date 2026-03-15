using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    [RequireComponent(typeof(SimpleObjectPool))]
    public class ProjectileManager : MonoBehaviour, IProjectileService
    {
        private SimpleObjectPool _pool;
        private List<ActiveProjectile> _activeProjectiles = new List<ActiveProjectile>();

        private void Awake()
        {
            _pool = GetComponent<SimpleObjectPool>();
            // AAA: Register via ServiceProvider, no Singletons.
            ServiceProvider.Register<IProjectileService>(this);
        }

        public void FireProjectile(Vector3 origin, Vector3 direction, float speed, float range, float damage, TeamType team)
        {
            GameObject go = _pool.Get();
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(direction);

            _activeProjectiles.Add(new ActiveProjectile
            {
                GameObject = go,
                Origin = origin,
                Direction = direction.normalized,
                Speed = speed,
                MaxRangeSq = range * range,
                Damage = damage,
                Team = team
            });
        }

        public void ManualTick(uint currentTick)
        {
            // Iterate backwards so we can safely remove items during the loop
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                var p = _activeProjectiles[i];

                // 1. Logic Movement
                Vector3 movement = p.Direction * (p.Speed * SimulationClock.TickDeltaTime);
                p.GameObject.transform.position += movement;

                // 2. Range Expiry
                if ((p.GameObject.transform.position - p.Origin).sqrMagnitude >= p.MaxRangeSq)
                {
                    Despawn(i);
                    continue;
                }

                // 3. High-Performance Collision (Spatial Grid Query)
                // We check if an entity on a different team is within 0.5 units
                var hit = SimulationClock.Grid?.CheckCollision(p.GameObject.transform.position, 0.5f, p.Team);
                if (hit != null)
                {
                    hit.TakeDamage(p.Damage);
                    Despawn(i);
                }
            }
        }

        private void Despawn(int index)
        {
            var p = _activeProjectiles[index];
            _pool.ReturnToPool(p.GameObject);
            _activeProjectiles.RemoveAt(index);
        }

        // Internal class to track simulation state without GC allocations
        private class ActiveProjectile
        {
            public GameObject GameObject;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float MaxRangeSq;
            public float Damage;
            public TeamType Team;
        }
    }
}