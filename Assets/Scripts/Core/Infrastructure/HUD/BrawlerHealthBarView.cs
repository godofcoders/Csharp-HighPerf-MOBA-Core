using UnityEngine;
using UnityEngine.UI;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// World-space health bar that floats above a brawler. Drives a UI Image
    /// fill from <see cref="BrawlerState.CurrentHealth"/> / MaxHealth.Value,
    /// billboards toward the main camera each frame, color-codes by team,
    /// and hides while the brawler is dead. Runtime-authored bars can bind
    /// references via <see cref="Bind"/>; designer-authored bars can keep
    /// wiring fields in the inspector.
    ///
    /// Setup (per brawler):
    ///   1. Create an empty child GameObject on the brawler at head height.
    ///   2. Add a Canvas (Render Mode = World Space). Scale ~0.01.
    ///   3. Add a background Image (dark, slightly larger).
    ///   4. Add a foreground Image set to Filled / Horizontal.
    ///   5. Add this component to the Canvas GameObject; assign:
    ///        _brawlerController (auto-discovered via GetComponentInParent
    ///        if you leave it null),
    ///        _fillImage (the foreground Image),
    ///        optional _backgroundImage and _frameImage.
    ///
    /// </summary>
    public class BrawlerHealthBarView : MonoBehaviour
    {
        private const float PerspectiveRefreshIntervalSeconds = 0.5f;

        private static TeamType _cachedLocalTeam = TeamType.Neutral;
        private static int _cachedLocalEntityId;
        private static float _nextPerspectiveRefreshTime;

        [Header("References")]
        [Tooltip("The brawler this bar tracks. If null, GetComponentInParent finds it on Awake.")]
        [SerializeField] private BrawlerController _brawlerController;

        [Tooltip("Foreground Image with Image Type = Filled, Horizontal. fillAmount is driven from health ratio.")]
        [SerializeField] private Image _fillImage;

        [Tooltip("Optional background Image. Stays static; included so we can hide the whole bar together.")]
        [SerializeField] private Image _backgroundImage;

        [Tooltip("Optional dark frame/edge image for readability over map geometry.")]
        [SerializeField] private Image _frameImage;

        [Tooltip("Canvas to billboard. If null, GetComponentInChildren finds it on Awake.")]
        [SerializeField] private Canvas _canvas;

        [Header("Display rules")]
        [Tooltip("Hide the bar entirely when the brawler is at full health. Reduces visual noise during downtime.")]
        [SerializeField] private bool _hideAtFullHealth = false;

        [Tooltip("Hide the bar while the brawler is dead. Almost always wanted.")]
        [SerializeField] private bool _hideWhileDead = true;

        [Tooltip("Color own brawler green, allies blue, enemies red from the local player's perspective.")]
        [SerializeField] private bool _useLocalPerspectiveColors = true;

        [Header("Colors")]
        [SerializeField] private Color _ownColor = new Color(0.30f, 1.00f, 0.38f);
        [SerializeField] private Color _allyColor = new Color(0.30f, 0.60f, 1.00f);
        [SerializeField] private Color _enemyColor = new Color(1.00f, 0.26f, 0.22f);
        [SerializeField] private Color _neutralColor = new Color(1.00f, 0.85f, 0.25f);
        [SerializeField] private Color _blueTeamFallbackColor = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color _redTeamFallbackColor = new Color(1.00f, 0.30f, 0.30f);
        [Tooltip("Tint applied when health drops below LowHealthThreshold (lerped over the team color).")]
        [SerializeField] private Color _lowHealthTint = new Color(1.00f, 0.08f, 0.06f);
        [SerializeField] private Color _regenerationTint = new Color(0.60f, 1.00f, 0.55f);
        [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.88f);
        [SerializeField] private Color _frameColor = new Color(0f, 0f, 0f, 0.78f);
        [Tooltip("Health ratio at or below which the low-health tint kicks in.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHealthThreshold = 0.30f;

        [Header("Motion")]
        [Min(1f)]
        [SerializeField] private float _fillLerpSpeed = 8f;

        private Camera _camera;
        private float _displayedHealthRatio = 1f;
        private bool _hasDisplayedHealthRatio;

        private void Awake()
        {
            AutoBindReferences();
            ApplyStaticColors();
        }

        public void Bind(
            BrawlerController brawlerController,
            Image fillImage,
            Image backgroundImage,
            Image frameImage,
            Canvas canvas)
        {
            _brawlerController = brawlerController;
            _fillImage = fillImage;
            _backgroundImage = backgroundImage;
            _frameImage = frameImage;
            _canvas = canvas;

            AutoBindReferences();
            ApplyStaticColors();
            _hasDisplayedHealthRatio = false;
        }

        private void AutoBindReferences()
        {
            if (_brawlerController == null)
                _brawlerController = GetComponentInParent<BrawlerController>();

            if (_canvas == null)
                _canvas = GetComponent<Canvas>() ?? GetComponentInChildren<Canvas>();
        }

        private void LateUpdate()
        {
            if (_brawlerController == null || _brawlerController.State == null)
            {
                SetVisible(false);
                return;
            }

            BrawlerState state = _brawlerController.State;

            // Visibility rules first — short-circuit before reading health
            // numbers if we're going to hide.
            bool dead = state.IsDead;
            if (_hideWhileDead && dead)
            {
                SetVisible(false);
                return;
            }

            float maxHealth = Mathf.Max(1f, state.MaxHealth.Value);
            float ratio = Mathf.Clamp01(state.CurrentHealth / maxHealth);

            if (_hideAtFullHealth && ratio >= 0.999f)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            float displayedRatio = ResolveDisplayedRatio(ratio);

            if (_fillImage != null)
            {
                _fillImage.fillAmount = displayedRatio;
                _fillImage.color = ResolveColor(_brawlerController, state, ratio);
            }

            BillboardToCamera();
        }

        private float ResolveDisplayedRatio(float targetRatio)
        {
            if (!_hasDisplayedHealthRatio)
            {
                _displayedHealthRatio = targetRatio;
                _hasDisplayedHealthRatio = true;
                return targetRatio;
            }

            float maxDelta = Mathf.Max(1f, _fillLerpSpeed) * Time.deltaTime;
            _displayedHealthRatio = Mathf.MoveTowards(
                _displayedHealthRatio,
                targetRatio,
                maxDelta);

            return _displayedHealthRatio;
        }

        private Color ResolveColor(
            BrawlerController brawlerController,
            BrawlerState state,
            float healthRatio)
        {
            Color baseColor = ResolveBaseColor(brawlerController, state.Team);

            if (healthRatio <= _lowHealthThreshold)
            {
                float t = 1f - (healthRatio / Mathf.Max(0.0001f, _lowHealthThreshold));
                baseColor = Color.Lerp(baseColor, _lowHealthTint, t);
            }

            if (state.IsHealthRegenerating)
            {
                float pulse = 0.35f + Mathf.PingPong(Time.time * 3.5f, 0.25f);
                baseColor = Color.Lerp(baseColor, _regenerationTint, pulse);
            }

            return baseColor;
        }

        private Color ResolveBaseColor(BrawlerController brawlerController, TeamType team)
        {
            if (!_useLocalPerspectiveColors)
                return ResolveTeamFallbackColor(team);

            RefreshLocalPerspectiveIfDue();

            BrawlerHealthBarPerspective perspective =
                BrawlerHealthBarColorUtility.ResolvePerspective(
                    team,
                    brawlerController != null ? brawlerController.EntityID : 0,
                    _cachedLocalTeam,
                    _cachedLocalEntityId);

            switch (perspective)
            {
                case BrawlerHealthBarPerspective.Own:
                    return _ownColor;
                case BrawlerHealthBarPerspective.Ally:
                    return _allyColor;
                case BrawlerHealthBarPerspective.Enemy:
                    return _enemyColor;
                case BrawlerHealthBarPerspective.Neutral:
                    return _neutralColor;
                case BrawlerHealthBarPerspective.Unknown:
                default:
                    return ResolveTeamFallbackColor(team);
            }
        }

        private Color ResolveTeamFallbackColor(TeamType team)
        {
            if (team == TeamType.Red)
                return _redTeamFallbackColor;

            if (team == TeamType.Neutral)
                return _neutralColor;

            return _blueTeamFallbackColor;
        }

        private void BillboardToCamera()
        {
            if (_canvas == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // Face the camera by aligning canvas forward away from camera.
            // For top-down cameras the canvas already lies roughly flat
            // toward the lens; this keeps it square-on regardless of camera
            // angle changes.
            Transform canvasTransform = _canvas.transform;
            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - _camera.transform.position,
                Vector3.up);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.enabled != visible)
                _canvas.enabled = visible;

            if (_fillImage != null) _fillImage.enabled = visible;
            if (_backgroundImage != null) _backgroundImage.enabled = visible;
            if (_frameImage != null) _frameImage.enabled = visible;
        }

        private void ApplyStaticColors()
        {
            if (_backgroundImage != null)
                _backgroundImage.color = _backgroundColor;

            if (_frameImage != null)
                _frameImage.color = _frameColor;
        }

        private static void RefreshLocalPerspectiveIfDue()
        {
            if (Time.unscaledTime < _nextPerspectiveRefreshTime)
                return;

            _nextPerspectiveRefreshTime =
                Time.unscaledTime + PerspectiveRefreshIntervalSeconds;
            _cachedLocalTeam = TeamType.Neutral;
            _cachedLocalEntityId = 0;

            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                PlayerCommandSource source = sources[i];
                if (source == null)
                    continue;

                BrawlerController brawler = source.GetComponent<BrawlerController>();
                if (brawler == null || brawler.State == null)
                    continue;

                _cachedLocalTeam = brawler.Team;
                _cachedLocalEntityId = brawler.EntityID;
                return;
            }
        }
    }
}
