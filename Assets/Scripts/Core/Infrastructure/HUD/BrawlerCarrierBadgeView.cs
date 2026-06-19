using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// World-space "carrier" badge that floats above a brawler whenever
    /// they're holding gems. Mirrors the per-brawler health-bar pattern:
    /// attaches to a child Canvas at head height (above the health bar),
    /// shows a gem icon + count, billboards toward the main camera, hides
    /// when the brawler isn't carrying anything (or is dead).
    ///
    /// Setup (per brawler):
    ///   1. Add a child GameObject above the health bar at head height.
    ///   2. Add a Canvas (Render Mode = World Space, Scale ~0.01).
    ///   3. Add a gem icon Image (sprite up to you).
    ///   4. Add a count Text (TMP preferred) next to the icon.
    ///   5. Drop this component on the Canvas; assign:
    ///        _brawlerController (auto-found in parent if null),
    ///        _badgeRoot — GameObject toggled on/off,
    ///        _gemIcon (optional — for tinting by team color),
    ///        _countTmp or _countLegacy — text component for the count.
    ///
    /// Brawl Stars convention: the icon shows the team color (your gems
    /// look one color, enemy gems another). For Phase 1 we use the same
    /// blue/red team colours as the health bar.
    /// </summary>
    public class BrawlerCarrierBadgeView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The brawler this badge tracks. If null, GetComponentInParent finds it on Awake.")]
        [SerializeField] private BrawlerController _brawlerController;

        [Tooltip("Root GameObject to toggle. Holds the icon + text. Hidden when not carrying.")]
        [SerializeField] private GameObject _badgeRoot;

        [Tooltip("Optional gem icon Image. Tinted by team color.")]
        [SerializeField] private Image _gemIcon;

        [Tooltip("Optional TMP text for carried-gem count. Either this or _countLegacy is used.")]
        [SerializeField] private TMP_Text _countTmp;

        [Tooltip("Optional legacy UI Text fallback for carried-gem count.")]
        [SerializeField] private Text _countLegacy;

        [Tooltip("Canvas to billboard. If null, GetComponent / GetComponentInChildren finds it on Awake.")]
        [SerializeField] private Canvas _canvas;

        [Header("Display rules")]
        [Tooltip("Hide while the brawler is dead. Almost always wanted.")]
        [SerializeField] private bool _hideWhileDead = true;

        [Header("Colors")]
        [SerializeField] private Color _blueTeamColor = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color _redTeamColor = new Color(1.00f, 0.30f, 0.30f);

        private Camera _camera;
        private int _lastCount = -1; // -1 forces first-frame text update

        public void Bind(
            BrawlerController brawlerController,
            GameObject badgeRoot,
            Image gemIcon,
            TMP_Text countTmp,
            Text countLegacy,
            Canvas canvas)
        {
            _brawlerController = brawlerController;
            _badgeRoot = badgeRoot;
            _gemIcon = gemIcon;
            _countTmp = countTmp;
            _countLegacy = countLegacy;
            _canvas = canvas;
            _lastCount = -1;

            if (_gemIcon != null && _brawlerController != null)
                _gemIcon.color = ResolveTeamColor(_brawlerController.Team);

            SetVisible(false);
        }

        private void Awake()
        {
            if (_brawlerController == null)
                _brawlerController = GetComponentInParent<BrawlerController>();

            if (_canvas == null)
                _canvas = GetComponent<Canvas>() ?? GetComponentInChildren<Canvas>();

            // Tint icon to team color once on Awake — team doesn't change
            // mid-match, no need to re-set every frame.
            if (_gemIcon != null && _brawlerController != null)
                _gemIcon.color = ResolveTeamColor(_brawlerController.Team);
        }

        private void LateUpdate()
        {
            if (_brawlerController == null || _brawlerController.State == null)
            {
                SetVisible(false);
                return;
            }

            BrawlerState state = _brawlerController.State;

            if (_hideWhileDead && state.IsDead)
            {
                SetVisible(false);
                return;
            }

            int count = state.CarriedGemCount;
            if (count <= 0)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            // Write text only when count actually changes — saves alloc and
            // string-formatting work on most frames (the count holds steady
            // between pickup and death/cashout).
            if (count != _lastCount)
            {
                string text = count.ToString();
                if (_countTmp != null) _countTmp.text = text;
                else if (_countLegacy != null) _countLegacy.text = text;
                _lastCount = count;
            }

            BillboardToCamera();
        }

        private void SetVisible(bool visible)
        {
            if (_badgeRoot != null && _badgeRoot.activeSelf != visible)
                _badgeRoot.SetActive(visible);

            if (!visible)
                _lastCount = -1; // force re-write next time we show
        }

        private Color ResolveTeamColor(TeamType team)
        {
            return team == TeamType.Red ? _redTeamColor : _blueTeamColor;
        }

        private void BillboardToCamera()
        {
            if (_canvas == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            Transform t = _canvas.transform;
            t.rotation = Quaternion.LookRotation(t.position - _camera.transform.position, Vector3.up);
        }
    }
}
