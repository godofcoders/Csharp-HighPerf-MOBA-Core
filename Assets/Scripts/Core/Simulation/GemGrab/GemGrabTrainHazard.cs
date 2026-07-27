using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class GemGrabTrainHazard : MonoBehaviour
    {
        private const float BrawlerCacheRefreshSeconds = 0.65f;

        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(8);
        private readonly Dictionary<int, float> _nextDamageTimeByEntity = new Dictionary<int, float>(8);

        private Vector3 _start;
        private Vector3 _end;
        private Vector3 _axis = Vector3.right;
        private Vector3 _lateral = Vector3.forward;
        private float _trackDistance;
        private float _travel;
        private float _speed = 6f;
        private float _damage = 850f;
        private float _hitHalfLength = 1.5f;
        private float _hitHalfWidth = 0.7f;
        private float _damageCooldownSeconds = 1.1f;
        private float _cacheRefreshTimer;
        private bool _configured;

        public void Configure(
            Vector3 start,
            Vector3 end,
            float speed,
            float damage,
            float hitHalfLength,
            float hitHalfWidth,
            float damageCooldownSeconds)
        {
            _start = start;
            _end = end;

            Vector3 delta = _end - _start;
            delta.y = 0f;
            _trackDistance = delta.magnitude;
            if (_trackDistance <= 0.01f)
                return;

            _axis = delta / _trackDistance;
            _lateral = new Vector3(-_axis.z, 0f, _axis.x);
            _speed = Mathf.Max(0.1f, speed);
            _damage = Mathf.Max(0f, damage);
            _hitHalfLength = Mathf.Max(0.1f, hitHalfLength);
            _hitHalfWidth = Mathf.Max(0.1f, hitHalfWidth);
            _damageCooldownSeconds = Mathf.Max(0.1f, damageCooldownSeconds);
            _travel = 0f;
            _configured = true;

            transform.position = _start;
            transform.rotation = Quaternion.LookRotation(_axis, Vector3.up);
            RefreshBrawlerCache();
        }

        private void Update()
        {
            if (!_configured)
                return;

            MoveTrain(Time.deltaTime);

            if (!ShouldDamage())
                return;

            _cacheRefreshTimer += Time.deltaTime;
            if (_cacheRefreshTimer >= BrawlerCacheRefreshSeconds)
            {
                _cacheRefreshTimer = 0f;
                RefreshBrawlerCache();
            }
            else if (_brawlers.Count == 0)
            {
                RefreshBrawlerCache();
            }

            ApplyDamageToOverlaps();
        }

        private void MoveTrain(float deltaTime)
        {
            _travel += deltaTime * _speed;
            float distanceAlongTrack = Mathf.PingPong(_travel, _trackDistance);
            float t = _trackDistance > 0.01f ? distanceAlongTrack / _trackDistance : 0f;
            transform.position = Vector3.Lerp(_start, _end, t);

            float directionSign = Mathf.Repeat(_travel, _trackDistance * 2f) <= _trackDistance ? 1f : -1f;
            Vector3 facing = _axis * directionSign;
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        private void ApplyDamageToOverlaps()
        {
            if (_damage <= 0f || !ServiceProvider.TryGet<IDamageService>(out var damageService))
                return;

            float now = Time.time;
            for (int i = _brawlers.Count - 1; i >= 0; i--)
            {
                BrawlerController brawler = _brawlers[i];
                if (!SpatialEntityUtility.IsAlive(brawler) ||
                    brawler.State == null ||
                    brawler.State.IsDead)
                {
                    _brawlers.RemoveAt(i);
                    continue;
                }

                if (!IsInsideTrainFootprint(brawler))
                    continue;

                int entityId = brawler.EntityID;
                if (_nextDamageTimeByEntity.TryGetValue(entityId, out float nextDamageTime) &&
                    now < nextDamageTime)
                {
                    continue;
                }

                _nextDamageTimeByEntity[entityId] = now + _damageCooldownSeconds;

                Vector3 direction = brawler.Position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.001f)
                    direction = _axis;

                direction.Normalize();
                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = null,
                    Target = brawler,
                    Damage = _damage,
                    Type = DamageType.AoE,
                    HitPosition = brawler.Position,
                    Direction = direction,
                    SourceAbility = null,
                    IsSuper = false
                });

                CombatPresentationEventBus.Raise(new CombatPresentationEvent
                {
                    EventType = CombatPresentationEventType.DamageHit,
                    Source = null,
                    Target = brawler,
                    AbilityDefinition = null,
                    SlotType = default,
                    Position = brawler.Position,
                    Direction = direction,
                    Value = _damage,
                    IsSuper = false,
                    IsHypercharged = false,
                    IsLingeringAreaEffect = false
                });
            }
        }

        private bool IsInsideTrainFootprint(BrawlerController brawler)
        {
            Vector3 delta = brawler.Position - transform.position;
            delta.y = 0f;

            float along = Mathf.Abs(Vector3.Dot(delta, _axis));
            float lateral = Mathf.Abs(Vector3.Dot(delta, _lateral));
            float radius = Mathf.Max(0.05f, brawler.CollisionRadius);

            return along <= _hitHalfLength + radius &&
                   lateral <= _hitHalfWidth + radius;
        }

        private void RefreshBrawlerCache()
        {
            _brawlers.Clear();
            BrawlerController[] discovered = FindObjectsOfType<BrawlerController>(false);
            for (int i = 0; i < discovered.Length; i++)
            {
                if (discovered[i] != null)
                    _brawlers.Add(discovered[i]);
            }
        }

        private static bool ShouldDamage()
        {
            MatchManager matchManager = MatchManager.Instance;
            return matchManager != null &&
                   matchManager.CurrentState == MatchState.Active &&
                   MatchStateUtility.IsCombatResolutionOpen();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_configured)
                return;

            Gizmos.color = new Color(0.95f, 0.1f, 0.05f, 0.7f);
            Gizmos.DrawLine(_start, _end);
            Gizmos.DrawWireCube(
                transform.position,
                new Vector3(_hitHalfLength * 2f, 0.25f, _hitHalfWidth * 2f));
        }
    }
}
