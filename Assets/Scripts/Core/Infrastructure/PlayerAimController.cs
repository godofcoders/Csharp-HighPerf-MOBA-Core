using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private PlayerCommandSource _commandSource;
        [SerializeField] private BrawlerController _brawler;
        [SerializeField] private AimIndicatorView _aimIndicatorView;

        [Header("Fallback")]
        [SerializeField] private float _defaultRange = 8f;
        [SerializeField] private float _originHeightOffset = 0.25f;
        [SerializeField] private float _defaultDirectionalWidth = 1f;
        [SerializeField] private float _minimumVisibleDirectionalRange = 1.15f;

        [Header("Obstacle Clipping")]
        [SerializeField] private LayerMask _aimObstacleMask;
        [SerializeField] private float _traceStartOffset = 0.75f;
        [SerializeField] private float _traceRadiusScale = 0.35f;
        [SerializeField] private float _obstacleSkin = 0.05f;

        [Header("Throwable Preview")]
        [SerializeField] private float _defaultThrowableArcHeight = 1.75f;
        [SerializeField] private float _defaultThrowableRadius = 1.5f;

        [Header("Placement Preview")]
        [SerializeField] private float _defaultPlacementRadius = 0.75f;

        [Header("Smoothing")]
        [SerializeField] private float _directionSmoothingSpeed = 16f;
        [SerializeField] private float _originSmoothingSpeed = 28f;
        [SerializeField] private float _rangeSmoothingSpeed = 24f;

        private bool _hasSmoothedAimDirection;
        private AimPreviewKind _smoothedAimKind = AimPreviewKind.None;
        private Vector3 _smoothedAimDirection = Vector3.forward;
        private bool _hasSmoothedOrigin;
        private Vector3 _smoothedOrigin;
        private bool _hasSmoothedDirectionalRange;
        private AimPreviewKind _smoothedRangeKind = AimPreviewKind.None;
        private float _smoothedDirectionalRange;
        private bool _hasResolvedObstacleMask;
        private int _resolvedObstacleMask;
        private int _handledPreviewCancelSequence;

        private void Awake()
        {
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();
        }

        private void LateUpdate()
        {
            if (_aimIndicatorView == null)
                return;

            if (_brawler == null)
            {
                HidePreview();
                return;
            }

            if (_commandSource == null)
                _commandSource = GetComponent<PlayerCommandSource>();

            if (_commandSource == null)
            {
                HidePreview();
                return;
            }

            if (ConsumePreviewCancellation())
            {
                return;
            }

            if (!_commandSource.HasPreviewAim())
            {
                HidePreview();
                return;
            }

            Vector3 aimDirection = _commandSource.GetPreviewAimDirection();
            if (aimDirection.sqrMagnitude <= 0.001f)
            {
                HidePreview();
                return;
            }

            AimPreviewKind kind = _commandSource.GetPreviewKind();
            AbilityDefinition ability = ResolvePreviewAbility(kind);

            if (ability == null)
            {
                HidePreview();
                return;
            }

            Vector3 smoothedAimDirection = SmoothAimDirection(kind, aimDirection.normalized);
            AimPreviewData data = BuildPreviewData(kind, ability, smoothedAimDirection);
            _aimIndicatorView.Show(data);
        }

        private void OnDisable()
        {
            HidePreview();
        }

        private void OnDestroy()
        {
            HidePreview();
        }

        private AbilityDefinition ResolvePreviewAbility(AimPreviewKind kind)
        {
            if (_brawler == null || _brawler.State == null)
                return null;

            switch (kind)
            {
                case AimPreviewKind.MainAttack:
                    return _brawler.State.GetCurrentMainAttackDefinition();

                case AimPreviewKind.Super:
                    return _brawler.State.GetCurrentSuperDefinition();

                default:
                    return null;
            }
        }

        private AimPreviewData BuildPreviewData(AimPreviewKind kind, AbilityDefinition ability, Vector3 aimDirection)
        {
            Vector3 playerCenter = SmoothPreviewOrigin(_brawler.transform.position + Vector3.up * _originHeightOffset);
            Vector3 previewTargetPoint = _commandSource != null
                ? _commandSource.GetPreviewTargetPoint()
                : _brawler.transform.position + (aimDirection * _defaultRange);

            switch (ability.PreviewMode)
            {
                case AimPreviewMode.Throwable:
                    {
                        float actualRange = (previewTargetPoint - _brawler.transform.position).magnitude;
                        float radius = _defaultThrowableRadius;

                        if (ability is ThrownHybridAoEAbilityDefinition thrown)
                            radius = thrown.ImpactRadius > 0f ? thrown.ImpactRadius : _defaultThrowableRadius;

                        return new AimPreviewData
                        {
                            IsValid = true,
                            Kind = kind,
                            Mode = AimPreviewMode.Throwable,
                            Origin = playerCenter,
                            Direction = aimDirection,
                            Range = actualRange,
                            TargetPoint = previewTargetPoint,
                            ArcHeight = _defaultThrowableArcHeight,
                            Radius = radius
                        };
                    }

                case AimPreviewMode.Placement:
                    {
                        float actualRange = (previewTargetPoint - _brawler.transform.position).magnitude;

                        return new AimPreviewData
                        {
                            IsValid = true,
                            Kind = kind,
                            Mode = AimPreviewMode.Placement,
                            Origin = playerCenter,
                            Direction = aimDirection,
                            Range = actualRange,
                            TargetPoint = previewTargetPoint,
                            ArcHeight = 0f,
                            Radius = _defaultPlacementRadius
                        };
                    }

                case AimPreviewMode.Directional:
                default:
                    {
                        float directionalRange = ResolveDirectionalRange(ability);
                        float previewWidth = ResolveDirectionalWidth(ability);
                        float spreadHalfAngle = ResolveSpreadHalfAngle(ability);
                        float visibleRange = ResolveVisibleDirectionalRange(
                            playerCenter,
                            aimDirection,
                            directionalRange,
                            previewWidth,
                            out bool isObstructed);
                        visibleRange = SmoothDirectionalRange(kind, visibleRange);

                        return new AimPreviewData
                        {
                            IsValid = true,
                            Kind = kind,
                            Mode = AimPreviewMode.Directional,
                            Origin = playerCenter,
                            Direction = aimDirection,
                            Range = visibleRange,
                            Width = previewWidth,
                            IsObstructed = isObstructed,
                            TargetPoint = playerCenter + (aimDirection * visibleRange),
                            ArcHeight = 0f,
                            Radius = 0f,
                            SpreadHalfAngleDegrees = spreadHalfAngle
                        };
                    }
            }
        }

        // Half the total spread angle, in degrees. Returns 0 for non-spread
        // abilities so the preview falls back to a single straight line.
        private float ResolveSpreadHalfAngle(AbilityDefinition ability)
        {
            if (ability is ProjectileAbilityDefinition proj && proj.SpreadAngle > 0f)
                return proj.SpreadAngle * 0.5f;

            if (ability is VolleyProjectileAbilityDefinition volley && volley.SpreadAngle > 0f)
                return volley.SpreadAngle * 0.5f;

            return 0f;
        }

        private float ResolveDirectionalWidth(AbilityDefinition ability)
        {
            float defaultWidth = Mathf.Max(0.05f, _defaultDirectionalWidth);
            if (ability == null)
                return defaultWidth;

            return ability.AimPreviewWidth > 0.01f
                ? ability.AimPreviewWidth
                : defaultWidth;
        }

        private float ResolveDirectionalRange(AbilityDefinition ability)
        {
            if (ability == null)
                return _defaultRange;

            if (ability is BasicProjectileAttackDefinition basicAttack)
                return basicAttack.Range;

            if (ability is BasicSuperDefinition basicSuper)
                return basicSuper.Range;

            if (ability is ProjectileAbilityDefinition projectile)
                return projectile.Range;

            if (ability is VolleyProjectileAbilityDefinition volley)
                return volley.Range;

            if (ability is BurstSequenceProjectileAbilityDefinition burst)
                return burst.Range;

            if (ability is ChainProjectileAbilityDefinition chain)
                return chain.Range;

            if (ability is AoEAbilityDefinition aoe)
                return aoe.Radius;

            return _defaultRange;
        }

        private Vector3 SmoothAimDirection(AimPreviewKind kind, Vector3 rawDirection)
        {
            rawDirection.y = 0f;
            if (rawDirection.sqrMagnitude <= 0.001f)
                return _smoothedAimDirection;

            rawDirection.Normalize();

            if (!_hasSmoothedAimDirection || _smoothedAimKind != kind)
            {
                _hasSmoothedAimDirection = true;
                _smoothedAimKind = kind;
                _smoothedAimDirection = rawDirection;
                return _smoothedAimDirection;
            }

            float speed = Mathf.Max(0f, _directionSmoothingSpeed);
            float t = speed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-speed * Time.deltaTime);

            _smoothedAimDirection = Vector3.Slerp(_smoothedAimDirection, rawDirection, t);
            _smoothedAimDirection.y = 0f;

            if (_smoothedAimDirection.sqrMagnitude <= 0.001f)
                _smoothedAimDirection = rawDirection;
            else
                _smoothedAimDirection.Normalize();

            return _smoothedAimDirection;
        }

        private Vector3 SmoothPreviewOrigin(Vector3 rawOrigin)
        {
            if (!_hasSmoothedOrigin)
            {
                _hasSmoothedOrigin = true;
                _smoothedOrigin = rawOrigin;
                return _smoothedOrigin;
            }

            float speed = Mathf.Max(0f, _originSmoothingSpeed);
            float t = speed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-speed * Time.deltaTime);

            _smoothedOrigin = Vector3.Lerp(_smoothedOrigin, rawOrigin, t);
            return _smoothedOrigin;
        }

        private float SmoothDirectionalRange(AimPreviewKind kind, float targetRange)
        {
            if (!_hasSmoothedDirectionalRange || _smoothedRangeKind != kind)
            {
                _hasSmoothedDirectionalRange = true;
                _smoothedRangeKind = kind;
                _smoothedDirectionalRange = targetRange;
                return _smoothedDirectionalRange;
            }

            float speed = Mathf.Max(0f, _rangeSmoothingSpeed);
            float t = speed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-speed * Time.deltaTime);

            _smoothedDirectionalRange = Mathf.Lerp(_smoothedDirectionalRange, targetRange, t);
            return _smoothedDirectionalRange;
        }

        private float ResolveVisibleDirectionalRange(
            Vector3 origin,
            Vector3 aimDirection,
            float range,
            float previewWidth,
            out bool isObstructed)
        {
            isObstructed = false;

            float safeRange = Mathf.Max(0f, range);
            Vector3 direction = aimDirection;
            direction.y = 0f;

            if (safeRange <= 0.001f || direction.sqrMagnitude <= 0.001f)
                return 0f;

            direction.Normalize();

            if (TryResolvePhysicsPreviewRange(
                    origin,
                    direction,
                    safeRange,
                    previewWidth,
                    out float physicsRange))
            {
                isObstructed = true;
                return ClampVisibleRange(physicsRange, safeRange);
            }

            int obstacleMask = ResolveObstacleMask();
            if (obstacleMask != 0)
                return safeRange;

            AimLineTraceResult gridTrace = TraceDirectionalPreviewGrid(
                origin,
                direction,
                safeRange,
                previewWidth);

            if (!gridTrace.IsBlocked)
                return safeRange;

            isObstructed = true;
            return ClampVisibleRange(gridTrace.ClearDistance, safeRange);
        }

        private bool TryResolvePhysicsPreviewRange(
            Vector3 origin,
            Vector3 direction,
            float range,
            float previewWidth,
            out float visibleRange)
        {
            visibleRange = range;

            int obstacleMask = ResolveObstacleMask();
            if (obstacleMask == 0)
                return false;

            float startOffset = Mathf.Clamp(_traceStartOffset, 0f, Mathf.Max(0f, range - 0.05f));
            float castDistance = Mathf.Max(0f, range - startOffset);
            if (castDistance <= 0.001f)
                return false;

            float radius = Mathf.Max(0.03f, previewWidth * 0.5f * Mathf.Max(0.05f, _traceRadiusScale));
            Vector3 castOrigin = origin + direction * startOffset;

            if (!Physics.SphereCast(
                    castOrigin,
                    radius,
                    direction,
                    out RaycastHit hit,
                    castDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            visibleRange = startOffset + Mathf.Max(0f, hit.distance - Mathf.Max(0f, _obstacleSkin));
            return true;
        }

        private AimLineTraceResult TraceDirectionalPreviewGrid(
            Vector3 origin,
            Vector3 direction,
            float range,
            float previewWidth)
        {
            float startOffset = Mathf.Clamp(_traceStartOffset, 0f, Mathf.Max(0f, range - 0.05f));
            Vector3 traceOrigin = origin + direction * startOffset;
            float traceRange = Mathf.Max(0f, range - startOffset);
            AimLineTraceResult trace = AimLineOfSightUtility.Trace(
                SimulationClock.Pathfinder,
                traceOrigin,
                direction,
                traceRange,
                Mathf.Max(0.03f, previewWidth * 0.5f * Mathf.Max(0.05f, _traceRadiusScale)));

            if (!trace.IsBlocked)
                return trace;

            return new AimLineTraceResult(
                true,
                startOffset + trace.ClearDistance,
                origin + direction * (startOffset + trace.ClearDistance),
                trace.BlockPoint);
        }

        private float ClampVisibleRange(float visibleRange, float maxRange)
        {
            float minimumRange = Mathf.Min(Mathf.Max(0.05f, _minimumVisibleDirectionalRange), maxRange);
            return Mathf.Clamp(visibleRange, minimumRange, maxRange);
        }

        private int ResolveObstacleMask()
        {
            if (_aimObstacleMask.value != 0)
                return _aimObstacleMask.value;

            if (_hasResolvedObstacleMask)
                return _resolvedObstacleMask;

            _hasResolvedObstacleMask = true;
            MapGenerator generator = FindObjectOfType<MapGenerator>();
            _resolvedObstacleMask = generator != null ? generator.ObstacleLayer.value : 0;
            return _resolvedObstacleMask;
        }

        private void HidePreview()
        {
            if (_aimIndicatorView != null)
                _aimIndicatorView.Hide();

            _hasSmoothedAimDirection = false;
            _smoothedAimKind = AimPreviewKind.None;
            _hasSmoothedOrigin = false;
            _hasSmoothedDirectionalRange = false;
            _smoothedRangeKind = AimPreviewKind.None;
        }

        private bool ConsumePreviewCancellation()
        {
            if (_commandSource == null)
                return false;

            int cancelSequence = _commandSource.PreviewCancelSequence;
            if (cancelSequence == _handledPreviewCancelSequence)
                return false;

            _handledPreviewCancelSequence = cancelSequence;
            HidePreview();
            return true;
        }
    }
}
