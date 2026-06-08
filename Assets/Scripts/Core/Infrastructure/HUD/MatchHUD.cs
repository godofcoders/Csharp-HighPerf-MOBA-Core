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

        private void Awake()
        {
            AutoBindTextTargets();
        }

        public void BindTextTargets(TMP_Text tmpText, Text legacyText)
        {
            _tmpText = tmpText;
            _legacyText = legacyText;
            AutoBindTextTargets();
        }

        private void AutoBindTextTargets()
        {
            if (_tmpText == null)
                _tmpText = GetComponent<TMP_Text>();

            if (_legacyText == null)
                _legacyText = GetComponent<Text>();
        }

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

            return MatchHUDFormatter.FormatActiveGemGrabStatus(
                gg.BlueTeamGems,
                gg.RedTeamGems,
                gg.GemsToWin,
                gg.HasLeader,
                gg.LeadingTeam,
                gg.WinTimerRemainingSeconds,
                gg.MatchTimeRemainingSeconds);
        }

        private void WriteStatus(string s)
        {
            if (_tmpText != null) _tmpText.text = s;
            else if (_legacyText != null) _legacyText.text = s;
        }
    }
}
