using System.Collections;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public sealed class LeapAbilityLogic : IAbilityLogic
    {
        private const int LandingOverlapBufferSize = 12;
        private const int LandingSearchRings = 3;
        private const int LandingSearchSteps = 12;

        private static readonly Collider[] LandingOverlapBuffer =
            new Collider[LandingOverlapBufferSize];

        private readonly LeapAbilityDefinition _definition;
        private readonly List<ISpatialEntity> _targetBuffer = new List<ISpatialEntity>(20);

        public LeapAbilityLogic(LeapAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null ||
                user is not BrawlerController owner ||
                owner.State == null)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            Vector3 origin = context.Origin;
            Vector3 forward = ResolveForward(owner, context.Direction);
            Vector3 requestedLanding = ResolveRequestedLanding(origin, forward, context);

            if (!TryResolveLanding(owner, origin, requestedLanding, out Vector3 landing))
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            float horizontalDistance = ResolveHorizontalDistance(origin, landing);
            float travelDuration = ResolveTravelDuration(horizontalDistance);
            float jumpHeight = ResolveJumpHeight(horizontalDistance);
            owner.WarpTo(landing);
            owner.PlayPresentationLeapArc(
                origin,
                landing,
                travelDuration,
                jumpHeight,
                Mathf.Max(0.35f, _definition.ApexHangPower));

            AbilityExecutionResult result = AbilityExecutionResult.Succeeded(
                context.AbilityDefinition,
                context.SlotType);
            result.AppliedAreaEffect = true;
            result.TargetsAffected = 0;
            result.ConsumedResource = true;

            owner.RunTimedBurst(ResolveLandingRoutine(owner, context, landing, forward, travelDuration));

            return result;
        }

        public void Tick(uint currentTick) { }

        private float ResolveTravelDuration(float horizontalDistance)
        {
            float duration = Mathf.Max(0f, _definition.TravelDurationSeconds);
            if (_definition.TravelDurationPerUnit > 0f)
                duration += Mathf.Max(0f, horizontalDistance) * _definition.TravelDurationPerUnit;

            if (_definition.MaxTravelDurationSeconds > 0f)
                duration = Mathf.Min(duration, _definition.MaxTravelDurationSeconds);

            return duration;
        }

        private float ResolveJumpHeight(float horizontalDistance)
        {
            float height = Mathf.Max(0f, _definition.JumpHeight);
            if (_definition.DistanceHeightBonus > 0f)
                height += Mathf.Max(0f, horizontalDistance) * _definition.DistanceHeightBonus;

            if (_definition.MaxJumpHeight > 0f)
                height = Mathf.Min(height, _definition.MaxJumpHeight);

            return height;
        }

        private static float ResolveHorizontalDistance(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.magnitude;
        }

        private IEnumerator ResolveLandingRoutine(
            BrawlerController owner,
            AbilityExecutionContext context,
            Vector3 landing,
            Vector3 forward,
            float travelDurationSeconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0f, travelDurationSeconds);
            while (elapsed < duration)
            {
                if (owner == null || owner.State == null || !owner.gameObject.activeInHierarchy)
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (owner == null ||
                owner.State == null ||
                !owner.gameObject.activeInHierarchy ||
                !SpatialEntityUtility.IsAlive(owner))
            {
                yield break;
            }

            int targetsAffected = ApplyLandingDamage(owner, context, landing);
            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.AreaEffectResolved,
                Source = owner,
                Target = null,
                AbilityDefinition = context.AbilityDefinition,
                SlotType = context.SlotType,
                Position = landing,
                Direction = forward,
                Value = targetsAffected,
                IsSuper = true,
                IsHypercharged = context.IsHypercharged
            });
        }

        private int ApplyLandingDamage(
            BrawlerController owner,
            AbilityExecutionContext context,
            Vector3 landing)
        {
            if (SimulationClock.Grid == null || _definition.Damage <= 0f || _definition.LandingRadius <= 0f)
                return 0;

            IDamageService damageService = ServiceProvider.Get<IDamageService>();
            float radius = Mathf.Max(0.1f, _definition.LandingRadius);
            int targetsAffected = 0;

            _targetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(landing, radius + 1f, _targetBuffer);

            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                ISpatialEntity target = _targetBuffer[i];
                if (!SpatialEntityUtility.IsAlive(target) ||
                    SpatialEntityUtility.IsSameEntity(target, owner) ||
                    !CanDamage(owner, target))
                {
                    continue;
                }

                Vector3 toTarget = target.Position - landing;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                float targetRadius = Mathf.Max(0f, target.CollisionRadius);
                if (Mathf.Max(0f, distance - targetRadius) > radius)
                    continue;

                Vector3 hitDirection = distance > 0.001f
                    ? toTarget / distance
                    : owner.transform.forward;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = owner,
                    Target = target,
                    Damage = _definition.Damage,
                    Type = DamageType.AoE,
                    HitPosition = target.Position,
                    Direction = hitDirection,
                    SourceAbility = context.AbilityDefinition,
                    IsSuper = true
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = owner,
                    Target = target as BrawlerController,
                    AbilityDefinition = context.AbilityDefinition,
                    SlotType = context.SlotType,
                    Position = target.Position,
                    Direction = hitDirection,
                    Value = _definition.Damage,
                    IsSuper = true,
                    IsHypercharged = context.IsHypercharged
                });

                targetsAffected++;
            }

            return targetsAffected;
        }

        private Vector3 ResolveRequestedLanding(
            Vector3 origin,
            Vector3 forward,
            AbilityExecutionContext context)
        {
            Vector3 target = context.HasTargetPoint
                ? context.TargetPoint
                : origin + forward * Mathf.Max(0.1f, _definition.Range);

            Vector3 offset = target - origin;
            offset.y = 0f;

            float range = Mathf.Max(0.1f, _definition.Range);
            if (offset.sqrMagnitude > range * range)
                offset = offset.normalized * range;

            Vector3 landing = origin + offset;
            landing.y = origin.y;
            return landing;
        }

        private bool TryResolveLanding(
            BrawlerController owner,
            Vector3 origin,
            Vector3 requested,
            out Vector3 landing)
        {
            requested.y = origin.y;
            float range = Mathf.Max(0.1f, _definition.Range);

            if (IsLandingClear(owner, requested))
            {
                landing = requested;
                return true;
            }

            float searchRadius = Mathf.Max(0.1f, _definition.LandingSearchRadius);
            Vector3 best = origin;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int ring = 1; ring <= LandingSearchRings; ring++)
            {
                float radius = searchRadius * ring / LandingSearchRings;
                for (int step = 0; step < LandingSearchSteps; step++)
                {
                    float angle = step * Mathf.PI * 2f / LandingSearchSteps;
                    Vector3 candidate = requested + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0f,
                        Mathf.Sin(angle) * radius);

                    Vector3 offsetFromOrigin = candidate - origin;
                    offsetFromOrigin.y = 0f;
                    if (offsetFromOrigin.sqrMagnitude > range * range)
                        candidate = origin + offsetFromOrigin.normalized * range;

                    candidate.y = origin.y;
                    if (!IsLandingClear(owner, candidate))
                        continue;

                    float score = (candidate - requested).sqrMagnitude;
                    if (score >= bestScore)
                        continue;

                    best = candidate;
                    bestScore = score;
                    found = true;
                }
            }

            landing = best;
            return found;
        }

        private static bool IsLandingClear(BrawlerController owner, Vector3 position)
        {
            if (owner == null ||
                !owner.TryGetWorldCollisionProbe(
                    out int collisionMask,
                    out float radius,
                    out float probeHeight,
                    out float skin) ||
                collisionMask == 0)
            {
                return true;
            }

            float castRadius = Mathf.Max(0.05f, radius + skin);
            float height = Mathf.Max(0.05f, probeHeight);
            Vector3 bottom = position + Vector3.up * 0.05f;
            Vector3 top = position + Vector3.up * height;

            int count = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                castRadius,
                LandingOverlapBuffer,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = LandingOverlapBuffer[i];
                if (hit == null ||
                    !hit.enabled ||
                    !hit.gameObject.activeInHierarchy ||
                    hit.transform.IsChildOf(owner.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool CanDamage(BrawlerController owner, ISpatialEntity target)
        {
            if (target.Team == TeamType.Neutral)
                return true;

            return TeamRelationshipUtility.AreEnemies(owner.Team, target.Team);
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
