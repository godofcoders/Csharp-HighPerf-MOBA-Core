using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    [RequireComponent(typeof(SimpleObjectPool))]
    public class ProjectileManager : MonoBehaviour, IProjectileService
    {
        private SimpleObjectPool _pool;

        private readonly List<ActiveProjectile> _activeProjectiles = new List<ActiveProjectile>(64);

        private void Awake()
        {
            _pool = GetComponent<SimpleObjectPool>();
            ServiceProvider.Register<IProjectileService>(this);
        }

        public void FireProjectile(
            BrawlerController owner,
            Vector3 origin,
            Vector3 direction,
            float speed,
            float range,
            float damage,
            TeamType team,
            float superChargeOnHit)
        {
            GameObject go = _pool.Get();
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(direction);

            _activeProjectiles.Add(new ActiveProjectile
            {
                Owner = owner,
                GameObject = go,
                Origin = origin,
                Direction = direction.normalized,
                Speed = speed,
                MaxRangeSq = range * range,
                Damage = damage,
                Team = team,
                SuperChargeOnHit = superChargeOnHit,
                SourceAbility = null,
                IsSuper = false
            });
        }

        public void ManualTick(uint currentTick)
        {
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                var p = _activeProjectiles[i];

                Vector3 movement = p.Direction * (p.Speed * SimulationClock.TickDeltaTime);
                p.GameObject.transform.position += movement;

                if ((p.GameObject.transform.position - p.Origin).sqrMagnitude >= p.MaxRangeSq)
                {
                    Despawn(i);
                    continue;
                }

                var hit = SimulationClock.Grid?.CheckCollision(p.GameObject.transform.position, 0.5f, p.Team);

                if (hit != null)
                {
                    var damageService = ServiceProvider.Get<IDamageService>();

                    damageService.ApplyDamage(new DamageContext
                    {
                        Attacker = p.Owner,
                        Target = hit,
                        Damage = p.Damage,
                        Type = DamageType.Projectile,
                        HitPosition = p.GameObject.transform.position,
                        Direction = p.Direction,
                        SourceAbility = p.SourceAbility,
                        IsSuper = p.IsSuper
                    });

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

        private sealed class ActiveProjectile
        {
            public BrawlerController Owner;
            public GameObject GameObject;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float MaxRangeSq;
            public float Damage;
            public TeamType Team;
            public float SuperChargeOnHit;

            public AbilityDefinition SourceAbility;
            public bool IsSuper;
        }
    }
}