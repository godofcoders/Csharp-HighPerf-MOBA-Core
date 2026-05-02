using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Minimal Match HUD scaffold. Reads from MatchManager + GemGrabMode
    /// each frame and writes a single status string to whichever Text
    /// component is wired (TMP_Text preferred, legacy UI.Text as fallback).
    ///
    /// Output format:
    ///   "Blue 3 / Red 7 — Hold 14.2s — Match 1:32"
    ///
    /// Pre-match shows just the match countdown ("Match starts in 3s").
    /// Post-match shows the winner ("Match Over — Winner: Blue").
    ///
    /// This is the foundation stub for the bigger HUD work in plan
    /// Sessions 17-18 (per-brawler health bars, super-charge ring, ammo
    /// indicators, scoreboard, etc.). It exists now so the game is
    /// playable end-to-end during integration playtests; the full HUD
    /// can subsume or replace it.
    /// </summary>
    public class MatchHUD : MonoBehaviour
    {
        [Header("Text targets — assign whichever is in your project")]
        [Tooltip("TextMeshPro target. Preferred if both are wired.")]
        [SerializeField] private TMP_Text _tmpText;
        [Tooltip("Legacy UnityEngine.UI.Text fallback. Used only if TMP target is null.")]
        [SerializeField] private Text _legacyText;

        [Header("Tuning")]
        [Tooltip("Hide the HUD before the match goes Active (lobby/countdown phases).")]
        [SerializeField] private bool _hideBeforeActive = false;

        private void Update()
        {
            string status = ComposeStatus();
            WriteStatus(status);
        }

        private string ComposeStatus()
        {
            MatchManager mm = MatchManager.Instance;
            if (mm == null)
                return "(MatchManager not in scene)";

            switch (mm.CurrentState)
            {
                case MatchState.Waiting:
                    return _hideBeforeActive ? string.Empty : "Waiting for match...";

                case MatchState.CountingDown:
                    return _hideBeforeActive ? string.Empty : "Match starts soon...";

                case MatchState.Ended:
                    return "Match Over";

                case MatchState.Active:
                default:
                    return ComposeActiveStatus();
            }
        }

        private string ComposeActiveStatus()
        {
            GemGrabMode gg = GemGrabMode.Instance;
            if (gg == null)
                return "Match active (no Gem Grab in scene)";

            // Gem totals + win-timer (only show timer when a team is
            // actually counting down).
            string scoreLine = $"Blue {gg.BlueTeamGems} / Red {gg.RedTeamGems}";
            string timerLine = gg.HasLeader
                ? $" — Hold {gg.WinTimerRemainingSeconds:0.0}s ({gg.LeadingTeam})"
                : string.Empty;

            // Match clock formatted as M:SS.
            float t = Mathf.Max(0f, gg.MatchTimeRemainingSeconds);
            int minutes = Mathf.FloorToInt(t / 60f);
            int seconds = Mathf.FloorToInt(t - minutes * 60f);
            string clock = $" — Match {minutes}:{seconds:00}";

            return scoreLine + timerLine + clock;
        }

        private void WriteStatus(string s)
        {
            if (_tmpText != null) _tmpText.text = s;
            else if (_legacyText != null) _legacyText.text = s;
        }
    }
}
