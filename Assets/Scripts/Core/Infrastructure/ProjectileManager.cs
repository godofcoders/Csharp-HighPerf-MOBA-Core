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

        public void FireProjectile(in ProjectileSpawnContext context)
        {
            GameObject go = _pool.Get();
            go.transform.position = context.Origin;
            go.transform.rotation = Quaternion.LookRotation(context.Direction);

            _activeProjectiles.Add(new ActiveProjectile
            {
                Owner = context.Owner,
                SourceAbility = context.SourceAbility,
                SlotType = context.SlotType,
                IsSuper = context.IsSuper,
                IsGadget = context.IsGadget,

                GameObject = go,
                Origin = context.Origin,
                Direction = context.Direction.normalized,
                Speed = context.Speed,
                MaxRangeSq = context.Range * context.Range,
                Damage = context.Damage,
                Team = context.Team,
                SuperChargeOnHit = context.SuperChargeOnHit,

                IsHybrid = context.IsHybrid,
                AllyHealAmount = context.AllyHealAmount,
                EnemyDamageAmount = context.EnemyDamageAmount,
                HitTeamRule = context.HitTeamRule
            });

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.ProjectileSpawned,
                Source = context.Owner,
                Target = null,
                AbilityDefinition = context.SourceAbility,
                SlotType = context.SlotType,
                Position = context.Origin,
                Direction = context.Direction,
                Value = context.Damage,
                IsSuper = context.IsSuper
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

                var hit = SimulationClock.Grid?.CheckCollision(
                    p.GameObject.transform.position,
                    0.5f,
                    p.Team,
                    p.HitTeamRule
                );

                if (hit != null)
                {
                    BrawlerController targetBrawler = hit as BrawlerController;

                    if (p.IsHybrid && targetBrawler != null && p.Owner != null)
                    {
                        bool isAlly = targetBrawler.Team == p.Owner.Team;

                        if (isAlly)
                        {
                            targetBrawler.State.Heal(p.AllyHealAmount);

                            CombatPresentationEventBus.Raise(new CombatPresentationEvent
                            {
                                EventType = CombatPresentationEventType.AbilityCastSucceeded,
                                Source = p.Owner,
                                Target = targetBrawler,
                                AbilityDefinition = p.SourceAbility,
                                SlotType = p.SlotType,
                                Position = p.GameObject.transform.position,
                                Direction = p.Direction,
                                Value = p.AllyHealAmount,
                                IsSuper = p.IsSuper
                            });
                        }
                        else
                        {
                            var damageService = ServiceProvider.Get<IDamageService>();

                            damageService.ApplyDamage(new DamageContext
                            {
                                Attacker = p.Owner,
                                Target = hit,
                                Damage = p.EnemyDamageAmount,
                                Type = DamageType.Projectile,
                                HitPosition = p.GameObject.transform.position,
                                Direction = p.Direction,
                                SourceAbility = p.SourceAbility,
                                IsSuper = p.IsSuper
                            });

                            CombatPresentationEventBus.Raise(new CombatPresentationEvent
                            {
                                EventType = CombatPresentationEventType.DamageHit,
                                Source = p.Owner,
                                Target = targetBrawler,
                                AbilityDefinition = p.SourceAbility,
                                SlotType = p.SlotType,
                                Position = p.GameObject.transform.position,
                                Direction = p.Direction,
                                Value = p.EnemyDamageAmount,
                                IsSuper = p.IsSuper
                            });
                        }
                    }
                    else
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

                        CombatPresentationEventBus.Raise(new CombatPresentationEvent
                        {
                            EventType = CombatPresentationEventType.DamageHit,
                            Source = p.Owner,
                            Target = hit as BrawlerController,
                            AbilityDefinition = p.SourceAbility,
                            SlotType = p.SlotType,
                            Position = p.GameObject.transform.position,
                            Direction = p.Direction,
                            Value = p.Damage,
                            IsSuper = p.IsSuper
                        });
                    }

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
            public AbilityDefinition SourceAbility;
            public AbilitySlotType SlotType;
            public bool IsSuper;
            public bool IsGadget;

            public GameObject GameObject;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float MaxRangeSq;
            public float Damage;
            public TeamType Team;
            public float SuperChargeOnHit;

            public bool IsHybrid;
            public float AllyHealAmount;
            public float EnemyDamageAmount;
            public ProjectileHitTeamRule HitTeamRule;
        }
    }
}