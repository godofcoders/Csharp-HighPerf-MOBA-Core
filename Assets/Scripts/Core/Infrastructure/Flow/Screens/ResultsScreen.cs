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

        private static Font _runtimeFont;

        private void Start()
        {
            string winnerStr = MatchResultBoard.WinnerKnown
                ? $"{MatchResultBoard.Winner} wins!"
                : (MatchResultBoard.Draw ? "Draw" : "Match Over");
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
                string nameLine = "STAR PLAYER: " + MatchResultBoard.MvpName;
                string statsLine = $"{s.Kills}/{s.Deaths}/{s.Assists} KDA   {Mathf.RoundToInt(s.DamageDealt)} damage   {s.GemsCollected} gems";

                if (_mvpNameTmp != null) _mvpNameTmp.text = nameLine;
                else if (_mvpNameLegacy != null) _mvpNameLegacy.text = nameLine;

                if (_mvpStatsTmp != null) _mvpStatsTmp.text = statsLine;
                else if (_mvpStatsLegacy != null) _mvpStatsLegacy.text = statsLine;
            }

            BuildRuntimeStatsBoard();

            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
            if (_rematchButton != null) _rematchButton.onClick.AddListener(OnRematch);
        }

        private void BuildRuntimeStatsBoard()
        {
            MatchResultEntry[] entries = MatchResultBoard.Entries;
            if (entries == null || entries.Length == 0)
                return;

            Transform parent = transform;
            GameObject panel = CreatePanel(
                parent,
                "RuntimeMatchStatsBoard",
                new Color(0.025f, 0.035f, 0.075f, 0.92f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            float panelHeight = Mathf.Clamp(112f + entries.Length * 46f, 390f, 610f);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -118f);
            panelRect.sizeDelta = new Vector2(1080f, panelHeight);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateBoardTitle(panel.transform);
            CreateStatsHeader(panel.transform);

            for (int i = 0; i < entries.Length; i++)
                CreateStatsRow(panel.transform, entries[i]);

            float buttonY = panelHeight > 430f ? -470f : -430f;
            MoveButton(_continueButton, new Vector2(-170f, buttonY));
            MoveButton(_rematchButton, new Vector2(170f, buttonY));
        }

        private static void CreateBoardTitle(Transform parent)
        {
            MatchResultEntry star = MatchResultBoard.StarPlayer;
            string starName = !string.IsNullOrWhiteSpace(star.DisplayName)
                ? star.DisplayName
                : MatchResultBoard.MvpName;

            Text title = CreateText(
                parent,
                "StatsBoardTitle",
                string.IsNullOrWhiteSpace(starName)
                    ? "MATCH STATS"
                    : "STAR PLAYER: " + starName.ToUpperInvariant(),
                30,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.22f, 1f),
                FontStyle.Bold);

            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 42f;
        }

        private static void CreateStatsHeader(Transform parent)
        {
            Text header = CreateText(
                parent,
                "StatsHeader",
                "PLAYER                         TEAM     K / D / A     DAMAGE     TAKEN      GEMS     RATING",
                18,
                TextAnchor.MiddleLeft,
                new Color(0.72f, 0.82f, 1f, 1f),
                FontStyle.Bold);

            LayoutElement headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 28f;
        }

        private static void CreateStatsRow(Transform parent, MatchResultEntry entry)
        {
            GameObject row = CreatePanel(parent, "StatsRow_" + SanitizeName(entry.DisplayName), ResolveRowColor(entry));
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 38f;

            HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowGroup.padding = new RectOffset(12, 12, 4, 4);
            rowGroup.spacing = 10f;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandHeight = true;

            CreateCell(row.transform, entry.IsStarPlayer ? "* " + entry.DisplayName : entry.DisplayName, 280f, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            CreateCell(row.transform, entry.Team.ToString().ToUpperInvariant(), 86f, TextAnchor.MiddleCenter, ResolveTeamColor(entry.Team), FontStyle.Bold);
            CreateCell(row.transform, $"{entry.Stats.Kills} / {entry.Stats.Deaths} / {entry.Stats.Assists}", 120f, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            CreateCell(row.transform, Mathf.RoundToInt(entry.Stats.DamageDealt).ToString(), 120f, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, Mathf.RoundToInt(entry.Stats.DamageTaken).ToString(), 110f, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, entry.Stats.GemsCollected.ToString(), 72f, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, Mathf.RoundToInt(entry.StarScore).ToString(), 96f, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.22f, 1f), FontStyle.Bold);
        }

        private static void CreateCell(
            Transform parent,
            string text,
            float width,
            TextAnchor alignment,
            Color color,
            FontStyle style)
        {
            Text cell = CreateText(parent, "Cell", text, 17, alignment, color, style);
            LayoutElement layout = cell.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);

            Text label = go.GetComponent<Text>();
            label.text = text;
            label.font = ResolveFont();
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            outline.useGraphicAlpha = true;

            return label;
        }

        private static void MoveButton(Button button, Vector2 anchoredPosition)
        {
            if (button == null)
                return;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = anchoredPosition;
        }

        private static Color ResolveTeamColor(TeamType team)
        {
            if (team == TeamType.Blue)
                return new Color(0.45f, 0.68f, 1f, 1f);

            if (team == TeamType.Red)
                return new Color(1f, 0.42f, 0.46f, 1f);

            return Color.white;
        }

        private static Color ResolveRowColor(MatchResultEntry entry)
        {
            if (entry.IsStarPlayer)
                return new Color(1f, 0.74f, 0.12f, 0.30f);

            if (entry.Team == TeamType.Blue)
                return new Color(0.10f, 0.22f, 0.55f, 0.44f);

            if (entry.Team == TeamType.Red)
                return new Color(0.55f, 0.10f, 0.13f, 0.44f);

            return new Color(0.12f, 0.14f, 0.18f, 0.46f);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            return value.Replace(" ", string.Empty).Replace("/", string.Empty);
        }

        private static Font ResolveFont()
        {
            if (_runtimeFont != null)
                return _runtimeFont;

            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
                _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return _runtimeFont;
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
        private static readonly MatchResultEntry[] EmptyEntries = new MatchResultEntry[0];

        public static bool WinnerKnown;
        public static bool Draw;
        public static TeamType Winner;
        public static int BlueScore;
        public static int RedScore;

        // MVP snapshot, written by MatchEndRouter from MatchStatsTracker.
        // Empty / 0 if no stats tracker was in the scene.
        public static string MvpName;
        public static MatchStats MvpStats;
        public static MatchResultEntry[] Entries = EmptyEntries;
        public static MatchResultEntry StarPlayer;

        public static void Capture(TeamType winner, int blue, int red)
        {
            WinnerKnown = winner != TeamType.Neutral;
            Draw = winner == TeamType.Neutral;
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

        public static void CaptureEntries(MatchResultEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                Entries = EmptyEntries;
                StarPlayer = default;
                MvpName = string.Empty;
                MvpStats = default;
                return;
            }

            Entries = entries;
            StarPlayer = default;

            for (int i = 0; i < Entries.Length; i++)
            {
                if (!Entries[i].IsStarPlayer)
                    continue;

                StarPlayer = Entries[i];
                if (string.IsNullOrWhiteSpace(MvpName))
                {
                    MvpName = StarPlayer.DisplayName;
                    MvpStats = StarPlayer.Stats;
                }

                return;
            }
        }

        public static void Reset()
        {
            WinnerKnown = false;
            Draw = false;
            Winner = TeamType.Blue;
            BlueScore = 0;
            RedScore = 0;
            MvpName = string.Empty;
            MvpStats = default;
            Entries = EmptyEntries;
            StarPlayer = default;
        }
    }

    public struct MatchResultEntry
    {
        public string DisplayName;
        public TeamType Team;
        public MatchStats Stats;
        public float StarScore;
        public bool IsStarPlayer;
    }
}
