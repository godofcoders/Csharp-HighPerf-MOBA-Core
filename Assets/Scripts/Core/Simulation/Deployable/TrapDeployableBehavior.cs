using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class TrapDeployableBehavior : IDeployableBehavior
    {
        private readonly List<ISpatialEntity> _targetBuffer = new List<ISpatialEntity>(16);

        private DeployableController _controller;
        private MineTrapDeployableDefinition _definition;
        private Renderer[] _renderers;
        private uint _armTick;
        private uint _detonationTick;
        private bool _armed;
        private bool _triggered;

        public void Initialize(DeployableController controller)
        {
            _controller = controller;
            _definition = controller != null
                ? controller.Definition as MineTrapDeployableDefinition
                : null;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            float armDelay = _definition != null ? Mathf.Max(0f, _definition.ArmDelaySeconds) : 0f;
            _armTick = currentTick + SimulationClock.SecondsToTicks(armDelay);
            _detonationTick = 0;
            _armed = armDelay <= 0f;
            _triggered = false;
            _renderers = controller != null
                ? controller.GetComponentsInChildren<Renderer>(true)
                : null;

            SetVisible(!_armed || !ShouldHideWhenArmed());
        }

        public void Tick(uint currentTick)
        {
            if (_controller == null || _definition == null)
                return;

            if (!_armed && currentTick >= _armTick)
            {
                _armed = true;
                SetVisible(!ShouldHideWhenArmed());
            }

            if (!_armed)
                return;

            if (!_triggered)
            {
                if (!TryFindTriggeringEnemy())
                    return;

                _triggered = true;
                _detonationTick = currentTick + SimulationClock.SecondsToTicks(
                    Mathf.Max(0f, _definition.DetonationDelaySeconds));
                SetVisible(true);
                return;
            }

            if (currentTick < _detonationTick)
                return;

            Explode();
            _controller.Despawn();
        }

        private bool TryFindTriggeringEnemy()
        {
            if (SimulationClock.Grid == null)
                return false;

            _targetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                _controller.Position,
                Mathf.Max(0.1f, _definition.TriggerRadius),
                _targetBuffer);

            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                ISpatialEntity entity = _targetBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    SpatialEntityUtility.IsSameEntity(entity, _controller) ||
                    entity is not BrawlerController brawler ||
                    brawler.State == null ||
                    brawler.State.IsDead)
                {
                    continue;
                }

                if (TeamRelationshipUtility.AreEnemies(_controller.Team, brawler.Team))
                    return true;
            }

            return false;
        }

        private void Explode()
        {
            if (SimulationClock.Grid == null)
                return;

            IDamageService damageService = ServiceProvider.Get<IDamageService>();
            float radius = Mathf.Max(0.1f, _definition.ExplosionRadius);
            int targetsAffected = 0;
            Vector3 minePosition = _controller.Position;

            _targetBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(minePosition, radius + 1f, _targetBuffer);

            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                ISpatialEntity target = _targetBuffer[i];
                if (!SpatialEntityUtility.IsAlive(target) ||
                    SpatialEntityUtility.IsSameEntity(target, _controller) ||
                    !CanDamage(target))
                {
                    continue;
                }

                Vector3 toTarget = target.Position - minePosition;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                float targetRadius = Mathf.Max(0f, target.CollisionRadius);
                if (Mathf.Max(0f, distance - targetRadius) > radius)
                    continue;

                Vector3 direction = distance > 0.001f
                    ? toTarget / distance
                    : Vector3.forward;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = _controller.Owner,
                    Target = target,
                    Damage = _definition.Damage,
                    Type = DamageType.AoE,
                    HitPosition = target.Position,
                    Direction = direction,
                    IsSuper = true
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = _controller.Owner,
                    Target = target as BrawlerController,
                    AbilityDefinition = null,
                    SlotType = AbilitySlotType.Super,
                    Position = target.Position,
                    Direction = direction,
                    Value = _definition.Damage,
                    IsSuper = true
                });

                targetsAffected++;
            }

            CombatPresentationEventBus.Raise(new CombatPresentationEvent
            {
                EventType = CombatPresentationEventType.AreaEffectResolved,
                Source = _controller.Owner,
                Target = null,
                AbilityDefinition = null,
                SlotType = AbilitySlotType.Super,
                Position = minePosition,
                Direction = Vector3.up,
                Value = targetsAffected,
                IsSuper = true
            });
        }

        private bool CanDamage(ISpatialEntity target)
        {
            if (target.Team == TeamType.Neutral)
                return true;

            return TeamRelationshipUtility.AreEnemies(_controller.Team, target.Team);
        }

        private bool ShouldHideWhenArmed()
        {
            return _definition != null && _definition.HideWhenArmed && !_triggered;
        }

        private void SetVisible(bool visible)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }
    }
}
