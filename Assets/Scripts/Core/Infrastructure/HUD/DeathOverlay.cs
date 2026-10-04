using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Full-screen "You died" overlay shown while the local brawler is
    /// dead. Drives a respawn countdown synced to SpawnManager's
    /// RespawnDelaySeconds.
    ///
    /// Detection: polls IsDead on the local brawler each frame. Captures
    /// Time.time on the false→true transition (death moment) and clears
    /// it on the true→false transition (respawn moment). The countdown is
    /// computed from `Time.time - deathTime` against `RespawnDelaySeconds`.
    ///
    /// Future "killed by X" line is wired but stays empty until kill-credit
    /// tracking lands (would need a LastDamageSource field on BrawlerState
    /// + DamageService writing to it). The killer-text widget gracefully
    /// hides if no killer string is set.
    ///
    /// Setup:
    ///   1. Full-screen Canvas (Screen Space - Overlay).
    ///   2. Optional dimmer Image (semi-transparent black) covering screen.
    ///   3. Centred big "You died" Text + smaller "Respawning in 5" Text.
    ///   4. Drop this on a root GameObject under the Canvas; assign
    ///        _overlayRoot, _titleText (optional, defaults to "You died"),
    ///        _countdownText, optional _killerText.
    /// </summary>
    public class DeathOverlay : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The brawler whose death triggers this overlay. If null, auto-discovers via PlayerCommandSource.")]
        [SerializeField] private BrawlerController _localBrawler;

        [Header("References")]
        [Tooltip("Root GameObject toggled on/off. Holds the dimmer + texts.")]
        [SerializeField] private GameObject _overlayRoot;

        [SerializeField] private TMP_Text _titleTextTmp;
        [SerializeField] private Text _titleTextLegacy;
        [SerializeField] private TMP_Text _countdownTextTmp;
        [SerializeField] private Text _countdownTextLegacy;
        [SerializeField] private TMP_Text _killerTextTmp;
        [SerializeField] private Text _killerTextLegacy;
        [SerializeField] private RectTransform _contentCard;
        [SerializeField] private Image _backdropImage;
        [SerializeField] private Image _accentImage;

        [Header("Tuning")]
        [SerializeField] private string _titleLabel = "You died";
        [Tooltip("Format string for the countdown. {0} = remaining seconds (integer).")]
        [SerializeField] private string _countdownFormat = "Respawning in {0}";
        [SerializeField] private string _knockoutTitleLabel = "Knocked out";
        [SerializeField] private string _spectatingLabel = "Spectating teammate";
        [SerializeField] private float _knockoutNoticeSeconds = 1.25f;

        private float _deathTime = -1f;
        private float _knockoutNoticeUntilTime = -1f;
        private float _nextAutoDiscoverTime;
        private bool _wasDead;
        private bool _spectatingAfterDeath;
        private BrawlerController _spectatedBrawler;
        private RectTransformState _defaultCardLayout;
        private RectTransformState _defaultTitleLayout;
        private RectTransformState _defaultCountdownLayout;
        private RectTransformState _defaultKillerLayout;
        private Color _defaultBackdropColor;
        private Color _defaultAccentColor;
        private float _defaultTitleFontSize;
        private float _defaultCountdownFontSize;
        private float _defaultKillerFontSize;
        private bool _presentationDefaultsCaptured;
        private bool _usingSpectatorPresentation;

        private void Awake()
        {
            if (_localBrawler == null)
                AutoDiscoverBrawler();

            // Title is static — set once at Awake.
            SetTitle(_titleLabel);

            // Killer line stays empty until kill-credit tracking exists.
            SetKiller(string.Empty);

            Show(false);
        }

        private void OnEnable()
        {
            SpawnManager.OnBrawlerRespawned += HandleBrawlerRespawned;
        }

        private void OnDisable()
        {
            SpawnManager.OnBrawlerRespawned -= HandleBrawlerRespawned;
        }

        private void AutoDiscoverBrawler()
        {
            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            if (sources.Length > 0)
            {
                BrawlerController b = sources[0].GetComponent<BrawlerController>();
                if (b != null) { _localBrawler = b; return; }
            }
            BrawlerController[] all = FindObjectsOfType<BrawlerController>();
            if (all.Length > 0) _localBrawler = all[0];
        }

        public void SetLocalBrawler(BrawlerController brawler) => _localBrawler = brawler;

        public void BindOverlay(
            GameObject overlayRoot,
            TMP_Text titleTextTmp,
            Text titleTextLegacy,
            TMP_Text countdownTextTmp,
            Text countdownTextLegacy,
            TMP_Text killerTextTmp,
            Text killerTextLegacy,
            RectTransform contentCard = null,
            Image backdropImage = null,
            Image accentImage = null)
        {
            _overlayRoot = overlayRoot;
            _titleTextTmp = titleTextTmp;
            _titleTextLegacy = titleTextLegacy;
            _countdownTextTmp = countdownTextTmp;
            _countdownTextLegacy = countdownTextLegacy;
            _killerTextTmp = killerTextTmp;
            _killerTextLegacy = killerTextLegacy;
            _contentCard = contentCard;
            _backdropImage = backdropImage;
            _accentImage = accentImage;

            CapturePresentationDefaults();
            SetTitle(_titleLabel);
            SetKiller(string.Empty);
            Show(false);
        }

        /// <summary>Optional: set "killed by X" string (e.g. "Killed by Colt").
        /// Pass empty / null to hide the line.</summary>
        public void SetKilledBy(string killerName)
        {
            SetKiller(string.IsNullOrWhiteSpace(killerName) ? string.Empty : "Killed by " + killerName);
        }

        private void Update()
        {
            TryAutoDiscoverBrawler();

            if (_localBrawler == null || _localBrawler.State == null)
            {
                SetSpectatingPresentation(false);
                Show(false);
                return;
            }

            bool dead = _localBrawler.State.IsDead;

            // Edge transitions: capture death time on false→true; clear on
            // true→false. Cheap state machine.
            if (dead && !_wasDead)
            {
                _deathTime = Time.time;
                _knockoutNoticeUntilTime = Time.time + Mathf.Max(0.1f, _knockoutNoticeSeconds);
                _spectatingAfterDeath = false;
                BrawlerController killer = _localBrawler.State.LastAttacker;
                if (killer != null && killer != _localBrawler && killer.Definition != null)
                {
                    string killerName = !string.IsNullOrWhiteSpace(killer.Definition.BrawlerName)
                        ? killer.Definition.BrawlerName
                        : killer.Definition.name;
                    SetKilledBy(killerName);
                }
            }
            else if (!dead && _wasDead)
            {
                _deathTime = -1f;
                _knockoutNoticeUntilTime = -1f;
                RestorePlayerCameraTarget();
                SetKiller(string.Empty); // wipe killer line on respawn
            }
            _wasDead = dead;

            if (!dead)
            {
                SetSpectatingPresentation(false);
                SetTitle(_titleLabel);
                Show(false);
                return;
            }

            bool duoShowdown = SoloShowdownMode.Instance != null &&
                                SoloShowdownMode.Instance.IsDuoShowdown;
            bool spectatingTeammate = duoShowdown && TrySpectateLivingTeammate();
            if (!ShouldShowRespawnCountdown())
            {
                if (!duoShowdown)
                    spectatingTeammate = TrySpectateLivingTeammate();

                SetSpectatingPresentation(spectatingTeammate);
                SetTitle(_knockoutTitleLabel);
                SetCountdown(spectatingTeammate ? _spectatingLabel : string.Empty);
                Show(Time.time < _knockoutNoticeUntilTime);
                return;
            }

            SetSpectatingPresentation(spectatingTeammate);

            // Compute remaining respawn time.
            float respawnDelay = SpawnManager.Instance != null
                ? SpawnManager.Instance.RespawnDelaySeconds
                : 5f;
            if (SoloShowdownMode.Instance != null &&
                SoloShowdownMode.Instance.IsDuoShowdown)
            {
                respawnDelay = SoloShowdownMode.Instance.RespawnDelaySeconds;
            }
            float elapsed = Time.time - _deathTime;
            float remaining = respawnDelay - elapsed;
            if (remaining < 0f) remaining = 0f;

            string countdownStr = string.Format(_countdownFormat, Mathf.CeilToInt(remaining).ToString());
            SetTitle(spectatingTeammate ? _spectatingLabel : _titleLabel);
            SetCountdown(countdownStr);

            Show(true);
        }

        private void TryAutoDiscoverBrawler()
        {
            if (_localBrawler != null || Time.unscaledTime < _nextAutoDiscoverTime)
                return;

            _nextAutoDiscoverTime = Time.unscaledTime + 0.5f;
            AutoDiscoverBrawler();
        }

        private bool ShouldShowRespawnCountdown()
        {
            if (SoloShowdownMode.Instance != null &&
                SoloShowdownMode.Instance.IsDuoShowdown)
            {
                return SoloShowdownMode.Instance.IsRespawnPending(_localBrawler);
            }

            return SpawnManager.Instance == null || SpawnManager.Instance.AllowAutoRespawn;
        }

        private bool TrySpectateLivingTeammate()
        {
            if (_spectatingAfterDeath && IsAlive(_spectatedBrawler))
                return true;

            BrawlerController teammate = FindLivingTeammate();
            if (teammate == null || teammate.PresentationFollowTarget == null)
            {
                _spectatedBrawler = null;
                _spectatingAfterDeath = false;
                return false;
            }

            CameraController.Instance?.SetTarget(teammate.PresentationFollowTarget);
            _spectatedBrawler = teammate;
            _spectatingAfterDeath = true;
            return true;
        }

        private BrawlerController FindLivingTeammate()
        {
            if (_localBrawler == null)
                return null;

            BrawlerController[] all = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < all.Length; i++)
            {
                BrawlerController candidate = all[i];
                if (candidate == null ||
                    candidate == _localBrawler ||
                    candidate.Team != _localBrawler.Team ||
                    candidate.State == null ||
                    candidate.State.IsDead)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsAlive(BrawlerController brawler)
        {
            return brawler != null && brawler.State != null && !brawler.State.IsDead;
        }

        private void HandleBrawlerRespawned(BrawlerController brawler)
        {
            if (brawler == null)
                return;

            if (_localBrawler == null && brawler.GetComponent<PlayerCommandSource>() != null)
                _localBrawler = brawler;

            if (brawler != _localBrawler)
                return;

            _deathTime = -1f;
            _knockoutNoticeUntilTime = -1f;
            _wasDead = false;
            RestorePlayerCameraTarget(force: true);
            SetSpectatingPresentation(false);
            SetKiller(string.Empty);
            Show(false);
        }

        private void RestorePlayerCameraTarget(bool force = false)
        {
            if ((!force && !_spectatingAfterDeath) || _localBrawler == null)
                return;

            CameraController.Instance?.SetTarget(_localBrawler.PresentationFollowTarget);
            _spectatedBrawler = null;
            _spectatingAfterDeath = false;
        }

        public void SetSpectatingPresentation(bool spectating)
        {
            CapturePresentationDefaults();
            if (!_presentationDefaultsCaptured)
                return;

            if (!spectating)
            {
                if (_usingSpectatorPresentation)
                    RestoreDefaultPresentation();
                return;
            }

            Canvas canvas = _contentCard != null
                ? _contentCard.GetComponentInParent<Canvas>()
                : null;
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            DeathOverlaySpectatorLayout layout =
                DeathOverlayLayoutUtility.ResolveSpectatorLayout(
                    Screen.safeArea,
                    new Vector2(Screen.width, Screen.height),
                    canvasScale);

            _contentCard.anchorMin = Vector2.up;
            _contentCard.anchorMax = Vector2.up;
            _contentCard.pivot = Vector2.up;
            _contentCard.anchoredPosition = layout.AnchoredPosition;
            _contentCard.sizeDelta = layout.CardSize;

            float textWidth = Mathf.Max(0f, layout.CardSize.x - 40f);
            ApplyCompactTextLayout(
                ResolveRectTransform(_titleTextTmp, _titleTextLegacy),
                new Vector2(0f, 33f),
                new Vector2(textWidth, 42f));
            ApplyCompactTextLayout(
                ResolveRectTransform(_countdownTextTmp, _countdownTextLegacy),
                new Vector2(0f, -8f),
                new Vector2(textWidth, 34f));
            ApplyCompactTextLayout(
                ResolveRectTransform(_killerTextTmp, _killerTextLegacy),
                new Vector2(0f, -43f),
                new Vector2(textWidth, 26f));

            SetFontSize(_titleTextTmp, _titleTextLegacy, 29f);
            SetFontSize(_countdownTextTmp, _countdownTextLegacy, 21f);
            SetFontSize(_killerTextTmp, _killerTextLegacy, 16f);

            if (_backdropImage != null)
            {
                Color backdropColor = _defaultBackdropColor;
                backdropColor.a = layout.BackdropAlpha;
                _backdropImage.color = backdropColor;
            }

            if (_accentImage != null)
                _accentImage.color = new Color(0.12f, 0.78f, 1f, 0.94f);

            _usingSpectatorPresentation = true;
        }

        private void CapturePresentationDefaults()
        {
            if (_presentationDefaultsCaptured || _contentCard == null)
                return;

            _defaultCardLayout = new RectTransformState(_contentCard);
            _defaultTitleLayout = new RectTransformState(
                ResolveRectTransform(_titleTextTmp, _titleTextLegacy));
            _defaultCountdownLayout = new RectTransformState(
                ResolveRectTransform(_countdownTextTmp, _countdownTextLegacy));
            _defaultKillerLayout = new RectTransformState(
                ResolveRectTransform(_killerTextTmp, _killerTextLegacy));
            _defaultTitleFontSize = ResolveFontSize(_titleTextTmp, _titleTextLegacy);
            _defaultCountdownFontSize = ResolveFontSize(_countdownTextTmp, _countdownTextLegacy);
            _defaultKillerFontSize = ResolveFontSize(_killerTextTmp, _killerTextLegacy);
            _defaultBackdropColor = _backdropImage != null
                ? _backdropImage.color
                : Color.clear;
            _defaultAccentColor = _accentImage != null
                ? _accentImage.color
                : Color.clear;
            _presentationDefaultsCaptured = true;
        }

        private void RestoreDefaultPresentation()
        {
            _defaultCardLayout.Apply(_contentCard);
            _defaultTitleLayout.Apply(ResolveRectTransform(_titleTextTmp, _titleTextLegacy));
            _defaultCountdownLayout.Apply(ResolveRectTransform(_countdownTextTmp, _countdownTextLegacy));
            _defaultKillerLayout.Apply(ResolveRectTransform(_killerTextTmp, _killerTextLegacy));
            SetFontSize(_titleTextTmp, _titleTextLegacy, _defaultTitleFontSize);
            SetFontSize(_countdownTextTmp, _countdownTextLegacy, _defaultCountdownFontSize);
            SetFontSize(_killerTextTmp, _killerTextLegacy, _defaultKillerFontSize);

            if (_backdropImage != null)
                _backdropImage.color = _defaultBackdropColor;
            if (_accentImage != null)
                _accentImage.color = _defaultAccentColor;

            _usingSpectatorPresentation = false;
        }

        private static void ApplyCompactTextLayout(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static RectTransform ResolveRectTransform(TMP_Text tmp, Text legacy)
        {
            if (tmp != null)
                return tmp.rectTransform;

            return legacy != null ? legacy.rectTransform : null;
        }

        private static float ResolveFontSize(TMP_Text tmp, Text legacy)
        {
            if (tmp != null)
                return tmp.fontSize;

            return legacy != null ? legacy.fontSize : 0f;
        }

        private static void SetFontSize(TMP_Text tmp, Text legacy, float size)
        {
            if (tmp != null)
                tmp.fontSize = size;
            else if (legacy != null)
                legacy.fontSize = Mathf.Max(1, Mathf.RoundToInt(size));
        }

        private void Show(bool visible)
        {
            if (_overlayRoot != null && _overlayRoot.activeSelf != visible)
                _overlayRoot.SetActive(visible);
        }

        private void SetTitle(string s)
        {
            if (_titleTextTmp != null) _titleTextTmp.text = s;
            else if (_titleTextLegacy != null) _titleTextLegacy.text = s;
        }

        private void SetCountdown(string s)
        {
            if (_countdownTextTmp != null) _countdownTextTmp.text = s;
            else if (_countdownTextLegacy != null) _countdownTextLegacy.text = s;
        }

        private void SetKiller(string s)
        {
            if (_killerTextTmp != null)
            {
                _killerTextTmp.text = s;
                _killerTextTmp.gameObject.SetActive(!string.IsNullOrEmpty(s));
            }
            else if (_killerTextLegacy != null)
            {
                _killerTextLegacy.text = s;
                _killerTextLegacy.gameObject.SetActive(!string.IsNullOrEmpty(s));
            }
        }

        private readonly struct RectTransformState
        {
            private readonly Vector2 _anchorMin;
            private readonly Vector2 _anchorMax;
            private readonly Vector2 _pivot;
            private readonly Vector2 _anchoredPosition;
            private readonly Vector2 _sizeDelta;
            private readonly bool _isValid;

            public RectTransformState(RectTransform rect)
            {
                _isValid = rect != null;
                _anchorMin = rect != null ? rect.anchorMin : Vector2.zero;
                _anchorMax = rect != null ? rect.anchorMax : Vector2.zero;
                _pivot = rect != null ? rect.pivot : Vector2.zero;
                _anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
                _sizeDelta = rect != null ? rect.sizeDelta : Vector2.zero;
            }

            public void Apply(RectTransform rect)
            {
                if (!_isValid || rect == null)
                    return;

                rect.anchorMin = _anchorMin;
                rect.anchorMax = _anchorMax;
                rect.pivot = _pivot;
                rect.anchoredPosition = _anchoredPosition;
                rect.sizeDelta = _sizeDelta;
            }
        }
    }
}
