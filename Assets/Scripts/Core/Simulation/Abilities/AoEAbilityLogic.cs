using UnityEngine;
using System.Collections.Generic;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.Abilities
{
    public class AoEAbilityLogic : IAbilityLogic
    {
        private float _damage;
        private float _radius;

        private readonly List<ISpatialEntity> _targetBuffer = new List<ISpatialEntity>(16);

        public AoEAbilityLogic(float damage, float radius)
        {
            _damage = damage;
            _radius = radius;
        }

        public void Execute(IAbilityUser user, AbilityContext context)
        {
            var damageService = ServiceProvider.Get<IDamageService>();

            if (SimulationClock.Grid == null)
                return;

            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(context.Origin, _radius, _targetBuffer);

            float sqrRadius = _radius * _radius;

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
                        Attacker = user as BrawlerController,
                        Target = target,
                        Damage = _damage,
                        Type = DamageType.AoE,
                        HitPosition = target.Position,
                        Direction = (target.Position - context.Origin).normalized,
                        SourceAbility = null,
                        IsSuper = false
                    });
                }
            }
        }

        public void Tick(uint currentTick) { }
    }
}