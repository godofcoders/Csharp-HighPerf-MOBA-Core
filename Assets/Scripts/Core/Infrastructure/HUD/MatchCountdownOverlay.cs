using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Bottom-right 5-4-3-2-1-GO badge shown during MatchManager.CountingDown
    /// and for a brief "GO!" hold once the match goes Active. Goal messages
    /// reuse the same root as a larger centered badge.
    ///
    /// Setup:
    ///   1. Full-screen Canvas (Screen Space - Overlay).
    ///   2. Centred Text (TMP preferred).
    ///   3. Add this component to a root GameObject under the Canvas;
    ///      assign _overlayRoot (the GameObject to toggle), _bigText (the
    ///      Text widget — TMP or legacy).
    /// </summary>
    public class MatchCountdownOverlay : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root GameObject toggled on/off. Holds the text + any vignette / dimmer.")]
        [SerializeField] private GameObject _overlayRoot;

        [SerializeField] private TMP_Text _bigTextTmp;
        [SerializeField] private Text _bigTextLegacy;

        [Header("Tuning")]
        [Tooltip("Seconds to keep showing 'GO!' once the match goes Active. Brawl Stars holds it for ~0.5s.")]
        [Min(0f)]
        [SerializeField] private float _goHoldSeconds = 0.6f;

        [Tooltip("Text shown for the final 'GO!' beat. Override to localise.")]
        [SerializeField] private string _goLabel = "GO!";
        [SerializeField, Min(0f)] private float _goalHoldSeconds = 1.1f;
        [SerializeField] private string _blueGoalLabel = "BLUE GOAL!";
        [SerializeField] private string _redGoalLabel = "RED GOAL!";
        [SerializeField] private string _blueRoundWinLabel = "BLUE WINS ROUND";
        [SerializeField] private string _redRoundWinLabel = "RED WINS ROUND";
        [SerializeField] private string _roundDrawLabel = "ROUND DRAW";
        [SerializeField] private string _matchStartsFormat = "MATCH STARTS IN {0}";
        [SerializeField] private Color _countdownBadgeColor = new Color(0f, 0f, 0f, 0.52f);
        [SerializeField] private Color _goalBadgeColor = new Color(0f, 0f, 0f, 0.36f);
        [SerializeField] private Vector2 _countdownBadgeSize = new Vector2(390f, 86f);
        [SerializeField] private Vector2 _countdownBadgePosition = new Vector2(-42f, 126f);
        [SerializeField] private Vector2 _goalBadgeSize = new Vector2(820f, 156f);
        [SerializeField] private int _countdownFontSize = 34;
        [SerializeField] private int _goalFontSize = 76;

        // Tracks when the match transitioned to Active so we know when to
        // stop holding the GO! flash.
        private float _activeStartTime = -1f;
        private float _goalMessageUntil = -1f;
        private string _goalMessage = string.Empty;
        private MatchState _previousState = MatchState.Waiting;
        private RectTransform _rootRect;
        private RectTransform _textRect;
        private Image _rootImage;

        private void OnEnable()
        {
            BrawlBallEventBus.OnGoalScored += HandleBrawlBallGoalScored;
            KnockoutEventBus.OnRoundEnded += HandleKnockoutRoundEnded;
        }

        private void OnDisable()
        {
            BrawlBallEventBus.OnGoalScored -= HandleBrawlBallGoalScored;
            KnockoutEventBus.OnRoundEnded -= HandleKnockoutRoundEnded;
        }

        public void BindOverlay(GameObject overlayRoot, TMP_Text bigTextTmp, Text bigTextLegacy)
        {
            _overlayRoot = overlayRoot;
            _bigTextTmp = bigTextTmp;
            _bigTextLegacy = bigTextLegacy;
            _rootRect = _overlayRoot != null ? _overlayRoot.GetComponent<RectTransform>() : null;
            _rootImage = _overlayRoot != null ? _overlayRoot.GetComponent<Image>() : null;
            _textRect = _bigTextTmp != null
                ? _bigTextTmp.rectTransform
                : _bigTextLegacy != null ? _bigTextLegacy.rectTransform : null;

            SetText(string.Empty);
            Show(false);
        }

        private void Update()
        {
            MatchManager mm = MatchManager.Instance;
            if (mm == null)
            {
                Show(false);
                return;
            }

            // Detect Active-edge to start the GO! hold timer. Cheap state
            // machine — no event subscription needed.
            if (_previousState != MatchState.Active && mm.CurrentState == MatchState.Active)
                _activeStartTime = Time.time;
            _previousState = mm.CurrentState;

            if (_goalMessageUntil > Time.time)
            {
                ApplyGoalPresentation();
                SetText(_goalMessage);
                Show(true);
                return;
            }

            switch (mm.CurrentState)
            {
                case MatchState.CountingDown:
                    {
                        // Show "MATCH STARTS IN 5" down to "1". Ceil keeps
                        // each number visible for its full one-second beat.
                        float remaining = mm.CountdownRemainingSeconds;
                        int n = Mathf.Max(1, Mathf.CeilToInt(remaining));
                        ApplyCountdownPresentation();
                        SetText(string.Format(_matchStartsFormat, n));
                        Show(true);
                        break;
                    }

                case MatchState.Active:
                    {
                        if (_activeStartTime >= 0f && (Time.time - _activeStartTime) <= _goHoldSeconds)
                        {
                            ApplyCountdownPresentation();
                            SetText(_goLabel);
                            Show(true);
                        }
                        else
                        {
                            Show(false);
                        }
                        break;
                    }

                case MatchState.Waiting:
                case MatchState.Ended:
                default:
                    Show(false);
                    break;
            }
        }

        private void Show(bool visible)
        {
            if (_overlayRoot != null && _overlayRoot.activeSelf != visible)
                _overlayRoot.SetActive(visible);
        }

        private void HandleBrawlBallGoalScored(TeamType scoringTeam, int blueGoals, int redGoals)
        {
            if (scoringTeam == TeamType.Blue)
                _goalMessage = _blueGoalLabel;
            else if (scoringTeam == TeamType.Red)
                _goalMessage = _redGoalLabel;
            else
                _goalMessage = "GOAL!";

            _goalMessageUntil = Time.time + Mathf.Max(0f, _goalHoldSeconds);
            ApplyGoalPresentation();
            SetText(_goalMessage);
            Show(true);
        }

        private void HandleKnockoutRoundEnded(
            TeamType winningTeam,
            int roundNumber,
            int blueRoundsWon,
            int redRoundsWon)
        {
            if (winningTeam == TeamType.Blue)
                _goalMessage = _blueRoundWinLabel;
            else if (winningTeam == TeamType.Red)
                _goalMessage = _redRoundWinLabel;
            else
                _goalMessage = _roundDrawLabel;

            _goalMessageUntil = Time.time + Mathf.Max(0f, _goalHoldSeconds);
            ApplyGoalPresentation();
            SetText(_goalMessage);
            Show(true);
        }

        private void ApplyCountdownPresentation()
        {
            ApplyRootLayout(
                new Vector2(1f, 0f),
                _countdownBadgePosition,
                _countdownBadgeSize,
                _countdownBadgeColor);
            ApplyTextLayout(_countdownFontSize, TextAnchor.MiddleCenter, TextAlignmentOptions.Center);
        }

        private void ApplyGoalPresentation()
        {
            ApplyRootLayout(
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                _goalBadgeSize,
                _goalBadgeColor);
            ApplyTextLayout(_goalFontSize, TextAnchor.MiddleCenter, TextAlignmentOptions.Center);
        }

        private void ApplyRootLayout(Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            if (_rootRect != null)
            {
                _rootRect.anchorMin = anchor;
                _rootRect.anchorMax = anchor;
                _rootRect.pivot = anchor;
                _rootRect.anchoredPosition = anchoredPosition;
                _rootRect.sizeDelta = size;
            }

            if (_rootImage != null)
                _rootImage.color = color;
        }

        private void ApplyTextLayout(int fontSize, TextAnchor legacyAlignment, TextAlignmentOptions tmpAlignment)
        {
            if (_textRect != null)
            {
                _textRect.anchorMin = Vector2.zero;
                _textRect.anchorMax = Vector2.one;
                _textRect.offsetMin = new Vector2(14f, 8f);
                _textRect.offsetMax = new Vector2(-14f, -8f);
            }

            if (_bigTextTmp != null)
            {
                _bigTextTmp.fontSize = fontSize;
                _bigTextTmp.alignment = tmpAlignment;
            }
            else if (_bigTextLegacy != null)
            {
                _bigTextLegacy.fontSize = fontSize;
                _bigTextLegacy.alignment = legacyAlignment;
            }
        }

        private void SetText(string s)
        {
            if (_bigTextTmp != null) _bigTextTmp.text = s;
            else if (_bigTextLegacy != null) _bigTextLegacy.text = s;
        }
    }
}
