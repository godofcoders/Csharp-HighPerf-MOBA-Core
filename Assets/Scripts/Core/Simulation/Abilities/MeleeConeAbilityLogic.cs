using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public sealed class MeleeConeAbilityLogic : IAbilityLogic
    {
        private const int ObstacleHitBufferSize = 6;
        private const float ObstacleTraceHeight = 0.25f;
        private const float ObstacleTraceRadius = 0.28f;

        private static readonly RaycastHit[] ObstacleHitBuffer =
            new RaycastHit[ObstacleHitBufferSize];
        private static bool _hasResolvedObstacleMask;
        private static int _resolvedObstacleMask;

        private readonly MeleeConeAbilityDefinition _definition;
        private readonly List<ISpatialEntity> _targetBuffer = new List<ISpatialEntity>(16);

        public MeleeConeAbilityLogic(MeleeConeAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null ||
                user is not BrawlerController owner ||
                owner.State == null ||
                SimulationClock.Grid == null)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            IDamageService damageService = ServiceProvider.Get<IDamageService>();
            Vector3 origin = context.Origin;
            Vector3 forward = ResolveForward(owner, context.Direction);
            float range = Mathf.Max(0.1f, _definition.Range);
            float searchRange = range + Mathf.Max(0f, _definition.HitRadiusPadding) + 1f;
            float cosHalfArc = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(_definition.ArcDegrees, 10f, 180f) * 0.5f);
            int targetsAffected = 0;

            _targetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(origin, searchRange, _targetBuffer);

            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                ISpatialEntity target = _targetBuffer[i];
                if (!SpatialEntityUtility.IsAlive(target) ||
                    SpatialEntityUtility.IsSameEntity(target, owner) ||
                    !CanDamage(owner, target))
                {
                    continue;
                }

                Vector3 toTarget = target.Position - origin;
                toTarget.y = 0f;

                float targetDistance = toTarget.magnitude;
                float targetRadius = Mathf.Max(0f, target.CollisionRadius);
                float edgeDistance = Mathf.Max(0f, targetDistance - targetRadius);
                if (edgeDistance > range + Mathf.Max(0f, _definition.HitRadiusPadding))
                    continue;

                Vector3 hitDirection = targetDistance > 0.001f
                    ? toTarget / targetDistance
                    : forward;

                float closeForgiveness = owner.CollisionRadius + targetRadius + 0.15f;
                if (targetDistance > closeForgiveness && Vector3.Dot(forward, hitDirection) < cosHalfArc)
                    continue;

                if (target.Team != TeamType.Neutral &&
                    HasBlockingObstacle(origin, hitDirection, Mathf.Max(0f, targetDistance - targetRadius * 0.45f)))
                {
                    continue;
                }

                ApplyDamage(damageService, owner, target, context, hitDirection);
                targetsAffected++;
            }

            AbilityExecutionResult result = AbilityExecutionResult.Succeeded(
                context.AbilityDefinition,
                context.SlotType);
            result.AppliedAreaEffect = true;
            result.TargetsAffected = targetsAffected;
            result.ConsumedResource = true;

            if (targetsAffected > 0)
            {
                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.AreaEffectResolved,
                    Source = owner,
                    Target = null,
                    AbilityDefinition = context.AbilityDefinition,
                    SlotType = context.SlotType,
                    Position = origin,
                    Direction = forward,
                    Value = targetsAffected,
                    IsSuper = context.IsSuper
                });
            }

            return result;
        }

        public void Tick(uint currentTick) { }

        private void ApplyDamage(
            IDamageService damageService,
            BrawlerController owner,
            ISpatialEntity target,
            AbilityExecutionContext context,
            Vector3 hitDirection)
        {
            Vector3 hitPosition = target.Position;

            damageService.ApplyDamage(new DamageContext
            {
                Attacker = owner,
                Target = target,
                Damage = _definition.Damage,
                Type = DamageType.Melee,
                HitPosition = hitPosition,
                Direction = hitDirection,
                SourceAbility = context.AbilityDefinition,
                IsSuper = context.IsSuper
            });

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.DamageHit,
                Source = owner,
                Target = target as BrawlerController,
                AbilityDefinition = context.AbilityDefinition,
                SlotType = context.SlotType,
                Position = hitPosition,
                Direction = hitDirection,
                Value = _definition.Damage,
                IsSuper = context.IsSuper
            });
        }

        private static bool CanDamage(BrawlerController owner, ISpatialEntity target)
        {
            if (target.Team == TeamType.Neutral)
                return true;

            return TeamRelationshipUtility.AreEnemies(owner.Team, target.Team);
        }

        private static bool HasBlockingObstacle(Vector3 origin, Vector3 direction, float distance)
        {
            if (distance <= 0.05f)
                return false;

            int obstacleMask = ResolveObstacleMask();
            if (obstacleMask == 0)
                return false;

            Vector3 traceOrigin = origin + Vector3.up * ObstacleTraceHeight;
            int hitCount = Physics.SphereCastNonAlloc(
                traceOrigin,
                ObstacleTraceRadius,
                direction,
                ObstacleHitBuffer,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount && i < ObstacleHitBuffer.Length; i++)
            {
                Collider hitCollider = ObstacleHitBuffer[i].collider;
                if (hitCollider == null ||
                    !hitCollider.enabled ||
                    !hitCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static int ResolveObstacleMask()
        {
            if (_hasResolvedObstacleMask)
                return _resolvedObstacleMask;

            _hasResolvedObstacleMask = true;

            MapGenerator mapGenerator = UnityEngine.Object.FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
            {
                _resolvedObstacleMask = mapGenerator.ObstacleLayer.value;
                return _resolvedObstacleMask;
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            _resolvedObstacleMask = obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
            return _resolvedObstacleMask;
        }

        private static Vector3 ResolveForward(BrawlerController owner, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                return direction.normalized;

            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f
                ? forward.normalized
                : Vector3.forward;
        }
    }
}
