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

        [Header("Gem Grab score targets")]
        [SerializeField] private TMP_Text _blueGemTmp;
        [SerializeField] private Text _blueGemLegacy;
        [SerializeField] private TMP_Text _redGemTmp;
        [SerializeField] private Text _redGemLegacy;
        [SerializeField] private TMP_Text _matchTimerTmp;
        [SerializeField] private Text _matchTimerLegacy;
        [SerializeField] private GameObject _blueLeaderHighlight;
        [SerializeField] private GameObject _redLeaderHighlight;

        [Header("Knockout targets")]
        [SerializeField] private GameObject _knockoutWidgetsRoot;
        [SerializeField] private Image[] _blueKnockoutPortraits;
        [SerializeField] private GameObject[] _blueKnockoutCrosses;
        [SerializeField] private Text[] _blueKnockoutLabels;
        [SerializeField] private Image[] _redKnockoutPortraits;
        [SerializeField] private GameObject[] _redKnockoutCrosses;
        [SerializeField] private Text[] _redKnockoutLabels;
        [SerializeField] private Image[] _knockoutRoundMarkers;

        [Header("Tuning")]
        [Tooltip("Hide the HUD before the match goes Active (lobby/countdown phases).")]
        [SerializeField] private bool _hideBeforeActive = false;

        private static readonly Color BlueRoundColor = new Color(0.18f, 0.46f, 1f, 0.94f);
        private static readonly Color RedRoundColor = new Color(1f, 0.22f, 0.28f, 0.94f);
        private static readonly Color EmptyRoundColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color NeutralRoundColor = new Color(1f, 0.82f, 0.28f, 0.62f);
        private static readonly Color VisiblePortraitColor = new Color(1f, 1f, 1f, 0.96f);
        private static readonly Color KnockedPortraitColor = new Color(0.35f, 0.35f, 0.35f, 0.58f);

        private void Awake()
        {
            AutoBindTextTargets();
        }

        public void BindGemScoreWidgets(
            TMP_Text blueGemTmp,
            Text blueGemLegacy,
            TMP_Text redGemTmp,
            Text redGemLegacy,
            TMP_Text matchTimerTmp,
            Text matchTimerLegacy,
            GameObject blueLeaderHighlight,
            GameObject redLeaderHighlight)
        {
            _blueGemTmp = blueGemTmp;
            _blueGemLegacy = blueGemLegacy;
            _redGemTmp = redGemTmp;
            _redGemLegacy = redGemLegacy;
            _matchTimerTmp = matchTimerTmp;
            _matchTimerLegacy = matchTimerLegacy;
            _blueLeaderHighlight = blueLeaderHighlight;
            _redLeaderHighlight = redLeaderHighlight;
        }

        public void BindTextTargets(TMP_Text tmpText, Text legacyText)
        {
            _tmpText = tmpText;
            _legacyText = legacyText;
            AutoBindTextTargets();
        }

        public void BindKnockoutWidgets(
            GameObject knockoutWidgetsRoot,
            Image[] bluePortraits,
            GameObject[] blueCrosses,
            Text[] blueLabels,
            Image[] redPortraits,
            GameObject[] redCrosses,
            Text[] redLabels,
            Image[] roundMarkers)
        {
            _knockoutWidgetsRoot = knockoutWidgetsRoot;
            _blueKnockoutPortraits = bluePortraits;
            _blueKnockoutCrosses = blueCrosses;
            _blueKnockoutLabels = blueLabels;
            _redKnockoutPortraits = redPortraits;
            _redKnockoutCrosses = redCrosses;
            _redKnockoutLabels = redLabels;
            _knockoutRoundMarkers = roundMarkers;
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
            UpdateGemScoreWidgets();
            UpdateKnockoutWidgets(KnockoutMode.Instance);

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
            KnockoutMode knockout = KnockoutMode.Instance;
            if (knockout != null)
                return ComposeKnockoutStatus(knockout);

            BrawlBallMode brawlBall = BrawlBallMode.Instance;
            if (brawlBall != null)
                return $"{ResolveModeMapPrefix()} | " + MatchHUDFormatter.FormatActiveBrawlBallStatus(
                    brawlBall.BlueGoals,
                    brawlBall.RedGoals,
                    brawlBall.GoalsToWin,
                    brawlBall.BallCarrier != null ? brawlBall.BallCarrier.Team : TeamType.Neutral);

            GemGrabMode gg = GemGrabMode.Instance;
            if (gg == null)
                return $"{ResolveModeMapPrefix()} | Match active";

            if (HasDedicatedGemScoreWidgets())
                return $"{ResolveModeMapPrefix()} | " + MatchHUDFormatter.FormatGemGrabHoldStatus(
                    gg.HasLeader,
                    gg.LeadingTeam,
                    gg.WinTimerRemainingSeconds);

            return $"{ResolveModeMapPrefix()} | " + MatchHUDFormatter.FormatActiveGemGrabStatus(
                gg.BlueTeamGems,
                gg.RedTeamGems,
                gg.GemsToWin,
                gg.HasLeader,
                gg.LeadingTeam,
                gg.WinTimerRemainingSeconds,
                gg.MatchTimeRemainingSeconds);
        }

        private string ComposeKnockoutStatus(KnockoutMode knockout)
        {
            int target = Mathf.Max(1, knockout.RoundsToWin);
            int blueSize = knockout.GetDisplayTeamSize(TeamType.Blue);
            int redSize = knockout.GetDisplayTeamSize(TeamType.Red);
            int blueAlive = knockout.GetAliveCount(TeamType.Blue);
            int redAlive = knockout.GetAliveCount(TeamType.Red);

            string status = HasDedicatedGemScoreWidgets()
                ? MatchHUDFormatter.FormatKnockoutRoundStatus(
                    blueAlive,
                    redAlive,
                    blueSize,
                    redSize,
                    knockout.CurrentRound,
                    knockout.IsRoundEnding)
                : MatchHUDFormatter.FormatActiveKnockoutStatus(
                    knockout.BlueRoundsWon,
                    knockout.RedRoundsWon,
                    target,
                    blueAlive,
                    redAlive,
                    blueSize,
                    redSize,
                    knockout.CurrentRound,
                    knockout.IsRoundEnding);

            return $"{ResolveModeMapPrefix()} | {status}";
        }

        private void UpdateGemScoreWidgets()
        {
            if (!HasDedicatedGemScoreWidgets())
                return;

            MatchManager mm = MatchManager.Instance;
            KnockoutMode knockout = KnockoutMode.Instance;
            BrawlBallMode brawlBall = BrawlBallMode.Instance;
            GemGrabMode gg = GemGrabMode.Instance;
            bool active = mm != null && mm.CurrentState == MatchState.Active;

            if (knockout != null && (mm == null || mm.CurrentState != MatchState.Ended))
            {
                int blueTeamSize = knockout.GetDisplayTeamSize(TeamType.Blue);
                int redTeamSize = knockout.GetDisplayTeamSize(TeamType.Red);
                int blueEliminated = knockout.GetEliminatedCount(TeamType.Blue);
                int redEliminated = knockout.GetEliminatedCount(TeamType.Red);

                SetText(_blueGemTmp, _blueGemLegacy, $"{blueEliminated}/{blueTeamSize}");
                SetText(_redGemTmp, _redGemLegacy, $"{redEliminated}/{redTeamSize}");
                SetText(_matchTimerTmp, _matchTimerLegacy, $"R{Mathf.Max(1, knockout.CurrentRound)}");

                SetActive(_blueLeaderHighlight, blueEliminated < redEliminated);
                SetActive(_redLeaderHighlight, redEliminated < blueEliminated);
                return;
            }

            if (brawlBall != null && (mm == null || mm.CurrentState != MatchState.Ended))
            {
                int goalTarget = Mathf.Max(1, brawlBall.GoalsToWin);
                SetText(_blueGemTmp, _blueGemLegacy, $"{brawlBall.BlueGoals}/{goalTarget}");
                SetText(_redGemTmp, _redGemLegacy, $"{brawlBall.RedGoals}/{goalTarget}");
                SetText(_matchTimerTmp, _matchTimerLegacy, brawlBall.BallCarrier != null ? "BALL" : "LOOSE");

                SetActive(_blueLeaderHighlight, brawlBall.BlueGoals > brawlBall.RedGoals);
                SetActive(_redLeaderHighlight, brawlBall.RedGoals > brawlBall.BlueGoals);
                return;
            }

            if (!active || gg == null)
            {
                SetText(_blueGemTmp, _blueGemLegacy, "--");
                SetText(_redGemTmp, _redGemLegacy, "--");
                SetText(_matchTimerTmp, _matchTimerLegacy, "--:--");
                SetActive(_blueLeaderHighlight, false);
                SetActive(_redLeaderHighlight, false);
                return;
            }

            int gemTarget = Mathf.Max(1, gg.GemsToWin);
            SetText(_blueGemTmp, _blueGemLegacy, $"{gg.BlueTeamGems}/{gemTarget}");
            SetText(_redGemTmp, _redGemLegacy, $"{gg.RedTeamGems}/{gemTarget}");
            SetText(
                _matchTimerTmp,
                _matchTimerLegacy,
                MatchHUDFormatter.FormatClock(gg.MatchTimeRemainingSeconds));

            SetActive(_blueLeaderHighlight, gg.HasLeader && gg.LeadingTeam == TeamType.Blue);
            SetActive(_redLeaderHighlight, gg.HasLeader && gg.LeadingTeam == TeamType.Red);
        }

        private bool HasDedicatedGemScoreWidgets()
        {
            return _blueGemTmp != null ||
                   _blueGemLegacy != null ||
                   _redGemTmp != null ||
                   _redGemLegacy != null ||
                   _matchTimerTmp != null ||
                   _matchTimerLegacy != null;
        }

        private void UpdateKnockoutWidgets(KnockoutMode knockout)
        {
            bool visible = knockout != null;
            SetActive(_knockoutWidgetsRoot, visible);
            if (!visible)
                return;

            UpdateKnockoutTeamSlots(
                knockout,
                TeamType.Blue,
                _blueKnockoutPortraits,
                _blueKnockoutCrosses,
                _blueKnockoutLabels);

            UpdateKnockoutTeamSlots(
                knockout,
                TeamType.Red,
                _redKnockoutPortraits,
                _redKnockoutCrosses,
                _redKnockoutLabels);

            if (_knockoutRoundMarkers != null)
            {
                for (int i = 0; i < _knockoutRoundMarkers.Length; i++)
                {
                    Image marker = _knockoutRoundMarkers[i];
                    if (marker == null)
                        continue;

                    marker.color = knockout.HasRoundResult(i)
                        ? ResolveRoundMarkerColor(knockout.GetRoundWinner(i))
                        : EmptyRoundColor;
                }
            }
        }

        private static void UpdateKnockoutTeamSlots(
            KnockoutMode knockout,
            TeamType team,
            Image[] portraits,
            GameObject[] crosses,
            Text[] labels)
        {
            int slotCount = portraits != null ? portraits.Length : 0;
            for (int i = 0; i < slotCount; i++)
            {
                Image portrait = portraits[i];
                GameObject cross = crosses != null && i < crosses.Length ? crosses[i] : null;
                Text label = labels != null && i < labels.Length ? labels[i] : null;

                bool hasBrawler = knockout.TryGetTeamBrawler(team, i, out BrawlerController brawler);
                bool knockedOut = hasBrawler &&
                                  brawler.State != null &&
                                  brawler.State.IsDead;

                if (portrait != null)
                {
                    Sprite sprite = hasBrawler && brawler.Definition != null
                        ? brawler.Definition.Portrait
                        : null;

                    portrait.sprite = sprite;
                    portrait.color = hasBrawler
                        ? knockedOut ? KnockedPortraitColor : VisiblePortraitColor
                        : new Color(1f, 1f, 1f, 0.10f);
                }

                if (label != null)
                {
                    label.text = hasBrawler && brawler.Definition != null
                        ? BuildShortBrawlerLabel(brawler.Definition.BrawlerName, brawler.Definition.name)
                        : string.Empty;
                }

                SetActive(cross, knockedOut);
            }
        }

        private static Color ResolveRoundMarkerColor(TeamType winner)
        {
            if (winner == TeamType.Blue)
                return BlueRoundColor;

            if (winner == TeamType.Red)
                return RedRoundColor;

            return winner == TeamType.Neutral ? NeutralRoundColor : EmptyRoundColor;
        }

        private static string BuildShortBrawlerLabel(string displayName, string fallbackName)
        {
            string source = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : fallbackName;

            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            return source.Length <= 4
                ? source.ToUpperInvariant()
                : source.Substring(0, 4).ToUpperInvariant();
        }

        private static string ResolveModeMapPrefix()
        {
            string mapName = string.Empty;
            if (SceneSelection.SelectedMap != null)
            {
                mapName = !string.IsNullOrWhiteSpace(SceneSelection.SelectedMap.DisplayName)
                    ? SceneSelection.SelectedMap.DisplayName
                    : SceneSelection.SelectedMap.name;
            }

            return MatchHUDFormatter.FormatModeMapPrefix(SceneSelection.SelectedMode, mapName);
        }

        private void WriteStatus(string s)
        {
            if (_tmpText != null) _tmpText.text = s;
            else if (_legacyText != null) _legacyText.text = s;
        }

        private static void SetText(TMP_Text tmp, Text legacy, string text)
        {
            if (tmp != null)
                tmp.text = text;
            else if (legacy != null)
                legacy.text = text;
        }

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }
    }
}
