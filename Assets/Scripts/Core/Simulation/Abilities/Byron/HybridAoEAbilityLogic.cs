using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.Abilities
{
    public class HybridAoEAbilityLogic : IAbilityLogic
    {
        private readonly HybridAoEAbilityDefinition _definition;
        private readonly List<ISpatialEntity> _targetBuffer = new List<ISpatialEntity>(16);

        public HybridAoEAbilityLogic(HybridAoEAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null || context.Source == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            if (SimulationClock.Grid == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            var damageService = ServiceProvider.Get<IDamageService>();

            _targetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(context.Origin, _definition.Radius, _targetBuffer);

            float sqrRadius = _definition.Radius * _definition.Radius;
            int targetsAffected = 0;

            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                var target = _targetBuffer[i];
                var targetBrawler = target as BrawlerController;

                if (targetBrawler == null)
                    continue;

                if (context.Source is BrawlerController owner && target.EntityID == owner.EntityID)
                {
                    // Byron super usually can affect self. Keep this allowed.
                    // So do NOT continue here.
                }

                float distSq = (target.Position - context.Origin).sqrMagnitude;
                if (distSq > sqrRadius)
                    continue;

                bool isAlly = targetBrawler.Team == context.Source.Team;

                if (isAlly)
                {
                    targetBrawler.State.Heal(_definition.AllyHeal);

                    Debug.Log($"[HYBRID AOE] {context.Source.name} healed ally {targetBrawler.name} for {_definition.AllyHeal}");

                    CombatPresentationEventBus.Raise(new CombatPresentationEvent
                    {
                        EventType = CombatPresentationEventType.AbilityCastSucceeded,
                        Source = context.Source,
                        Target = targetBrawler,
                        AbilityDefinition = context.AbilityDefinition,
                        SlotType = context.SlotType,
                        Position = target.Position,
                        Direction = (target.Position - context.Origin).normalized,
                        Value = _definition.AllyHeal,
                        IsSuper = context.IsSuper
                    });
                }
                else
                {
                    damageService.ApplyDamage(new DamageContext
                    {
                        Attacker = context.Source,
                        Target = target,
                        Damage = _definition.EnemyDamage,
                        Type = DamageType.AoE,
                        HitPosition = target.Position,
                        Direction = (target.Position - context.Origin).normalized,
                        SourceAbility = context.AbilityDefinition,
                        IsSuper = context.IsSuper
                    });

                    Debug.Log($"[HYBRID AOE] {context.Source.name} damaged enemy {targetBrawler.name} for {_definition.EnemyDamage}");

                    CombatPresentationEventBus.Raise(new CombatPresentationEvent
                    {
                        EventType = CombatPresentationEventType.DamageHit,
                        Source = context.Source,
                        Target = targetBrawler,
                        AbilityDefinition = context.AbilityDefinition,
                        SlotType = context.SlotType,
                        Position = target.Position,
                        Direction = (target.Position - context.Origin).normalized,
                        Value = _definition.EnemyDamage,
                        IsSuper = context.IsSuper
                    });
                }

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

        public void Tick(uint currentTick)
        {
        }
    }
}