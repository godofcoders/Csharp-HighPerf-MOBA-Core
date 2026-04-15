using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public class AreaHazardController : MonoBehaviour
    {
        private AreaHazardDefinition _definition;
        private BrawlerController _owner;
        private TeamType _team;
        private AbilityDefinition _sourceAbility;
        private AbilitySlotType _slotType;
        private bool _isSuper;

        private float _elapsedLifetime;
        private float _tickTimer;

        private GameObject _visualInstance;
        private readonly List<ISpatialEntity> _targets = new List<ISpatialEntity>(16);

        public void Initialize(in AreaHazardSpawnRequest request)
        {
            _definition = request.Definition;
            _owner = request.Owner;
            _team = request.Team;
            _sourceAbility = request.SourceAbility;
            _slotType = request.SlotType;
            _isSuper = request.IsSuper;

            transform.position = request.Position;

            BuildVisual();
        }

        private void Update()
        {
            if (_definition == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsedLifetime += Time.deltaTime;
            _tickTimer += Time.deltaTime;

            if (_tickTimer >= _definition.TickIntervalSeconds)
            {
                _tickTimer -= _definition.TickIntervalSeconds;
                ApplyTick();
            }

            if (_elapsedLifetime >= _definition.DurationSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyTick()
        {
            if (SimulationClock.Grid == null || _owner == null)
                return;

            _targets.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(transform.position, _definition.Radius, _targets);

            var damageService = ServiceProvider.Get<IDamageService>();
            if (damageService == null)
                return;

            float sqrRadius = _definition.Radius * _definition.Radius;

            for (int i = 0; i < _targets.Count; i++)
            {
                BrawlerController target = _targets[i] as BrawlerController;
                if (target == null || target.State == null || target.State.IsDead)
                    continue;

                Vector3 delta = target.Position - transform.position;
                delta.y = 0f;

                if (delta.sqrMagnitude > sqrRadius)
                    continue;

                if (!IsValidTarget(target))
                    continue;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = _owner,
                    Target = target,
                    Damage = _definition.DamagePerTick,
                    Type = DamageType.AoE,
                    HitPosition = target.Position,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward,
                    SourceAbility = _sourceAbility,
                    IsSuper = _isSuper
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = _owner,
                    Target = target,
                    AbilityDefinition = _sourceAbility,
                    SlotType = _slotType,
                    Position = target.Position,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward,
                    Value = _definition.DamagePerTick,
                    IsSuper = _isSuper
                });
            }
        }

        private bool IsValidTarget(BrawlerController target)
        {
            switch (_definition.TargetTeamRule)
            {
                case AbilityTargetTeamRule.Enemy:
                    return target.Team != _team;

                case AbilityTargetTeamRule.Ally:
                    return target.Team == _team;

                case AbilityTargetTeamRule.Any:
                    return true;

                default:
                    return false;
            }
        }

        private void BuildVisual()
        {
            if (_definition.VisualPrefab == null)
                return;

            _visualInstance = Instantiate(_definition.VisualPrefab, transform);

            // Keep it centered on the hazard origin
            _visualInstance.transform.localPosition = Vector3.zero;

            // Preserve authored prefab rotation
            Vector3 authoredScale = _visualInstance.transform.localScale;

            float diameter = _definition.Radius * 2f;

            // Multiply authored prefab scale instead of replacing it
            _visualInstance.transform.localScale = new Vector3(
                authoredScale.x * diameter,
                authoredScale.y,
                authoredScale.z * diameter
            );
        }
    }
}