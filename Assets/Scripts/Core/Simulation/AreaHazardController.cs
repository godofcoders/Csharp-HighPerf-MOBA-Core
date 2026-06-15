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
        private AreaHazardService _hazardService;

        private float _elapsedLifetime;
        private float _tickTimer;

        private GameObject _visualInstance;
        private readonly List<ISpatialEntity> _targets = new List<ISpatialEntity>(16);

        public BrawlerController Owner => _owner;
        public TeamType Team => _team;
        public Vector3 Position => transform.position;
        public float Radius => _definition != null ? _definition.Radius : 0f;
        public float DamagePerTick => _definition != null ? _definition.DamagePerTick : 0f;
        public bool IsSuper => _isSuper;

        public void Initialize(in AreaHazardSpawnRequest request, AreaHazardService hazardService = null)
        {
            _definition = request.Definition;
            _owner = request.Owner;
            _team = request.Team;
            _sourceAbility = request.SourceAbility;
            _slotType = request.SlotType;
            _isSuper = request.IsSuper;
            _hazardService = hazardService;

            transform.position = request.Position;

            BuildVisual();
        }

        private void OnDestroy()
        {
            _hazardService?.Unregister(this);
        }

        public bool CanThreatenTeam(TeamType observerTeam)
        {
            if (_definition == null || _definition.DamagePerTick <= 0f)
                return false;

            switch (_definition.TargetTeamRule)
            {
                case AbilityTargetTeamRule.Enemy:
                    return TeamRelationshipUtility.AreEnemies(_team, observerTeam);

                case AbilityTargetTeamRule.Ally:
                    return TeamRelationshipUtility.AreAllies(_team, observerTeam);

                case AbilityTargetTeamRule.Any:
                    return true;

                default:
                    return false;
            }
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
                ISpatialEntity target = _targets[i];
                if (!SpatialEntityUtility.IsAlive(target))
                    continue;

                BrawlerController targetBrawler = target as BrawlerController;
                BreakableObjectController targetBreakable = target as BreakableObjectController;
                if (targetBrawler == null && targetBreakable == null)
                    continue;

                if (targetBrawler != null && (targetBrawler.State == null || targetBrawler.State.IsDead))
                    continue;

                if (targetBreakable != null && targetBreakable.IsDestroyed)
                    continue;

                Vector3 targetPosition = target.Position;
                Vector3 delta = targetPosition - transform.position;
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
                    HitPosition = targetPosition,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward,
                    SourceAbility = _sourceAbility,
                    IsSuper = _isSuper
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = _owner,
                    Target = targetBrawler,
                    AbilityDefinition = _sourceAbility,
                    SlotType = _slotType,
                    Position = targetPosition,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward,
                    Value = _definition.DamagePerTick,
                    IsSuper = _isSuper
                });
            }
        }

        private bool IsValidTarget(ISpatialEntity target)
        {
            switch (_definition.TargetTeamRule)
            {
                case AbilityTargetTeamRule.Enemy:
                    return target.Team == TeamType.Neutral ||
                           TeamRelationshipUtility.AreEnemies(_team, target.Team);

                case AbilityTargetTeamRule.Ally:
                    return TeamRelationshipUtility.AreAllies(_team, target.Team);

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
