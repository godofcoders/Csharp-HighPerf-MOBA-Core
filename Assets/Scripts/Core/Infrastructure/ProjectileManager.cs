using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    [RequireComponent(typeof(SimpleObjectPool))]
    public class ProjectileManager : MonoBehaviour, IProjectileService, IProjectileThreatProvider, ITickable
    {
        private const float DirectProjectileThreatRadius = 1.15f;
        private const float DirectProjectileThreatLookaheadSeconds = 1.25f;

        private SimpleObjectPool _pool;
        private readonly List<ActiveProjectile> _activeProjectiles = new List<ActiveProjectile>(64);

        private void Awake()
        {
            _pool = GetComponent<SimpleObjectPool>();
            EnsureImpactFeedbackView();
            ServiceProvider.Register<IProjectileService>(this);
            ServiceProvider.Register<IProjectileThreatProvider>(this);
        }

        // Registered in Start (not Awake) so SimulationClock.Awake has run first
        // and SimulationClock.Registry exists. Unity guarantees all Awake() finish
        // before any Start() runs.
        //
        // Phase choice: Collision. ProjectileManager.Tick currently does movement +
        // collision detection + damage application + despawn in one pass. "Collision"
        // is the phase where it makes the most sense to land, because (a) the hit
        // resolution is the part that depends on other systems' finished state
        // (brawler positions updated in Movement), and (b) it preserves the old
        // behavior of projectiles ticking AFTER brawlers.
        //
        // TODO (Session 3): Split Tick() into TickMovement (→ Movement phase) and
        // TickCollision (→ Collision phase), and register for both phases.
        private void Start()
        {
            SimulationClock.Registry?.Register(this, TickPhase.Collision);
        }

        private void OnDestroy()
        {
            SimulationClock.Registry?.Unregister(this, TickPhase.Collision);
            ClearActiveProjectilesForShutdown();
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
                LingeringHazardDefinition = context.LingeringHazardDefinition,


                UseArcMotion = context.UseArcMotion,
                ArcHeight = context.ArcHeight,
                TravelDistance = context.TravelDistance,
                TravelProgress = 0f,

                PresentationProfile = context.PresentationProfile,
                IsChainProjectile = context.IsChainProjectile,
                RemainingBounces = context.RemainingBounces,
                BounceRadius = context.BounceRadius,
                HitEntityIds = new System.Collections.Generic.HashSet<int>(),
                CanAffectEnemiesOnImpact = context.CanAffectEnemiesOnImpact,
                CanAffectAlliesOnImpact = context.CanAffectAlliesOnImpact,
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

        public void Tick(uint currentTick)
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
                        Despawn(i, ProjectileEndReason.Expired, p.GameObject.transform.position, null);
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
                        Despawn(i, ProjectileEndReason.Impact, p.TargetPoint, null);
                    }

                    continue;
                }

                var hit = SimulationClock.Grid?.CheckCollision(
                    p.GameObject.transform.position,
                    0.5f,
                    p.Team,
                    p.HitTeamRule
                );

                if (SpatialEntityUtility.IsAlive(hit))
                {
                    Vector3 projectilePosition = p.GameObject.transform.position;
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
                            float beforeHealth = targetBrawler.State.CurrentHealth;
                            targetBrawler.State.Heal(p.AllyHealAmount, p.Owner, true);
                            float healingDone = targetBrawler.State.CurrentHealth - beforeHealth;
                            if (healingDone > 0f)
                            {
                                AIReportCardTracker.RecordHealingDone(
                                    p.Owner,
                                    targetBrawler,
                                    healingDone,
                                    p.IsSuper,
                                    AIReportCardTracker.GetCurrentTickOrZero());
                            }

                            CombatPresentationEventBus.Raise(new CombatPresentationEvent
                            {
                                EventType = CombatPresentationEventType.AbilityCastSucceeded,
                                Source = p.Owner,
                                Target = targetBrawler,
                                AbilityDefinition = p.SourceAbility,
                                SlotType = p.SlotType,
                                Position = projectilePosition,
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
                                HitPosition = projectilePosition,
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
                                Position = projectilePosition,
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
                            HitPosition = projectilePosition,
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
                            Position = projectilePosition,
                            Direction = p.Direction,
                            Value = p.Damage,
                            IsSuper = p.IsSuper
                        });
                    }

                    // CHAIN PROJECTILE HANDLING
                    if (p.IsChainProjectile && targetBrawler != null)
                    {
                        p.HitEntityIds.Add(targetBrawler.EntityID);
                        RaiseProjectileEndEvent(p, ProjectileEndReason.Impact, projectilePosition, targetBrawler);

                        if (p.RemainingBounces > 0)
                        {
                            BrawlerController nextTarget = ResolveNextChainTarget(p, targetBrawler);
                            if (nextTarget != null)
                            {
                                p.RemainingBounces--;

                                Vector3 nextDirection = (nextTarget.Position - projectilePosition).normalized;
                                p.Direction = nextDirection;

                                if (nextDirection.sqrMagnitude > 0.001f)
                                    p.GameObject.transform.rotation = Quaternion.LookRotation(nextDirection);

                                continue;
                            }
                        }
                    }
                    else
                    {
                        RaiseProjectileEndEvent(p, ProjectileEndReason.Impact, projectilePosition, targetBrawler);
                    }

                    Despawn(i, ProjectileEndReason.Impact, projectilePosition, targetBrawler, false);
                }
            }
        }

        public void AppendProjectileThreatsNonAlloc(
            Vector3 observerPosition,
            TeamType observerTeam,
            float scanRadius,
            List<GameplayThreatInfo> results)
        {
            if (results == null)
                return;

            float scanRadiusSq = scanRadius * scanRadius;

            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                ActiveProjectile projectile = _activeProjectiles[i];
                if (projectile == null || projectile.GameObject == null)
                    continue;

                if (projectile.DeliveryType == ProjectileDeliveryType.ThrownImpactAoE)
                {
                    AppendThrownProjectileThreat(
                        projectile,
                        observerPosition,
                        observerTeam,
                        scanRadiusSq,
                        results);
                }
                else
                {
                    AppendDirectProjectileThreat(
                        projectile,
                        observerPosition,
                        observerTeam,
                        scanRadiusSq,
                        results);
                }
            }
        }

        private void AppendDirectProjectileThreat(
            ActiveProjectile projectile,
            Vector3 observerPosition,
            TeamType observerTeam,
            float scanRadiusSq,
            List<GameplayThreatInfo> results)
        {
            float damage = GetProjectileThreatDamage(projectile, observerTeam);
            if (damage <= 0f)
                return;

            Vector3 projectilePosition = projectile.GameObject.transform.position;
            Vector3 toObserver = observerPosition - projectilePosition;
            toObserver.y = 0f;

            if (toObserver.sqrMagnitude > scanRadiusSq)
                return;

            Vector3 direction = projectile.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            direction.Normalize();
            float forwardDistance = Vector3.Dot(toObserver, direction);
            float lookaheadDistance = Mathf.Max(
                DirectProjectileThreatRadius,
                projectile.Speed * DirectProjectileThreatLookaheadSeconds);

            if (forwardDistance < -DirectProjectileThreatRadius || forwardDistance > lookaheadDistance)
                return;

            Vector3 closestPoint = projectilePosition + direction * Mathf.Max(0f, forwardDistance);
            Vector3 lateral = observerPosition - closestPoint;
            lateral.y = 0f;

            float threatRadius = DirectProjectileThreatRadius;
            if (lateral.sqrMagnitude > threatRadius * threatRadius)
                return;

            results.Add(new GameplayThreatInfo
            {
                Owner = projectile.Owner,
                Team = projectile.Team,
                Position = closestPoint,
                Direction = direction,
                Radius = threatRadius,
                Damage = damage,
                TimeToImpact = projectile.Speed > 0f ? Mathf.Max(0f, forwardDistance) / projectile.Speed : 0f,
                IsProjectile = true,
                IsAreaHazard = false,
                IsSuper = projectile.IsSuper
            });
        }

        private void AppendThrownProjectileThreat(
            ActiveProjectile projectile,
            Vector3 observerPosition,
            TeamType observerTeam,
            float scanRadiusSq,
            List<GameplayThreatInfo> results)
        {
            float damage = GetThrownThreatDamage(projectile, observerTeam);
            if (damage <= 0f || projectile.ImpactRadius <= 0f)
                return;

            Vector3 delta = observerPosition - projectile.TargetPoint;
            delta.y = 0f;

            if (delta.sqrMagnitude > scanRadiusSq)
                return;

            float threatRadius = projectile.ImpactRadius + 0.35f;
            if (delta.sqrMagnitude > threatRadius * threatRadius)
                return;

            float remainingDistance = Mathf.Max(0f, 1f - projectile.TravelProgress) *
                                      Mathf.Max(0.01f, projectile.TravelDistance);

            Vector3 direction = projectile.TargetPoint - projectile.Origin;
            direction.y = 0f;

            results.Add(new GameplayThreatInfo
            {
                Owner = projectile.Owner,
                Team = projectile.Team,
                Position = projectile.TargetPoint,
                Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero,
                Radius = threatRadius,
                Damage = damage,
                TimeToImpact = projectile.Speed > 0f ? remainingDistance / projectile.Speed : 0f,
                IsProjectile = true,
                IsAreaHazard = false,
                IsSuper = projectile.IsSuper
            });
        }

        private float GetProjectileThreatDamage(ActiveProjectile projectile, TeamType observerTeam)
        {
            if (!CanProjectileAffectTeam(projectile.HitTeamRule, projectile.Team, observerTeam))
                return 0f;

            return projectile.IsHybrid
                ? Mathf.Max(0f, projectile.EnemyDamageAmount)
                : Mathf.Max(0f, projectile.Damage);
        }

        private float GetThrownThreatDamage(ActiveProjectile projectile, TeamType observerTeam)
        {
            if (projectile.HasHybridAoEImpact)
            {
                bool enemy = observerTeam != projectile.Team;
                if (enemy && projectile.CanAffectEnemiesOnImpact)
                    return Mathf.Max(0f, projectile.ImpactEnemyDamage);

                return 0f;
            }

            if (!CanProjectileAffectTeam(projectile.HitTeamRule, projectile.Team, observerTeam))
                return 0f;

            return projectile.IsHybrid
                ? Mathf.Max(0f, projectile.EnemyDamageAmount)
                : Mathf.Max(0f, projectile.Damage);
        }

        private static bool CanProjectileAffectTeam(
            ProjectileHitTeamRule hitTeamRule,
            TeamType projectileTeam,
            TeamType observerTeam)
        {
            switch (hitTeamRule)
            {
                case ProjectileHitTeamRule.EnemiesOnly:
                    return observerTeam != projectileTeam;

                case ProjectileHitTeamRule.AlliesOnly:
                    return observerTeam == projectileTeam;

                case ProjectileHitTeamRule.AlliesAndEnemies:
                    return true;

                default:
                    return false;
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

                Vector3 targetPosition = targetBrawler.Position;
                float distSq = (targetPosition - impactPosition).sqrMagnitude;
                if (distSq > sqrRadius)
                    continue;

                bool isAlly = targetBrawler.Team == p.Owner.Team;

                if (isAlly)
                {
                    if (!p.CanAffectAlliesOnImpact)
                        continue;

                    if (p.ImpactAllyHeal != 0f)
                    {
                        float beforeHealth = targetBrawler.State.CurrentHealth;
                        targetBrawler.State.Heal(p.ImpactAllyHeal, p.Owner, true);
                        float healingDone = targetBrawler.State.CurrentHealth - beforeHealth;
                        if (healingDone > 0f)
                        {
                            AIReportCardTracker.RecordHealingDone(
                                p.Owner,
                                targetBrawler,
                                healingDone,
                                p.IsSuper,
                                AIReportCardTracker.GetCurrentTickOrZero());
                        }
                        Debug.Log($"[THROWN HYBRID AOE] {p.Owner.name} healed ally {targetBrawler.name} for {p.ImpactAllyHeal}");
                    }
                }
                else
                {
                    if (!p.CanAffectEnemiesOnImpact)
                        continue;

                    if (p.ImpactEnemyDamage != 0f)
                    {
                        damageService.ApplyDamage(new DamageContext
                        {
                            Attacker = p.Owner,
                            Target = targetBrawler,
                            Damage = p.ImpactEnemyDamage,
                            Type = DamageType.AoE,
                            HitPosition = impactPosition,
                            Direction = (targetPosition - impactPosition).normalized,
                            SourceAbility = p.SourceAbility,
                            IsSuper = p.IsSuper
                        });

                        Debug.Log($"[THROWN HYBRID AOE] {p.Owner.name} damaged enemy {targetBrawler.name} for {p.ImpactEnemyDamage}");
                    }
                }
            }

            if (p.LingeringHazardDefinition != null)
            {
                var hazardService = ServiceProvider.Get<IAreaHazardService>();
                if (hazardService != null)
                {
                    hazardService.SpawnHazard(new AreaHazardSpawnRequest
                    {
                        Owner = p.Owner,
                        Team = p.Team,
                        Definition = p.LingeringHazardDefinition,
                        Position = impactPosition,
                        SourceAbility = p.SourceAbility,
                        SlotType = p.SlotType,
                        IsSuper = p.IsSuper
                    });
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

        private void Despawn(
            int index,
            ProjectileEndReason reason,
            Vector3 position,
            BrawlerController target,
            bool raiseEvent = true)
        {
            var p = _activeProjectiles[index];

            if (raiseEvent)
                RaiseProjectileEndEvent(p, reason, position, target);

            ProjectileVisualController visualController = p.GameObject != null
                ? p.GameObject.GetComponent<ProjectileVisualController>()
                : null;

            if (visualController != null)
                visualController.ResetForPool();

            _pool.ReturnToPool(p.GameObject);
            _activeProjectiles.RemoveAt(index);
        }

        private void RaiseProjectileEndEvent(
            ActiveProjectile projectile,
            ProjectileEndReason reason,
            Vector3 position,
            BrawlerController target)
        {
            if (projectile == null)
                return;

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = reason == ProjectileEndReason.Expired
                    ? CombatPresentationEventType.ProjectileExpired
                    : CombatPresentationEventType.ProjectileImpacted,
                Source = projectile.Owner,
                Target = target,
                AbilityDefinition = projectile.SourceAbility,
                SlotType = projectile.SlotType,
                Position = position,
                Direction = projectile.Direction,
                Value = ResolveImpactFeedbackRadius(projectile),
                IsSuper = projectile.IsSuper
            });
        }

        private static float ResolveImpactFeedbackRadius(ActiveProjectile projectile)
        {
            if (projectile == null)
                return 0.26f;

            if (projectile.DeliveryType == ProjectileDeliveryType.ThrownImpactAoE && projectile.ImpactRadius > 0f)
                return Mathf.Clamp(projectile.ImpactRadius * 0.18f, 0.34f, 0.82f);

            return projectile.IsSuper ? 0.42f : 0.26f;
        }

        private void ClearActiveProjectilesForShutdown()
        {
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                ActiveProjectile projectile = _activeProjectiles[i];
                if (projectile?.GameObject == null)
                    continue;

                ProjectileVisualController visualController =
                    projectile.GameObject.GetComponent<ProjectileVisualController>();

                if (visualController != null)
                    visualController.ResetForPool();

                projectile.GameObject.SetActive(false);
            }

            _activeProjectiles.Clear();
        }

        private void EnsureImpactFeedbackView()
        {
            if (GetComponent<ProjectileImpactFeedbackView>() == null)
                gameObject.AddComponent<ProjectileImpactFeedbackView>();
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
            public AreaHazardDefinition LingeringHazardDefinition;

            public bool CanAffectEnemiesOnImpact;
            public bool CanAffectAlliesOnImpact;
        }

        private enum ProjectileEndReason
        {
            Impact,
            Expired
        }
    }
}
