using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Post-match results screen. Reads the last match outcome from
    /// MatchResultBoard (a small static carrier set by the Match scene
    /// before transitioning here). Shows the winner team + final scores.
    /// "Continue" returns to MainMenu; "Rematch" jumps straight back to
    /// the Match scene with the same selection.
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _winnerTextTmp;
        [SerializeField] private Text _winnerTextLegacy;
        [SerializeField] private TMP_Text _scoreTextTmp;
        [SerializeField] private Text _scoreTextLegacy;

        [Header("Buttons")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _rematchButton;

        private void Start()
        {
            string winnerStr = MatchResultBoard.WinnerKnown
                ? $"{MatchResultBoard.Winner} wins!"
                : "Match Over";
            string scoreStr = $"Blue {MatchResultBoard.BlueScore} — Red {MatchResultBoard.RedScore}";

            if (_winnerTextTmp != null) _winnerTextTmp.text = winnerStr;
            else if (_winnerTextLegacy != null) _winnerTextLegacy.text = winnerStr;

            if (_scoreTextTmp != null) _scoreTextTmp.text = scoreStr;
            else if (_scoreTextLegacy != null) _scoreTextLegacy.text = scoreStr;

            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
            if (_rematchButton != null) _rematchButton.onClick.AddListener(OnRematch);
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinue);
            if (_rematchButton != null) _rematchButton.onClick.RemoveListener(OnRematch);
        }

        private void OnContinue() => SceneFlow.Instance?.ReturnToMainMenu();
        private void OnRematch() => SceneFlow.Instance?.LoadScene(SceneId.Match);
    }

    /// <summary>Static carrier for last-match outcome. The Match scene
    /// writes here just before transitioning to Results; the Results screen
    /// reads it on Start. Reset on next match start.</summary>
    public static class MatchResultBoard
    {
        public static bool WinnerKnown;
        public static TeamType Winner;
        public static int BlueScore;
        public static int RedScore;

        public static void Capture(TeamType winner, int blue, int red)
        {
            WinnerKnown = true;
            Winner = winner;
            BlueScore = blue;
            RedScore = red;
        }

        public static void Reset()
        {
            WinnerKnown = false;
            Winner = TeamType.Blue;
            BlueScore = 0;
            RedScore = 0;
        }
    }
}
