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

        [Header("MVP (optional)")]
        [Tooltip("Root toggled on/off depending on whether MatchResultBoard has MVP data. Hidden when no MatchStatsTracker was in the scene.")]
        [SerializeField] private GameObject _mvpRoot;
        [SerializeField] private TMP_Text _mvpNameTmp;
        [SerializeField] private Text _mvpNameLegacy;
        [SerializeField] private TMP_Text _mvpStatsTmp;
        [SerializeField] private Text _mvpStatsLegacy;

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

            // MVP block — show only if MatchResultBoard has a name set.
            bool hasMvp = !string.IsNullOrWhiteSpace(MatchResultBoard.MvpName);
            if (_mvpRoot != null) _mvpRoot.SetActive(hasMvp);
            if (hasMvp)
            {
                MatchStats s = MatchResultBoard.MvpStats;
                string nameLine = "MVP: " + MatchResultBoard.MvpName;
                string statsLine = $"{s.GemsCollected} gems   {s.Kills} kills   {s.Deaths} deaths";

                if (_mvpNameTmp != null) _mvpNameTmp.text = nameLine;
                else if (_mvpNameLegacy != null) _mvpNameLegacy.text = nameLine;

                if (_mvpStatsTmp != null) _mvpStatsTmp.text = statsLine;
                else if (_mvpStatsLegacy != null) _mvpStatsLegacy.text = statsLine;
            }

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

        // MVP snapshot, written by MatchEndRouter from MatchStatsTracker.
        // Empty / 0 if no stats tracker was in the scene.
        public static string MvpName;
        public static MatchStats MvpStats;

        public static void Capture(TeamType winner, int blue, int red)
        {
            WinnerKnown = true;
            Winner = winner;
            BlueScore = blue;
            RedScore = red;
        }

        /// <summary>Optional MVP snapshot — call alongside Capture when a
        /// MatchStatsTracker is in the scene.</summary>
        public static void CaptureMvp(string name, MatchStats stats)
        {
            MvpName = name;
            MvpStats = stats;
        }

        public static void Reset()
        {
            WinnerKnown = false;
            Winner = TeamType.Blue;
            BlueScore = 0;
            RedScore = 0;
            MvpName = string.Empty;
            MvpStats = default;
        }
    }
}
