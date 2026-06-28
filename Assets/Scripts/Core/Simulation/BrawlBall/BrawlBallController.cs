using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Authoritative runtime ball entity for Brawl Ball. It owns loose-ball
    /// pickup, carrier following, invalid-carrier drops, and center resets;
    /// BrawlBallMode owns score and objective state.
    /// </summary>
    public sealed class BrawlBallController : SimulationEntity
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Mode")]
        [SerializeField] private BrawlBallMode _mode;

        [Header("Pickup")]
        [SerializeField, Min(0.1f)] private float _pickupRadius = 0.95f;
        [SerializeField, Min(0f)] private float _carrierForwardOffset = 0.72f;
        [SerializeField, Min(0f)] private float _carrierHeightOffset = 0.32f;
        [SerializeField, Min(0f)] private float _dropForwardOffset = 0.72f;

        [Header("Presentation")]
        [SerializeField] private bool _useRuntimeVisual = true;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Color _ballColor = new Color(1f, 0.82f, 0.12f, 1f);
        [SerializeField, Min(0.1f)] private float _visualDiameter = 0.72f;

        private readonly List<ISpatialEntity> _pickupCandidates = new List<ISpatialEntity>(16);
        private BrawlerController _carrier;
        private Vector3 _spawnPosition;
        private bool _hasSpawnPosition;
        private MeshRenderer _runtimeRenderer;
        private MaterialPropertyBlock _propertyBlock;

        protected override TickPhase Phase => TickPhase.PostTick;

        public BrawlerController Carrier => IsValidCarrier(_carrier) ? _carrier : null;
        public bool IsCarried => Carrier != null;
        public float PickupRadius => _pickupRadius;
        public Vector3 CurrentPosition => transform.position;

        protected override void Awake()
        {
            base.Awake();
            CaptureSpawnPosition();
            ResolveMode();
            EnsureRuntimeVisual();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CaptureSpawnPosition();
            ResolveMode();
            _mode?.RegisterBall(this);
        }

        protected override void OnDisable()
        {
            _mode?.UnregisterBall(this);
            base.OnDisable();
        }

        public override void Tick(uint currentTick)
        {
            if (_carrier != null)
            {
                if (!IsValidCarrier(_carrier))
                {
                    DropAt(transform.position);
                    return;
                }

                UpdateCarriedTransform(_carrier);
                return;
            }

            TryPickupNearest();
        }

        public bool TryPickupBy(BrawlerController carrier)
        {
            if (!IsValidCarrier(carrier))
                return false;

            _carrier = carrier;
            UpdateCarriedTransform(carrier);

            if (_mode != null)
            {
                _mode.NotifyBallPickedUp(carrier, transform.position);
            }
            else
            {
                BrawlBallEventBus.RaiseCarrierChanged(carrier);
                BrawlBallEventBus.RaiseBallPickedUp(carrier, transform.position);
            }

            return true;
        }

        public void DropFromCarrier()
        {
            if (_carrier == null)
                return;

            DropAt(ResolveDropPosition(_carrier));
        }

        public void DropAt(Vector3 position)
        {
            _carrier = null;
            SetLoosePosition(position);

            if (_mode != null)
                _mode.NotifyBallDropped(transform.position);
            else
                BrawlBallEventBus.RaiseBallDropped(transform.position);
        }

        public void ResetToSpawn()
        {
            _carrier = null;
            SetLoosePosition(_spawnPosition);

            if (_mode != null)
                _mode.NotifyBallReset(transform.position);
            else
                BrawlBallEventBus.RaiseBallReset(transform.position);
        }

        public void AssignCarrierFromMode(BrawlerController carrier)
        {
            if (!IsValidCarrier(carrier))
            {
                ClearCarrierFromMode();
                return;
            }

            _carrier = carrier;
            UpdateCarriedTransform(carrier);
        }

        public void ClearCarrierFromMode()
        {
            _carrier = null;
            SetLoosePosition(transform.position);
        }

        private void TryPickupNearest()
        {
            SpatialGrid grid = SimulationClock.Grid;
            if (grid == null)
                return;

            _pickupCandidates.Clear();
            grid.GetEntitiesInRadiusNonAlloc(transform.position, _pickupRadius + 4f, _pickupCandidates);

            float bestDistanceSq = float.MaxValue;
            BrawlerController bestCarrier = null;
            Vector3 ballPosition = transform.position;
            float pickupRadiusSq = _pickupRadius * _pickupRadius;

            for (int i = 0; i < _pickupCandidates.Count; i++)
            {
                if (!(_pickupCandidates[i] is BrawlerController brawler) ||
                    !IsValidCarrier(brawler))
                {
                    continue;
                }

                Vector3 brawlerPosition = brawler.Position;
                float dx = brawlerPosition.x - ballPosition.x;
                float dz = brawlerPosition.z - ballPosition.z;
                float distanceSq = dx * dx + dz * dz;
                if (distanceSq > pickupRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestCarrier = brawler;
            }

            if (bestCarrier != null)
                TryPickupBy(bestCarrier);
        }

        private void UpdateCarriedTransform(BrawlerController carrier)
        {
            if (!TryResolveCarrierPose(carrier, out Vector3 position))
                return;

            transform.position = position;
        }

        private bool TryResolveCarrierPose(BrawlerController carrier, out Vector3 position)
        {
            position = transform.position;
            if (!IsValidCarrier(carrier))
                return false;

            Vector3 forward = carrier.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            position = carrier.Position +
                       forward * _carrierForwardOffset +
                       Vector3.up * _carrierHeightOffset;
            return true;
        }

        private Vector3 ResolveDropPosition(BrawlerController carrier)
        {
            if (!SpatialEntityUtility.IsAlive(carrier))
                return transform.position;

            Vector3 forward = carrier.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            Vector3 position = carrier.Position + forward * _dropForwardOffset;
            position.y = transform.position.y;
            return position;
        }

        private void SetLoosePosition(Vector3 position)
        {
            if (_hasSpawnPosition)
                position.y = _spawnPosition.y;

            transform.position = position;
        }

        private void CaptureSpawnPosition()
        {
            if (_hasSpawnPosition)
                return;

            _spawnPosition = transform.position;
            _hasSpawnPosition = true;
        }

        private void ResolveMode()
        {
            if (_mode != null)
                return;

            _mode = GetComponentInParent<BrawlBallMode>();
            if (_mode == null)
                _mode = BrawlBallMode.Instance;
        }

        private void EnsureRuntimeVisual()
        {
            if (!_useRuntimeVisual || _visualRoot != null)
                return;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "BallVisual";
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * _visualDiameter;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(visualCollider);
                else
                    DestroyImmediate(visualCollider);
            }

            _visualRoot = visual.transform;
            _runtimeRenderer = visual.GetComponent<MeshRenderer>();
            ApplyRuntimeColor();
        }

        private void ApplyRuntimeColor()
        {
            if (_runtimeRenderer == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            _runtimeRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, _ballColor);
            _propertyBlock.SetColor(BaseColorId, _ballColor);
            _runtimeRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static bool IsValidCarrier(BrawlerController carrier)
        {
            return SpatialEntityUtility.IsAlive(carrier) &&
                   carrier.State != null &&
                   !carrier.State.IsDead &&
                   carrier.gameObject.activeInHierarchy;
        }
    }
}
