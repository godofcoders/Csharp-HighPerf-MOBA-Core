using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Full-screen 3-2-1-GO overlay shown during MatchManager.CountingDown
    /// and for a brief "GO!" hold once the match goes Active. Drives a
    /// single Text widget; the parent root GameObject is toggled so an
    /// optional vignette/dimmer Image can be attached as a sibling and
    /// it'll show/hide together.
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

        // Tracks when the match transitioned to Active so we know when to
        // stop holding the GO! flash.
        private float _activeStartTime = -1f;
        private float _goalMessageUntil = -1f;
        private string _goalMessage = string.Empty;
        private MatchState _previousState = MatchState.Waiting;

        private void OnEnable()
        {
            BrawlBallEventBus.OnGoalScored += HandleBrawlBallGoalScored;
        }

        private void OnDisable()
        {
            BrawlBallEventBus.OnGoalScored -= HandleBrawlBallGoalScored;
        }

        public void BindOverlay(GameObject overlayRoot, TMP_Text bigTextTmp, Text bigTextLegacy)
        {
            _overlayRoot = overlayRoot;
            _bigTextTmp = bigTextTmp;
            _bigTextLegacy = bigTextLegacy;

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
                SetText(_goalMessage);
                Show(true);
                return;
            }

            switch (mm.CurrentState)
            {
                case MatchState.CountingDown:
                    {
                        // Show "3", "2", "1" — Mathf.CeilToInt so the
                        // character flips at the boundary (3.0s = "3",
                        // 2.99s still reads "3" until it hits 2.99 → 2.99
                        // ceil = 3 actually wait. Floor + 1?
                        // Brawl Stars: number shown = ceil(remaining).
                        // remaining=3.0 → 3, remaining=2.5 → 3, remaining=2.0 → 2.
                        // CeilToInt(2.5)=3, CeilToInt(2.0)=2. Correct.
                        float remaining = mm.CountdownRemainingSeconds;
                        int n = Mathf.Max(1, Mathf.CeilToInt(remaining));
                        SetText(n.ToString());
                        Show(true);
                        break;
                    }

                case MatchState.Active:
                    {
                        if (_activeStartTime >= 0f && (Time.time - _activeStartTime) <= _goHoldSeconds)
                        {
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
            SetText(_goalMessage);
            Show(true);
        }

        private void SetText(string s)
        {
            if (_bigTextTmp != null) _bigTextTmp.text = s;
            else if (_bigTextLegacy != null) _bigTextLegacy.text = s;
        }
    }
}
