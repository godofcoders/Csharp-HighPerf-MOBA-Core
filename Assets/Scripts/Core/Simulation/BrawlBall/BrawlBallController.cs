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
    [DefaultExecutionOrder(50)]
    public sealed class BrawlBallController : SimulationEntity
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float BallCollisionSkin = 0.025f;
        private const float BallCollisionEpsilon = 0.001f;

        [Header("Mode")]
        [SerializeField] private BrawlBallMode _mode;

        [Header("Pickup")]
        [SerializeField, Min(0.1f)] private float _pickupRadius = 0.95f;
        [SerializeField, Min(0f)] private float _carrierForwardOffset = 0.72f;
        [SerializeField, Min(0f)] private float _carrierHeightOffset = 0.32f;
        [SerializeField, Min(0f)] private float _dropForwardOffset = 0.72f;

        [Header("Kicking")]
        [SerializeField, Min(0.5f)] private float _normalKickSpeed = 13f;
        [SerializeField, Min(0.5f)] private float _normalKickRange = 9f;
        [SerializeField, Min(0.5f)] private float _superKickSpeed = 18f;
        [SerializeField, Min(0.5f)] private float _superKickRange = 11.5f;
        [SerializeField, Min(0f)] private float _pickupLockoutSeconds = 0.16f;
        [SerializeField, Min(0.01f)] private float _collisionRadius = 0.32f;
        [SerializeField, Min(0f)] private float _collisionHeightOffset = 0.35f;
        [SerializeField] private LayerMask _worldCollisionMask;
        [SerializeField, Range(0.1f, 1f)] private float _bounceVelocityRetain = 0.86f;
        [SerializeField, Range(0, 4)] private int _maxBouncesPerTick = 2;

        [Header("Presentation")]
        [SerializeField] private bool _useRuntimeVisual = true;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Color _ballColor = new Color(0.96f, 0.94f, 0.86f, 1f);
        [SerializeField] private Color _panelColor = new Color(0.04f, 0.04f, 0.04f, 1f);
        [SerializeField, Min(0.1f)] private float _visualDiameter = 0.72f;
        [SerializeField, Min(0.02f)] private float _panelDiameter = 0.2f;

        private readonly List<ISpatialEntity> _pickupCandidates = new List<ISpatialEntity>(16);
        private readonly List<MeshRenderer> _runtimePanelRenderers = new List<MeshRenderer>(8);
        private BrawlerController _carrier;
        private Vector3 _spawnPosition;
        private bool _hasSpawnPosition;
        private MeshRenderer _runtimeRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _looseVelocity;
        private float _remainingTravelDistance;
        private uint _pickupUnlockTick;
        private bool _hasResolvedWorldCollisionMask;
        private int _resolvedWorldCollisionMask;

        protected override TickPhase Phase => TickPhase.PostTick;

        public BrawlerController Carrier => IsValidCarrier(_carrier) ? _carrier : null;
        public bool IsCarried => Carrier != null;
        public bool IsMovingLoose => _carrier == null && _looseVelocity.sqrMagnitude > 0.001f;
        public float PickupRadius => _pickupRadius;
        public float CollisionRadius => _collisionRadius;
        public float NormalKickRange => _normalKickRange;
        public float SuperKickRange => _superKickRange;
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

        private void LateUpdate()
        {
            BrawlerController carrier = Carrier;
            if (carrier == null)
                return;

            if (TryResolveCarrierPose(carrier, usePresentationTarget: true, out Vector3 position))
                transform.position = position;
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
                TryScoreGoal(currentTick);
                return;
            }

            MoveLooseBall();

            if (TryScoreGoal(currentTick))
                return;

            if (currentTick < _pickupUnlockTick)
                return;

            TryPickupNearest();
        }

        private bool TryScoreGoal(uint currentTick)
        {
            return _mode != null &&
                   _mode.TryScoreGoalAt(transform.position, currentTick, out _);
        }

        public bool TryPickupBy(BrawlerController carrier)
        {
            if (!IsValidCarrier(carrier))
                return false;

            _carrier = carrier;
            StopLooseMotion();
            ClearPickupLockout();
            UpdateCarriedTransform(carrier);
            ResetVisualRotation();

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
            StopLooseMotion();
            ClearPickupLockout();
            SetLoosePosition(position);

            if (_mode != null)
                _mode.NotifyBallDropped(transform.position);
            else
                BrawlBallEventBus.RaiseBallDropped(transform.position);
        }

        public void ResetToSpawn()
        {
            _carrier = null;
            StopLooseMotion();
            ClearPickupLockout();
            SetLoosePosition(_spawnPosition);
            ResetVisualRotation();

            if (_mode != null)
                _mode.NotifyBallReset(transform.position);
            else
                BrawlBallEventBus.RaiseBallReset(transform.position);
        }

        public void OverrideSpawnPosition(Vector3 spawnPosition, bool resetIfLoose)
        {
            _spawnPosition = spawnPosition;
            _hasSpawnPosition = true;

            if (resetIfLoose && _carrier == null)
            {
                StopLooseMotion();
                ClearPickupLockout();
                SetLoosePosition(_spawnPosition);
                ResetVisualRotation();
            }
        }

        public void AssignCarrierFromMode(BrawlerController carrier)
        {
            if (!IsValidCarrier(carrier))
            {
                ClearCarrierFromMode();
                return;
            }

            _carrier = carrier;
            StopLooseMotion();
            ClearPickupLockout();
            UpdateCarriedTransform(carrier);
            ResetVisualRotation();
        }

        public void ClearCarrierFromMode()
        {
            _carrier = null;
            StopLooseMotion();
            ClearPickupLockout();
            SetLoosePosition(transform.position);
        }

        public bool KickFromCarrier(
            BrawlerController kicker,
            Vector3 direction,
            bool isSuperKick,
            uint currentTick)
        {
            if (!IsValidCarrier(kicker) || _carrier != kicker)
                return false;

            direction = ResolveKickDirection(kicker, direction);
            if (direction.sqrMagnitude <= 0.001f)
                return false;

            _carrier = null;
            SetLoosePosition(ResolveDropPosition(kicker));

            float speed = isSuperKick ? _superKickSpeed : _normalKickSpeed;
            float range = isSuperKick ? _superKickRange : _normalKickRange;

            _looseVelocity = direction * Mathf.Max(0.5f, speed);
            _remainingTravelDistance = Mathf.Max(0.5f, range);
            _pickupUnlockTick = currentTick + SimulationClock.SecondsToTicks(_pickupLockoutSeconds);

            if (_mode != null)
                _mode.NotifyBallKicked(kicker, transform.position, direction, isSuperKick);
            else
                BrawlBallEventBus.RaiseBallKicked(kicker, transform.position, direction, isSuperKick);

            return true;
        }

        private void MoveLooseBall()
        {
            if (_looseVelocity.sqrMagnitude <= 0.001f || _remainingTravelDistance <= 0.001f)
            {
                StopLooseMotion();
                return;
            }

            float speed = _looseVelocity.magnitude;
            if (speed <= 0.001f)
            {
                StopLooseMotion();
                return;
            }

            float remainingTickSeconds = SimulationClock.TickDeltaTime;
            if (remainingTickSeconds <= BallCollisionEpsilon)
            {
                StopLooseMotion();
                return;
            }

            Vector3 direction = _looseVelocity / speed;
            Vector3 currentPosition = transform.position;
            float consumedDistance = 0f;
            int bouncesUsed = 0;
            int maxBounces = Mathf.Max(0, _maxBouncesPerTick);

            while (remainingTickSeconds > BallCollisionEpsilon &&
                   consumedDistance < _remainingTravelDistance - BallCollisionEpsilon)
            {
                float stepDistance = Mathf.Min(
                    speed * remainingTickSeconds,
                    _remainingTravelDistance - consumedDistance);
                if (stepDistance <= BallCollisionEpsilon)
                    break;

                Vector3 movement = direction * stepDistance;
                if (!TryResolveWorldCollision(
                        currentPosition,
                        movement,
                        out Vector3 resolvedPosition,
                        out Vector3 bounceDirection,
                        out float hitDistance))
                {
                    SetLoosePosition(currentPosition + movement);
                    RollVisual(direction, stepDistance);
                    consumedDistance += stepDistance;
                    break;
                }

                float safeTravelDistance = Vector3.Distance(currentPosition, resolvedPosition);
                SetLoosePosition(resolvedPosition);
                RollVisual(direction, safeTravelDistance);

                float segmentConsumed = Mathf.Clamp(hitDistance, BallCollisionSkin, stepDistance);
                consumedDistance += segmentConsumed;
                remainingTickSeconds = Mathf.Max(0f, remainingTickSeconds - segmentConsumed / speed);
                currentPosition = transform.position;

                if (bounceDirection.sqrMagnitude <= BallCollisionEpsilon || bouncesUsed >= maxBounces)
                {
                    StopLooseMotion();
                    return;
                }

                bouncesUsed++;
                speed *= Mathf.Clamp(_bounceVelocityRetain, 0.1f, 1f);
                if (speed <= BallCollisionEpsilon)
                {
                    StopLooseMotion();
                    return;
                }

                direction = bounceDirection.normalized;
                _looseVelocity = direction * speed;

                if (safeTravelDistance <= BallCollisionEpsilon && remainingTickSeconds > BallCollisionEpsilon)
                {
                    float nudgeDistance = Mathf.Min(
                        BallCollisionSkin,
                        _remainingTravelDistance - consumedDistance);
                    currentPosition += direction * nudgeDistance;
                    SetLoosePosition(currentPosition);
                    consumedDistance += nudgeDistance;
                    remainingTickSeconds = Mathf.Max(0f, remainingTickSeconds - nudgeDistance / speed);
                }
            }

            _remainingTravelDistance -= consumedDistance;

            if (_remainingTravelDistance <= 0.001f)
                StopLooseMotion();
        }

        private bool TryResolveWorldCollision(
            Vector3 previousPosition,
            Vector3 movement,
            out Vector3 resolvedPosition,
            out Vector3 bounceDirection,
            out float hitDistance)
        {
            resolvedPosition = previousPosition + movement;
            bounceDirection = Vector3.zero;
            hitDistance = movement.magnitude;

            int collisionMask = ResolveWorldCollisionMask();
            if (collisionMask == 0)
                return false;

            float distance = movement.magnitude;
            if (distance <= BallCollisionEpsilon)
                return false;

            Vector3 direction = movement / distance;
            Vector3 castOrigin = previousPosition + Vector3.up * Mathf.Max(0f, _collisionHeightOffset);
            float radius = Mathf.Max(0.01f, _collisionRadius);

            if (!Physics.SphereCast(
                    castOrigin,
                    radius,
                    direction,
                    out RaycastHit hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            hitDistance = Mathf.Clamp(hit.distance, 0f, distance);
            resolvedPosition = previousPosition + direction * Mathf.Max(0f, hitDistance - BallCollisionSkin);
            bounceDirection = ResolveBounceDirection(direction, hit.normal);
            return true;
        }

        private static Vector3 ResolveBounceDirection(Vector3 incomingDirection, Vector3 hitNormal)
        {
            incomingDirection.y = 0f;
            if (incomingDirection.sqrMagnitude <= BallCollisionEpsilon)
                return Vector3.zero;

            incomingDirection.Normalize();

            Vector3 horizontalNormal = hitNormal;
            horizontalNormal.y = 0f;
            if (horizontalNormal.sqrMagnitude <= BallCollisionEpsilon)
                horizontalNormal = -incomingDirection;
            else
                horizontalNormal.Normalize();

            Vector3 reflected = Vector3.Reflect(incomingDirection, horizontalNormal);
            reflected.y = 0f;
            if (reflected.sqrMagnitude <= BallCollisionEpsilon)
                reflected = -incomingDirection;

            return reflected.sqrMagnitude > BallCollisionEpsilon
                ? reflected.normalized
                : Vector3.zero;
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
            if (!TryResolveCarrierPose(carrier, usePresentationTarget: false, out Vector3 position))
                return;

            transform.position = position;
        }

        private bool TryResolveCarrierPose(
            BrawlerController carrier,
            bool usePresentationTarget,
            out Vector3 position)
        {
            position = transform.position;
            if (!IsValidCarrier(carrier))
                return false;

            Transform followTarget = usePresentationTarget && carrier.PresentationFollowTarget != null
                ? carrier.PresentationFollowTarget
                : carrier.transform;

            Vector3 forward = followTarget.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            position = followTarget.position +
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

        private Vector3 ResolveKickDirection(BrawlerController kicker, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f && SpatialEntityUtility.IsAlive(kicker))
                direction = kicker.transform.forward;

            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.zero;
        }

        private void SetLoosePosition(Vector3 position)
        {
            if (_hasSpawnPosition)
                position.y = _spawnPosition.y;

            transform.position = position;
        }

        private void StopLooseMotion()
        {
            _looseVelocity = Vector3.zero;
            _remainingTravelDistance = 0f;
        }

        private void RollVisual(Vector3 direction, float distance)
        {
            if (_visualRoot == null || distance <= 0.0001f)
                return;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();
            Vector3 rollAxis = Vector3.Cross(Vector3.up, direction);
            if (rollAxis.sqrMagnitude <= 0.0001f)
                return;

            float radius = Mathf.Max(0.05f, _visualDiameter * 0.5f);
            float degrees = (distance / radius) * Mathf.Rad2Deg;
            _visualRoot.Rotate(rollAxis.normalized, degrees, Space.World);
        }

        private void ResetVisualRotation()
        {
            if (_visualRoot != null)
                _visualRoot.localRotation = Quaternion.identity;
        }

        private void ClearPickupLockout()
        {
            _pickupUnlockTick = 0u;
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

        private int ResolveWorldCollisionMask()
        {
            if (_worldCollisionMask.value != 0)
                return _worldCollisionMask.value;

            if (_hasResolvedWorldCollisionMask)
                return _resolvedWorldCollisionMask;

            _hasResolvedWorldCollisionMask = true;

            MapGenerator mapGenerator = FindObjectOfType<MapGenerator>();
            if (mapGenerator != null && mapGenerator.ObstacleLayer.value != 0)
            {
                _resolvedWorldCollisionMask = mapGenerator.ObstacleLayer.value;
                return _resolvedWorldCollisionMask;
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            _resolvedWorldCollisionMask = obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
            return _resolvedWorldCollisionMask;
        }

        private void EnsureRuntimeVisual()
        {
            if (!_useRuntimeVisual || _visualRoot != null)
                return;

            GameObject root = new GameObject("BallVisualRoot");
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            _visualRoot = root.transform;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "BallSphere";
            visual.layer = gameObject.layer;
            visual.transform.SetParent(_visualRoot, false);
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

            _runtimeRenderer = visual.GetComponent<MeshRenderer>();

            CreateFootballPanels();
            ApplyRuntimeColor();
        }

        private void CreateFootballPanels()
        {
            _runtimePanelRenderers.Clear();

            Vector3[] normals =
            {
                Vector3.up,
                new Vector3(0.78f, 0.18f, 0.58f).normalized,
                new Vector3(-0.78f, 0.18f, 0.58f).normalized,
                new Vector3(0.78f, 0.18f, -0.58f).normalized,
                new Vector3(-0.78f, 0.18f, -0.58f).normalized,
                new Vector3(0f, -0.35f, 1f).normalized,
                new Vector3(0f, -0.35f, -1f).normalized
            };

            float radius = Mathf.Max(0.05f, _visualDiameter * 0.5f);
            float panelDiameter = Mathf.Max(0.02f, _panelDiameter);
            float panelThickness = Mathf.Max(0.006f, _visualDiameter * 0.035f);

            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 normal = normals[i];
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                panel.name = "FootballPanel";
                panel.layer = gameObject.layer;
                panel.transform.SetParent(_visualRoot, false);
                panel.transform.localPosition = normal * (radius + panelThickness * 0.5f);
                panel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
                panel.transform.localScale = new Vector3(panelDiameter, panelThickness, panelDiameter);

                Collider panelCollider = panel.GetComponent<Collider>();
                if (panelCollider != null)
                {
                    if (Application.isPlaying)
                        Destroy(panelCollider);
                    else
                        DestroyImmediate(panelCollider);
                }

                MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
                if (renderer != null)
                    _runtimePanelRenderers.Add(renderer);
            }
        }

        private void ApplyRuntimeColor()
        {
            if (_runtimeRenderer == null && _runtimePanelRenderers.Count == 0)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            ApplyRendererColor(_runtimeRenderer, _ballColor);
            for (int i = 0; i < _runtimePanelRenderers.Count; i++)
                ApplyRendererColor(_runtimePanelRenderers[i], _panelColor);
        }

        private void ApplyRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
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
