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

                if (user is BrawlerController owner && target.EntityID == owner.EntityID)
                    continue;

                float distSq = (target.Position - context.Origin).sqrMagnitude;
                if (distSq <= sqrRadius)
                {
                    damageService.ApplyDamage(new DamageContext
                    {
                        Attacker = context.Source,
                        Target = target,
                        Damage = _damage,
                        Type = DamageType.AoE,
                        HitPosition = target.Position,
                        Direction = (target.Position - context.Origin).normalized,
                        SourceAbility = context.AbilityDefinition,
                        IsSuper = context.IsSuper
                    });

                    targetsAffected++;
                }
            }

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.AppliedAreaEffect = true;
            result.TargetsAffected = targetsAffected;
            result.ConsumedResource = true;

            return result;
        }

        public void Tick(uint currentTick) { }
    }
}