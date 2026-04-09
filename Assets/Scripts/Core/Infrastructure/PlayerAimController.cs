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

        [Header("Aim")]
        [SerializeField] private bool _showDirectionalPreview = true;
        [SerializeField] private float _defaultRange = 8f;
        [SerializeField] private float _originHeightOffset = 0.25f;

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

            if (!_showDirectionalPreview)
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

            float range = ResolvePreviewRange();

            AimPreviewData data = new AimPreviewData
            {
                IsValid = true,
                Origin = _brawler.transform.position + Vector3.up * _originHeightOffset,
                Direction = aimDirection.normalized,
                Range = range
            };

            _aimIndicatorView.Show(data);
        }

        private float ResolvePreviewRange()
        {
            if (_brawler == null || _brawler.State == null)
                return _defaultRange;

            AbilityDefinition ability = _brawler.State.GetCurrentMainAttackDefinition();
            if (ability == null)
                return _defaultRange;

            if (ability is ProjectileAbilityDefinition projectile)
                return projectile.Range;

            if (ability is BurstSequenceProjectileAbilityDefinition burst)
                return burst.Range;

            if (ability is ChainProjectileAbilityDefinition chain)
                return chain.Range;

            return _defaultRange;
        }
    }
}