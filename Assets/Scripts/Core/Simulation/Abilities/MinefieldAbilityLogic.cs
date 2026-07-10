using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public sealed class MinefieldAbilityLogic : IAbilityLogic
    {
        private readonly MinefieldAbilityDefinition _definition;

        public MinefieldAbilityLogic(MinefieldAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null ||
                _definition.MineDefinition == null ||
                user is not BrawlerController owner)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            IDeployableService deployableService = ServiceProvider.Get<IDeployableService>();
            if (deployableService == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            Vector3 forward = ResolveForward(owner, context.Direction);
            Vector3 center = ResolveTargetPoint(context.Origin, forward, context);
            int count = Mathf.Max(1, _definition.MineCount);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = ResolveMinePosition(center, forward, i, count);
                DeployableSpawnRequest request = new DeployableSpawnRequest
                {
                    Owner = owner,
                    Team = owner.Team,
                    Definition = _definition.MineDefinition,
                    Position = position,
                    Direction = forward
                };

                if (deployableService.Spawn(request) != null)
                    spawned++;
            }

            if (spawned <= 0)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.AreaEffectResolved,
                Source = owner,
                Target = null,
                AbilityDefinition = context.AbilityDefinition,
                SlotType = context.SlotType,
                Position = center,
                Direction = forward,
                Value = spawned,
                IsSuper = true,
                IsHypercharged = context.IsHypercharged
            });

            AbilityExecutionResult result = AbilityExecutionResult.Succeeded(
                context.AbilityDefinition,
                context.SlotType);
            result.AppliedAreaEffect = true;
            result.TargetsAffected = spawned;
            result.ConsumedResource = true;
            return result;
        }

        public void Tick(uint currentTick) { }

        private Vector3 ResolveTargetPoint(
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

            Vector3 resolved = origin + offset;
            resolved.y = origin.y;
            return resolved;
        }

        private Vector3 ResolveMinePosition(Vector3 center, Vector3 forward, int index, int count)
        {
            if (count <= 1)
                return center;

            float spacing = Mathf.Max(0f, _definition.MineSpacing);
            float angle = count == 3
                ? 90f + index * 120f
                : index * 360f / count;
            Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * forward * spacing;
            Vector3 position = center + offset;
            position.y = center.y;
            return position;
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
