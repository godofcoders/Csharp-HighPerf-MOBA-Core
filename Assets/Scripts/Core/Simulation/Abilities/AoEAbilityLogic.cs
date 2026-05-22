using UnityEngine;
using System.Collections.Generic;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.Abilities
{
    public class AoEAbilityLogic : IAbilityLogic
    {
        private readonly float _damage;
        private readonly float _radius;
        private readonly List<ISpatialEntity> _targetBuffer = new List<ISpatialEntity>(16);

        public AoEAbilityLogic(float damage, float radius)
        {
            _damage = damage;
            _radius = radius;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            var damageService = ServiceProvider.Get<IDamageService>();

            if (SimulationClock.Grid == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            _targetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(context.Origin, _radius, _targetBuffer);

            float sqrRadius = _radius * _radius;
            int targetsAffected = 0;

            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                var target = _targetBuffer[i];
                if (!SpatialEntityUtility.IsAlive(target))
                    continue;

                if (user is BrawlerController owner && target.EntityID == owner.EntityID)
                    continue;

                float distSq = (target.Position - context.Origin).sqrMagnitude;
                if (distSq > sqrRadius)
                    continue;

                Vector3 targetPosition = target.Position;
                Vector3 direction = (targetPosition - context.Origin).normalized;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = context.Source,
                    Target = target,
                    Damage = _damage,
                    Type = DamageType.AoE,
                    HitPosition = targetPosition,
                    Direction = direction,
                    SourceAbility = context.AbilityDefinition,
                    IsSuper = context.IsSuper
                });
                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = context.Source,
                    Target = target as BrawlerController,
                    AbilityDefinition = context.AbilityDefinition,
                    SlotType = context.SlotType,
                    Position = targetPosition,
                    Direction = direction,
                    Value = _damage,
                    IsSuper = context.IsSuper
                });

                targetsAffected++;
            }

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.AppliedAreaEffect = true;
            result.TargetsAffected = targetsAffected;
            result.ConsumedResource = true;

            if (targetsAffected > 0)
            {
                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.AreaEffectResolved,
                    Source = context.Source,
                    Target = null,
                    AbilityDefinition = context.AbilityDefinition,
                    SlotType = context.SlotType,
                    Position = context.Origin,
                    Direction = context.Direction,
                    Value = targetsAffected,
                    IsSuper = context.IsSuper
                });
            }

            return result;
        }

        public void Tick(uint currentTick) { }
    }
}
