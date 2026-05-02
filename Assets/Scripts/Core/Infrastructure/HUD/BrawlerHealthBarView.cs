using UnityEngine;
using UnityEngine.UI;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// World-space health bar that floats above a brawler. Drives a UI Image
    /// fill from <see cref="BrawlerState.CurrentHealth"/> / MaxHealth.Value,
    /// billboards toward the main camera each frame, color-codes by team,
    /// and hides while the brawler is dead.
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
    ///        optional _backgroundImage.
    ///
    /// Future-self note: there's a colour-by-perspective pattern (own brawler
    /// green, allies blue, enemies red) that needs "who's the local player"
    /// info. For Phase 1 we color by team identity (Blue team blue, Red team
    /// red). Once a LocalPlayer service exists, swap the colour resolver.
    /// </summary>
    public class BrawlerHealthBarView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The brawler this bar tracks. If null, GetComponentInParent finds it on Awake.")]
        [SerializeField] private BrawlerController _brawlerController;

        [Tooltip("Foreground Image with Image Type = Filled, Horizontal. fillAmount is driven from health ratio.")]
        [SerializeField] private Image _fillImage;

        [Tooltip("Optional background Image. Stays static; included so we can hide the whole bar together.")]
        [SerializeField] private Image _backgroundImage;

        [Tooltip("Canvas to billboard. If null, GetComponentInChildren finds it on Awake.")]
        [SerializeField] private Canvas _canvas;

        [Header("Display rules")]
        [Tooltip("Hide the bar entirely when the brawler is at full health. Reduces visual noise during downtime.")]
        [SerializeField] private bool _hideAtFullHealth = false;

        [Tooltip("Hide the bar while the brawler is dead. Almost always wanted.")]
        [SerializeField] private bool _hideWhileDead = true;

        [Header("Colors")]
        [SerializeField] private Color _blueTeamColor = new Color(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color _redTeamColor = new Color(1.00f, 0.30f, 0.30f);
        [Tooltip("Tint applied when health drops below LowHealthThreshold (lerped over the team color).")]
        [SerializeField] private Color _lowHealthTint = new Color(1.00f, 0.10f, 0.10f);
        [Tooltip("Health ratio at or below which the low-health tint kicks in.")]
        [Range(0f, 1f)]
        [SerializeField] private float _lowHealthThreshold = 0.30f;

        private Camera _camera;

        private void Awake()
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

            // Fill amount + color.
            if (_fillImage != null)
            {
                _fillImage.fillAmount = ratio;
                _fillImage.color = ResolveColor(state.Team, ratio);
            }

            // Billboard toward camera.
            BillboardToCamera();
        }

        private Color ResolveColor(TeamType team, float healthRatio)
        {
            Color baseColor = team == TeamType.Red ? _redTeamColor : _blueTeamColor;

            if (healthRatio > _lowHealthThreshold)
                return baseColor;

            // Lerp toward _lowHealthTint as ratio approaches 0. Below the
            // threshold, t goes 0 (at threshold) → 1 (at zero health).
            float t = 1f - (healthRatio / Mathf.Max(0.0001f, _lowHealthThreshold));
            return Color.Lerp(baseColor, _lowHealthTint, t);
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
            if (_fillImage != null) _fillImage.enabled = visible;
            if (_backgroundImage != null) _backgroundImage.enabled = visible;
        }
    }
}
