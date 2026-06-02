using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// World-object representation of a single gem in Gem Grab. Spawned by
    /// the gem-spawner system (Day 2 work) at gem-mine points; picked up
    /// when a brawler walks over it; consumed when picked up.
    ///
    /// Design notes:
    ///   - Inherits SimulationEntity so it auto-registers with the tick
    ///     phase pipeline. Default phase is Movement (matches Deployables).
    ///   - Value is configurable so dropped-on-death gems can carry the
    ///     full count (e.g. a brawler with 3 gems dies → spawn 3 single-
    ///     value gems, OR spawn 1 triple-value gem — design decision
    ///     deferred to the death-drop work).
    ///   - <see cref="IsPickedUp"/> guards against double-consumption
    ///     (proximity ticks fire on multiple brawlers in the same frame).
    ///   - <see cref="TryPickupBy"/> is the API surface for any pickup
    ///     mechanism: a proximity-scan system, a trigger collider on the
    ///     brawler, or a deterministic spatial-grid query during tick.
    ///     Whichever wins the race, IsPickedUp ensures only one transfer
    ///     happens.
    ///
    /// Day 1 scope: entity + pickup API. The actual proximity detection
    /// system lands in Day 2 (spawner + pickup-on-overlap).
    /// </summary>
    public sealed class Gem : SimulationEntity
    {
        [Header("Tuning")]
        [Tooltip("How many gems this world-object grants when picked up. Default 1.")]
        [Min(1)]
        [SerializeField] private int _value = 1;

        [Tooltip("Pickup radius in world units. Brawlers within this distance get the gem.")]
        [Min(0.1f)]
        [SerializeField] private float _pickupRadius = 1.0f;

        // Reusable scratch buffer for the proximity query so we don't alloc
        // per tick. SpatialGrid.GetEntitiesInRadiusNonAlloc fills this.
        private static readonly List<ISpatialEntity> _scratch = new List<ISpatialEntity>(8);

        // Static registry of every live (unpicked) Gem in the scene. Used by
        // AI gem-hunger scoring to detect "is there a gem near me?" without
        // a FindObjectsOfType per scoring call.
        private static readonly List<Gem> _all = new List<Gem>();
        public static IReadOnlyList<Gem> All => _all;

        /// <summary>True if any unpicked Gem exists within `radius` of `origin`.
        /// Linear scan over <see cref="All"/>; cheap because there are only
        /// a handful of live gems on the field at once.</summary>
        public static bool HasAnyUnpickedWithin(Vector3 origin, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _all.Count; i++)
            {
                Gem g = _all[i];
                if (g == null || g.IsPickedUp) continue;
                if ((g.transform.position - origin).sqrMagnitude <= r2)
                    return true;
            }
            return false;
        }

        public static bool TryGetBestUnpickedWithin(
            Vector3 origin,
            float radius,
            out Vector3 position,
            out int value,
            out float distance)
        {
            position = Vector3.zero;
            value = 0;
            distance = 0f;

            if (radius <= 0f)
                return false;

            float radiusSq = radius * radius;
            float bestScore = float.MinValue;
            float bestDistanceSq = 0f;
            bool found = false;

            for (int i = 0; i < _all.Count; i++)
            {
                Gem gem = _all[i];
                if (gem == null || gem.IsPickedUp)
                    continue;

                Vector3 gemPosition = gem.transform.position;
                float dx = gemPosition.x - origin.x;
                float dz = gemPosition.z - origin.z;
                float distanceSq = dx * dx + dz * dz;
                if (distanceSq > radiusSq)
                    continue;

                float score = gem.Value * 8f - distanceSq;
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestDistanceSq = distanceSq;
                    position = gemPosition;
                    value = gem.Value;
                }
            }

            if (!found)
                return false;

            distance = Mathf.Sqrt(bestDistanceSq);
            return true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!_all.Contains(this)) _all.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _all.Remove(this);
        }

        public int Value => _value;
        public bool IsPickedUp { get; private set; }

        /// <summary>Convenience setter used by the gem-spawner / death-drop
        /// pipelines to mint a multi-value gem inline.</summary>
        public void SetValue(int value)
        {
            if (value < 1) value = 1;
            _value = value;
        }

        /// <summary>
        /// Attempts to transfer this gem's value into the supplied brawler's
        /// CarriedGems. Returns true on a clean transfer; returns false if
        /// the gem has already been picked up (race protection) or the
        /// supplied carrier is null.
        ///
        /// On a successful pickup the gem self-destructs via
        /// <see cref="Object.Destroy(Object)"/> so the spatial grid and
        /// simulation registry release it on the next tick boundary.
        /// </summary>
        public bool TryPickupBy(BrawlerState carrier)
        {
            if (IsPickedUp || carrier == null || carrier.IsDead)
                return false;

            IsPickedUp = true;
            carrier.CarriedGems.Add(_value);

            // Fire pickup event so MatchStatsTracker can attribute
            // GemsCollected per brawler.
            GemEventBus.OnGemPickedUp?.Invoke(carrier, _value);

            // Hide immediately so any same-frame proximity check sees a
            // visually-gone gem; Destroy hands off to Unity for the actual
            // GameObject teardown.
            gameObject.SetActive(false);
            Object.Destroy(gameObject);

            return true;
        }

        public override void Tick(uint currentTick)
        {
            if (IsPickedUp)
                return;

            SpatialGrid grid = SimulationClock.Grid;
            if (grid == null)
                return;

            // SpatialGrid uses 3D distance, but brawlers stand at chest
            // height while gems sit on the ground — naive 3D distance
            // would miss overlapping brawlers because of the Y gap. Query
            // with a generous radius (covers up to 4m of vertical brawler
            // height) and then filter by XZ-only distance for the actual
            // pickup decision.
            _scratch.Clear();
            grid.GetEntitiesInRadiusNonAlloc(transform.position, _pickupRadius + 4f, _scratch);

            float pickupRadiusSq = _pickupRadius * _pickupRadius;
            Vector3 gemPos = transform.position;

            for (int i = 0; i < _scratch.Count; i++)
            {
                if (!(_scratch[i] is BrawlerController brawler) ||
                    brawler.State == null || brawler.State.IsDead)
                    continue;

                Vector3 brawlerPos = brawler.Position;
                float dx = brawlerPos.x - gemPos.x;
                float dz = brawlerPos.z - gemPos.z;
                if (dx * dx + dz * dz > pickupRadiusSq)
                    continue;

                if (TryPickupBy(brawler.State))
                    return; // gem consumed
            }
        }
    }
}
