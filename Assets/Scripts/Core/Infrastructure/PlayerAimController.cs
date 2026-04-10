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

        [Header("Throwable Preview")]
        [SerializeField] private float _defaultThrowableArcHeight = 1.75f;
        [SerializeField] private float _defaultThrowableRadius = 1.5f;

        [Header("Placement Preview")]
        [SerializeField] private float _defaultPlacementRadius = 0.75f;

        private void Awake()
        {
            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();
        }

        private void Update()
        {
            if (_brawler == null || _aimIndicatorView == null)
                return;

            if (_commandSource == null)
                _commandSource = GetComponent<PlayerCommandSource>();

            if (_commandSource == null)
            {
                _aimIndicatorView.Hide();
                return;
            }

            if (!_commandSource.HasPreviewAim())
            {
                _aimIndicatorView.Hide();
                return;
            }

            Vector3 aimDirection = _commandSource.GetPreviewAimDirection();
            if (aimDirection.sqrMagnitude <= 0.001f)
            {
                _aimIndicatorView.Hide();
                return;
            }

            AimPreviewKind kind = _commandSource.GetPreviewKind();
            AbilityDefinition ability = ResolvePreviewAbility(kind);

            if (ability == null)
            {
                _aimIndicatorView.Hide();
                return;
            }

            AimPreviewData data = BuildPreviewData(kind, ability, aimDirection.normalized);
            _aimIndicatorView.Show(data);
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
            Vector3 playerCenter = _brawler.transform.position + Vector3.up * _originHeightOffset;
            Vector3 previewTargetPoint = _commandSource != null
                ? _commandSource.GetPreviewTargetPoint()
                : _brawler.transform.position + (aimDirection * _defaultRange);

            // Throwable preview
            if (ability is ThrownHybridAoEAbilityDefinition thrown)
            {
                float actualRange = (previewTargetPoint - _brawler.transform.position).magnitude;

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
                    Radius = thrown.ImpactRadius > 0f ? thrown.ImpactRadius : _defaultThrowableRadius
                };
            }

            // Placement preview
            if (ability is EffectAbilityDefinition effectAbility)
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

            // Directional preview
            float directionalRange = ResolveDirectionalRange(ability);

            return new AimPreviewData
            {
                IsValid = true,
                Kind = kind,
                Mode = AimPreviewMode.Directional,
                Origin = playerCenter,
                Direction = aimDirection,
                Range = directionalRange,
                TargetPoint = playerCenter + (aimDirection * directionalRange),
                ArcHeight = 0f,
                Radius = 0f
            };
        }

        private float ResolveDirectionalRange(AbilityDefinition ability)
        {
            if (ability == null)
                return _defaultRange;

            if (ability is ProjectileAbilityDefinition projectile)
                return projectile.Range;

            if (ability is BurstSequenceProjectileAbilityDefinition burst)
                return burst.Range;

            if (ability is ChainProjectileAbilityDefinition chain)
                return chain.Range;

            if (ability is AoEAbilityDefinition aoe)
                return aoe.Radius;

            return _defaultRange;
        }
    }
}