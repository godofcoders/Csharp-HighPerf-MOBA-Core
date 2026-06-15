using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class SoloShowdownPoisonZone : MonoBehaviour
    {
        public static SoloShowdownPoisonZone Instance { get; private set; }

        [Header("Safe Zone")]
        [SerializeField] private Transform _centerOverride;
        [SerializeField, Min(0.5f)] private float _initialSafeRadius = 24f;
        [SerializeField, Min(0.5f)] private float _finalSafeRadius = 4f;
        [SerializeField, Min(0f)] private float _shrinkDelaySeconds = 20f;
        [SerializeField, Min(1f)] private float _shrinkDurationSeconds = 120f;

        [Header("Poison Damage")]
        [SerializeField, Min(0f)] private float _damagePerTick = 600f;
        [SerializeField, Min(0.1f)] private float _tickIntervalSeconds = 1f;
        [SerializeField, Min(0.1f)] private float _cacheRefreshSeconds = 2f;

        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(12);
        private float _elapsedSeconds;
        private float _tickTimer;
        private float _cacheRefreshTimer;

        public Vector3 Center => _centerOverride != null
            ? _centerOverride.position
            : transform.position;

        public float CurrentSafeRadius { get; private set; }
        public float InitialSafeRadius => _initialSafeRadius;
        public float FinalSafeRadius => _finalSafeRadius;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CurrentSafeRadius = Mathf.Max(_finalSafeRadius, _initialSafeRadius);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (MatchManager.Instance != null &&
                MatchManager.Instance.CurrentState != MatchState.Active)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _elapsedSeconds += deltaTime;
            _tickTimer += deltaTime;
            _cacheRefreshTimer += deltaTime;

            UpdateSafeRadius();

            if (_cacheRefreshTimer >= _cacheRefreshSeconds)
            {
                _cacheRefreshTimer = 0f;
                RefreshBrawlerCache();
            }

            if (_tickTimer >= _tickIntervalSeconds)
            {
                _tickTimer -= _tickIntervalSeconds;
                ApplyPoisonTick();
            }
        }

        public bool IsInsideSafeZone(Vector3 position)
        {
            Vector3 delta = position - Center;
            delta.y = 0f;
            return delta.sqrMagnitude <= CurrentSafeRadius * CurrentSafeRadius;
        }

        public float GetDistanceBeyondSafeZone(Vector3 position)
        {
            Vector3 delta = position - Center;
            delta.y = 0f;
            return Mathf.Max(0f, delta.magnitude - CurrentSafeRadius);
        }

        private void UpdateSafeRadius()
        {
            float shrinkProgress = _elapsedSeconds <= _shrinkDelaySeconds
                ? 0f
                : Mathf.Clamp01((_elapsedSeconds - _shrinkDelaySeconds) /
                                Mathf.Max(0.1f, _shrinkDurationSeconds));

            CurrentSafeRadius = Mathf.Lerp(
                Mathf.Max(_finalSafeRadius, _initialSafeRadius),
                Mathf.Min(_initialSafeRadius, _finalSafeRadius),
                shrinkProgress);
        }

        private void RefreshBrawlerCache()
        {
            _brawlers.Clear();
            BrawlerController[] discovered = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < discovered.Length; i++)
            {
                if (discovered[i] != null)
                    _brawlers.Add(discovered[i]);
            }
        }

        private void ApplyPoisonTick()
        {
            if (_damagePerTick <= 0f)
                return;

            if (_brawlers.Count == 0)
                RefreshBrawlerCache();

            if (!ServiceProvider.TryGet<IDamageService>(out var damageService))
                return;

            Vector3 center = Center;
            for (int i = 0; i < _brawlers.Count; i++)
            {
                BrawlerController brawler = _brawlers[i];
                if (!SpatialEntityUtility.IsAlive(brawler) ||
                    brawler.State == null ||
                    brawler.State.IsDead ||
                    IsInsideSafeZone(brawler.Position))
                {
                    continue;
                }

                Vector3 direction = brawler.Position - center;
                direction.y = 0f;

                damageService.ApplyDamage(new DamageContext
                {
                    Attacker = null,
                    Target = brawler,
                    Damage = _damagePerTick,
                    Type = DamageType.AoE,
                    HitPosition = brawler.Position,
                    Direction = direction.sqrMagnitude > 0.001f
                        ? direction.normalized
                        : Vector3.forward,
                    SourceAbility = null,
                    IsSuper = false
                });
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.1f, 0.8f, 0.35f);
            float radius = Application.isPlaying && CurrentSafeRadius > 0f
                ? CurrentSafeRadius
                : Mathf.Max(_finalSafeRadius, _initialSafeRadius);
            Gizmos.DrawWireSphere(Center, radius);
        }
    }
}
