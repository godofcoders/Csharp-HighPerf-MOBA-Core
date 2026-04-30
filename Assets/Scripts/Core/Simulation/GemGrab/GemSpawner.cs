using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Periodically spawns Gem entities at this transform's position. Drop
    /// one of these on a "gem mine" point in the scene; assign the Gem
    /// prefab in the inspector; tune the cadence + cap.
    ///
    /// The spawner enforces a max-active-gems cap so unattended mines don't
    /// flood the map — once the cap is hit, the spawner pauses until some
    /// gems are picked up. Tracking is by reference: each spawned Gem is
    /// added to <see cref="_active"/> and removed when null (Unity
    /// destroyed it post-pickup).
    ///
    /// Day 2 scope: timing + cap + spawn. Day 3+ may add: visual telegraph
    /// before each spawn, randomised cadence within a band, multi-point
    /// spawn rotation per spawner.
    /// </summary>
    public sealed class GemSpawner : SimulationEntity
    {
        [Header("Spawn Settings")]
        [Tooltip("Gem prefab to instantiate. Must have a Gem component.")]
        [SerializeField] private Gem _gemPrefab;

        [Tooltip("Seconds between spawns. Brawl Stars uses ~6s on the gem mine.")]
        [Min(0.1f)]
        [SerializeField] private float _spawnIntervalSeconds = 6.0f;

        [Tooltip("Max number of unpicked-up gems this spawner allows in the world at once.")]
        [Min(1)]
        [SerializeField] private int _maxActiveGems = 4;

        [Tooltip("Spawn position offset from this transform. Useful if the spawner GameObject is the mine root and gems should pop out at a child point.")]
        [SerializeField] private Vector3 _spawnOffset = Vector3.zero;

        // Active gems we've spawned that haven't been picked up yet.
        // Unity nulls the reference when Object.Destroy completes; the
        // pre-spawn cleanup pass below uses that to count live gems.
        private readonly List<Gem> _active = new List<Gem>(8);

        // Tick-counted cooldown. Set on Awake to NextSpawnTick = currentTick
        // + interval-in-ticks at first OnEnable; we lazy-init this on the
        // first Tick() so we don't depend on the clock being ready in Awake.
        private uint _nextSpawnTick;
        private bool _initialized;

        public int ActiveGemCount
        {
            get
            {
                PruneNullReferences();
                return _active.Count;
            }
        }

        public override void Tick(uint currentTick)
        {
            if (!_initialized)
            {
                _nextSpawnTick = currentTick + SimulationClock.SecondsToTicks(_spawnIntervalSeconds);
                _initialized = true;
                return;
            }

            if (currentTick < _nextSpawnTick)
                return;

            // Re-arm the cooldown REGARDLESS of whether we actually spawn —
            // if we're at cap, we still want the next attempt to be paced.
            _nextSpawnTick = currentTick + SimulationClock.SecondsToTicks(_spawnIntervalSeconds);

            PruneNullReferences();
            if (_active.Count >= _maxActiveGems)
                return;

            if (_gemPrefab == null)
                return;

            Vector3 spawnPos = transform.position + _spawnOffset;
            Gem spawned = Object.Instantiate(_gemPrefab, spawnPos, Quaternion.identity);
            _active.Add(spawned);
        }

        private void PruneNullReferences()
        {
            // Walk backwards so removals don't shift indices we still need.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] == null)
                    _active.RemoveAt(i);
            }
        }
    }
}
