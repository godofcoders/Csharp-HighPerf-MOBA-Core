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

            ProjectileVisualController visualController = go.GetComponent<ProjectileVisualController>();
            if (visualController != null)
            {
                visualController.ApplyProfile(context.PresentationProfile);
            }

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
                HitTeamRule = context.HitTeamRule,

                DeliveryType = context.DeliveryType,
                TargetPoint = context.TargetPoint,

                HasHybridAoEImpact = context.HasHybridAoEImpact,
                ImpactRadius = context.ImpactRadius,
                ImpactEnemyDamage = context.ImpactEnemyDamage,
                ImpactAllyHeal = context.ImpactAllyHeal,

                UseArcMotion = context.UseArcMotion,
                ArcHeight = context.ArcHeight,
                TravelDistance = context.TravelDistance,
                TravelProgress = 0f,

                PresentationProfile = context.PresentationProfile,
                IsChainProjectile = context.IsChainProjectile,
                RemainingBounces = context.RemainingBounces,
                BounceRadius = context.BounceRadius,
                HitEntityIds = new System.Collections.Generic.HashSet<int>(),
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

                if (p.DeliveryType == ProjectileDeliveryType.ThrownImpactAoE && p.UseArcMotion)
                {
                    float totalDistance = Mathf.Max(0.01f, p.TravelDistance);
                    float distanceStep = p.Speed * SimulationClock.TickDeltaTime;

                    p.TravelProgress += distanceStep / totalDistance;
                    p.TravelProgress = Mathf.Clamp01(p.TravelProgress);

                    Vector3 basePos = Vector3.Lerp(p.Origin, p.TargetPoint, p.TravelProgress);
                    float arcOffset = 4f * p.ArcHeight * p.TravelProgress * (1f - p.TravelProgress);

                    p.GameObject.transform.position = basePos + (Vector3.up * arcOffset);

                    Vector3 flatDirection = p.TargetPoint - p.Origin;
                    flatDirection.y = 0f;
                    if (flatDirection.sqrMagnitude > 0.001f)
                        p.GameObject.transform.rotation = Quaternion.LookRotation(flatDirection.normalized);
                }
                else
                {
                    Vector3 movement = p.Direction * (p.Speed * SimulationClock.TickDeltaTime);
                    p.GameObject.transform.position += movement;

                    ProjectileVisualController visualController = p.GameObject.GetComponent<ProjectileVisualController>();
                    if (visualController != null)
                    {
                        visualController.TickVisual(SimulationClock.TickDeltaTime);

                        if (visualController.ShouldFaceMovementDirection())
                        {
                            Vector3 lookDirection = p.Direction;
                            if (lookDirection.sqrMagnitude > 0.001f)
                                p.GameObject.transform.rotation = Quaternion.LookRotation(lookDirection.normalized);
                        }
                    }
                }

                if (p.DeliveryType != ProjectileDeliveryType.ThrownImpactAoE)
                {
                    if ((p.GameObject.transform.position - p.Origin).sqrMagnitude >= p.MaxRangeSq)
                    {
                        Despawn(i);
                        continue;
                    }
                }

                if (p.DeliveryType == ProjectileDeliveryType.ThrownImpactAoE)
                {
                    bool reachedImpact =
                        p.UseArcMotion
                            ? p.TravelProgress >= 1f
                            : (p.GameObject.transform.position - p.TargetPoint).sqrMagnitude <= (0.2f * 0.2f);

                    if (reachedImpact)
                    {
                        ResolveHybridAoEImpact(p, p.TargetPoint);
                        Despawn(i);
                    }

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

                    if (p.IsChainProjectile && targetBrawler != null && p.HitEntityIds.Contains(targetBrawler.EntityID))
                    {
                        continue;
                    }

                    if (p.IsHybrid && targetBrawler != null && p.Owner != null)
                    {
                        bool isAlly = targetBrawler.Team == p.Owner.Team;

                        if (isAlly)
                        {
                            Debug.Log($"[HYBRID PROJECTILE] {p.Owner.name} healed ally {targetBrawler.name} for {p.AllyHealAmount}");
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
                            Debug.Log($"[HYBRID PROJECTILE] {p.Owner.name} damaged enemy {targetBrawler.name} for {p.EnemyDamageAmount}");

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

                    // CHAIN PROJECTILE HANDLING
                    if (p.IsChainProjectile && targetBrawler != null)
                    {
                        p.HitEntityIds.Add(targetBrawler.EntityID);

                        if (p.RemainingBounces > 0)
                        {
                            BrawlerController nextTarget = ResolveNextChainTarget(p, targetBrawler);
                            if (nextTarget != null)
                            {
                                p.RemainingBounces--;

                                Vector3 nextDirection = (nextTarget.Position - p.GameObject.transform.position).normalized;
                                p.Direction = nextDirection;

                                if (nextDirection.sqrMagnitude > 0.001f)
                                    p.GameObject.transform.rotation = Quaternion.LookRotation(nextDirection);

                                continue;
                            }
                        }
                    }

                    Despawn(i);
                }
            }
        }

        private void ResolveHybridAoEImpact(ActiveProjectile p, Vector3 impactPosition)
        {
            if (!p.HasHybridAoEImpact || SimulationClock.Grid == null || p.Owner == null)
                return;

            List<ISpatialEntity> targets = new List<ISpatialEntity>(16);
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(impactPosition, p.ImpactRadius, targets);

            var damageService = ServiceProvider.Get<IDamageService>();
            float sqrRadius = p.ImpactRadius * p.ImpactRadius;

            for (int i = 0; i < targets.Count; i++)
            {
                BrawlerController targetBrawler = targets[i] as BrawlerController;
                if (targetBrawler == null)
                    continue;

                float distSq = (targetBrawler.Position - impactPosition).sqrMagnitude;
                if (distSq > sqrRadius)
                    continue;

                bool isAlly = targetBrawler.Team == p.Owner.Team;

                if (isAlly)
                {
                    targetBrawler.State.Heal(p.ImpactAllyHeal);
                    Debug.Log($"[THROWN HYBRID AOE] {p.Owner.name} healed ally {targetBrawler.name} for {p.ImpactAllyHeal}");
                }
                else
                {
                    damageService.ApplyDamage(new DamageContext
                    {
                        Attacker = p.Owner,
                        Target = targetBrawler,
                        Damage = p.ImpactEnemyDamage,
                        Type = DamageType.AoE,
                        HitPosition = impactPosition,
                        Direction = (targetBrawler.Position - impactPosition).normalized,
                        SourceAbility = p.SourceAbility,
                        IsSuper = p.IsSuper
                    });

                    Debug.Log($"[THROWN HYBRID AOE] {p.Owner.name} damaged enemy {targetBrawler.name} for {p.ImpactEnemyDamage}");
                }
            }

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.AreaEffectResolved,
                Source = p.Owner,
                Target = null,
                AbilityDefinition = p.SourceAbility,
                SlotType = p.SlotType,
                Position = impactPosition,
                Direction = p.Direction,
                Value = p.ImpactRadius,
                IsSuper = p.IsSuper
            });
        }

        private void Despawn(int index)
        {
            var p = _activeProjectiles[index];
            _pool.ReturnToPool(p.GameObject);
            _activeProjectiles.RemoveAt(index);
        }

        private BrawlerController ResolveNextChainTarget(ActiveProjectile p, BrawlerController currentTarget)
        {
            if (SimulationClock.Grid == null || currentTarget == null)
                return null;

            List<ISpatialEntity> candidates = new List<ISpatialEntity>(16);
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(currentTarget.Position, p.BounceRadius, candidates);

            float bestDistSq = float.MaxValue;
            BrawlerController best = null;

            for (int j = 0; j < candidates.Count; j++)
            {
                BrawlerController candidate = candidates[j] as BrawlerController;
                if (candidate == null)
                    continue;

                if (candidate.Team == p.Team)
                    continue;

                if (candidate.State == null || candidate.State.IsDead)
                    continue;

                if (p.HitEntityIds.Contains(candidate.EntityID))
                    continue;

                float distSq = (candidate.Position - currentTarget.Position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                }
            }

            return best;
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

            public ProjectileDeliveryType DeliveryType;
            public Vector3 TargetPoint;

            public bool HasHybridAoEImpact;
            public float ImpactRadius;
            public float ImpactEnemyDamage;
            public float ImpactAllyHeal;

            public bool UseArcMotion;
            public float ArcHeight;
            public float TravelDistance;
            public float TravelProgress;

            public ProjectilePresentationProfile PresentationProfile;

            public bool IsChainProjectile;
            public int RemainingBounces;
            public float BounceRadius;
            public System.Collections.Generic.HashSet<int> HitEntityIds;
        }
    }
}
