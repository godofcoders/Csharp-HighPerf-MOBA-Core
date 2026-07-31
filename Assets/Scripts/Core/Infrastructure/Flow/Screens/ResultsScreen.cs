using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;
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
        private const string RuntimeResultsBackgroundName = "RuntimeResultsBackground";

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

        private readonly List<ResultModelView> _resultModels = new List<ResultModelView>(8);
        private readonly List<RenderTexture> _miniModelRenderTextures = new List<RenderTexture>(8);
        private RenderTexture _modelRenderTexture;
        private GameObject _modelStageRoot;
        private GameObject _miniModelStageRoot;
        private int _miniModelSlotIndex;

        private void Start()
        {
            bool hasRuntimeEntries = MatchResultBoard.Entries != null &&
                                     MatchResultBoard.Entries.Length > 0;
            string winnerStr = ResolveWinnerText();
            string scoreStr = ResolveScoreText();

            EnsureResultsPresentation();

            if (_winnerTextTmp != null) _winnerTextTmp.text = winnerStr;
            else if (_winnerTextLegacy != null) _winnerTextLegacy.text = winnerStr;

            if (_scoreTextTmp != null) _scoreTextTmp.text = scoreStr;
            else if (_scoreTextLegacy != null) _scoreTextLegacy.text = scoreStr;

            if (hasRuntimeEntries)
            {
                SetLegacyScoreText(string.Empty);
                MoveResultText(_winnerTextTmp, _winnerTextLegacy, new Vector2(0f, 422f), new Vector2(960f, 76f), 50);
            }

            // MVP block — show only if MatchResultBoard has a name set.
            bool hasMvp = !hasRuntimeEntries && !string.IsNullOrWhiteSpace(MatchResultBoard.MvpName);
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
            BuildRuntimeModelShowcase(parent, entries);

            if (SceneSelection.SelectedMode == GameModeId.SoloShowdown)
            {
                CreateTeamPanel(
                    parent,
                    MatchResultBoard.LocalPlayerTeam,
                    CollectSoloEntries(entries),
                    new Vector2(0f, -142f),
                    MatchResultBoard.LocalPlayerWon,
                    "YOUR STATS",
                    string.Empty);

                MoveButton(_continueButton, new Vector2(-165f, -458f));
                MoveButton(_rematchButton, new Vector2(165f, -458f));
                return;
            }

            CreateTeamPanel(
                parent,
                TeamType.Blue,
                CollectTeamEntries(entries, TeamType.Blue),
                new Vector2(-360f, -148f),
                MatchResultBoard.WinnerKnown && MatchResultBoard.Winner == TeamType.Blue);

            CreateTeamPanel(
                parent,
                TeamType.Red,
                CollectTeamEntries(entries, TeamType.Red),
                new Vector2(360f, -148f),
                MatchResultBoard.WinnerKnown && MatchResultBoard.Winner == TeamType.Red);

            MoveButton(_continueButton, new Vector2(-165f, -458f));
            MoveButton(_rematchButton, new Vector2(165f, -458f));
        }

        private void EnsureResultsPresentation()
        {
            EnsureResultsBackground();
            StyleResultButton(_continueButton, "CONTINUE", MenuUITheme.SecondaryButton);
            StyleResultButton(_rematchButton, "REMATCH", MenuUITheme.PrimaryButton);
        }

        private void EnsureResultsBackground()
        {
            Transform existing = transform.Find(RuntimeResultsBackgroundName);
            if (existing != null)
            {
                existing.SetAsFirstSibling();
                return;
            }

            GameObject background = CreatePanel(transform, RuntimeResultsBackgroundName, MenuUITheme.ScreenBackground);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            CreateResultsLayer(background.transform, "ResultsSky", new Vector2(0f, 0.50f), Vector2.one, new Color(0.020f, 0.060f, 0.120f, 1f));
            CreateResultsLayer(background.transform, "ResultsFloor", Vector2.zero, new Vector2(1f, 0.50f), new Color(0.012f, 0.024f, 0.058f, 1f));
            CreateResultsLayer(background.transform, "ResultsCenterBand", new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.77f), new Color(0.040f, 0.075f, 0.160f, 0.72f));
            CreateResultsGlow(background.transform, "BlueGlow", new Vector2(0.04f, 0.15f), new Vector2(0.38f, 0.82f), new Color(0.16f, 0.42f, 1f, 0.14f));
            CreateResultsGlow(background.transform, "GoldGlow", new Vector2(0.62f, 0.10f), new Vector2(0.96f, 0.72f), new Color(1f, 0.68f, 0.16f, 0.10f));

            background.transform.SetAsFirstSibling();
        }

        private static void CreateResultsLayer(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject layer = CreatePanel(parent, name, color);
            RectTransform rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateResultsGlow(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject glowObject = CreatePanel(parent, name, color);
            Image glow = glowObject.GetComponent<Image>();
            if (glow != null)
                glow.sprite = RuntimeUISpriteUtility.GetSoftCircleSprite();

            RectTransform rect = glowObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            for (int i = 0; i < _resultModels.Count; i++)
            {
                ResultModelView model = _resultModels[i];
                if (model.Root == null)
                    continue;

                float time = Time.time * (model.Celebrating ? 5.2f : 1.8f) + model.Phase;
                float jump = model.Celebrating
                    ? Mathf.Abs(Mathf.Sin(time)) * 0.22f
                    : Mathf.Sin(time) * 0.025f;
                float yaw = model.Celebrating
                    ? Mathf.Sin(time * 0.7f) * 10f
                    : Mathf.Sin(time * 0.5f) * 3f;

                model.Root.localPosition = model.BasePosition + Vector3.up * jump;
                model.Root.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
            }
        }

        private static List<MatchResultEntry> CollectTeamEntries(
            MatchResultEntry[] entries,
            TeamType team)
        {
            List<MatchResultEntry> result = new List<MatchResultEntry>(4);
            if (entries == null)
                return result;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Team == team)
                    result.Add(entries[i]);
            }

            result.Sort((a, b) => b.StarScore.CompareTo(a.StarScore));
            return result;
        }

        private static List<MatchResultEntry> CollectSoloEntries(MatchResultEntry[] entries)
        {
            List<MatchResultEntry> localEntries = new List<MatchResultEntry>(1);
            if (entries == null)
                return localEntries;

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].IsLocalPlayer)
                    localEntries.Add(entries[i]);
            }

            if (localEntries.Count > 0)
                return localEntries;

            for (int i = 0; i < entries.Length; i++)
                localEntries.Add(entries[i]);

            return localEntries;
        }

        private void CreateTeamPanel(
            Transform parent,
            TeamType team,
            List<MatchResultEntry> entries,
            Vector2 anchoredPosition,
            bool isWinner,
            string titleOverride = null,
            string scoreOverride = null)
        {
            GameObject panel = CreatePanel(
                parent,
                team + "ResultPanel",
                ResolvePanelColor(team));

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = SceneSelection.SelectedMode == GameModeId.SoloShowdown
                ? new Vector2(760f, 318f)
                : new Vector2(690f, 360f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateTeamPanelHeader(panel.transform, team, isWinner, titleOverride, scoreOverride);
            CreateTeamStatsHeader(panel.transform);

            if (entries == null || entries.Count == 0)
            {
                Text empty = CreateText(
                    panel.transform,
                    "NoTeamEntries",
                    "NO TEAM DATA",
                    18,
                    TextAnchor.MiddleCenter,
                    new Color(1f, 1f, 1f, 0.55f),
                    FontStyle.Bold);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
                return;
            }

            for (int i = 0; i < entries.Count; i++)
                CreateTeamStatsRow(panel.transform, entries[i]);
        }

        private static void CreateTeamPanelHeader(
            Transform parent,
            TeamType team,
            bool isWinner,
            string titleOverride = null,
            string scoreOverride = null)
        {
            GameObject header = CreatePanel(parent, "TeamHeader", new Color(0f, 0f, 0f, 0.26f));
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 58f;

            HorizontalLayoutGroup group = header.AddComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(12, 12, 6, 6);
            group.spacing = 10f;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandHeight = true;

            Text title = CreateText(
                header.transform,
                "TeamName",
                string.IsNullOrWhiteSpace(titleOverride)
                    ? (team == TeamType.Blue ? "BLUE TEAM" : "RED TEAM")
                    : titleOverride,
                28,
                TextAnchor.MiddleLeft,
                ResolveTeamColor(team),
                FontStyle.Bold);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;

            Text score = CreateText(
                header.transform,
                "TeamScore",
                scoreOverride ?? ResolveTeamScoreLabel(team),
                22,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);
            LayoutElement scoreLayout = score.gameObject.AddComponent<LayoutElement>();
            scoreLayout.preferredWidth = 110f;

            Text winner = CreateText(
                header.transform,
                "WinnerBadge",
                isWinner ? "WINNER" : string.Empty,
                20,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.20f, 1f),
                FontStyle.Bold);
            LayoutElement winnerLayout = winner.gameObject.AddComponent<LayoutElement>();
            winnerLayout.preferredWidth = 126f;
        }

        private static void CreateTeamStatsHeader(Transform parent)
        {
            GameObject header = CreatePanel(parent, "TeamStatsHeader", new Color(0f, 0f, 0f, 0.18f));
            LayoutElement layout = header.AddComponent<LayoutElement>();
            layout.preferredHeight = 26f;

            HorizontalLayoutGroup rowGroup = header.AddComponent<HorizontalLayoutGroup>();
            rowGroup.padding = new RectOffset(12, 12, 2, 2);
            rowGroup.spacing = 8f;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandHeight = true;

            Color headerColor = new Color(0.78f, 0.86f, 1f, 1f);
            CreateCell(header.transform, string.Empty, 58f, 13, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "PLAYER", 132f, 13, TextAnchor.MiddleLeft, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "K/D/A", 68f, 13, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "DAMAGE", 78f, 13, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "TAKEN", 72f, 13, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "GEMS", 46f, 13, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "RATING", 64f, 13, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
        }

        private void CreateTeamStatsRow(Transform parent, MatchResultEntry entry)
        {
            GameObject row = CreatePanel(parent, "StatsRow_" + SanitizeName(entry.DisplayName), ResolveRowColor(entry));
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 72f;

            HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowGroup.padding = new RectOffset(12, 12, 7, 7);
            rowGroup.spacing = 8f;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandHeight = true;

            string displayName = entry.IsStarPlayer
                ? "* " + entry.DisplayName.ToUpperInvariant()
                : entry.DisplayName.ToUpperInvariant();
            CreateMiniatureModelCell(row.transform, entry);
            CreateCell(row.transform, displayName, 132f, 16, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            CreateCell(row.transform, $"{entry.Stats.Kills} / {entry.Stats.Deaths} / {entry.Stats.Assists}", 68f, 15, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            CreateCell(row.transform, Mathf.RoundToInt(entry.Stats.DamageDealt).ToString(), 78f, 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, Mathf.RoundToInt(entry.Stats.DamageTaken).ToString(), 72f, 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, entry.Stats.GemsCollected.ToString(), 46f, 14, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, Mathf.RoundToInt(entry.StarScore).ToString(), 64f, 15, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.22f, 1f), FontStyle.Bold);
        }

        private void CreateMiniatureModelCell(Transform parent, MatchResultEntry entry)
        {
            RawImage modelImage = CreateRawImage(parent, "MiniModel_" + SanitizeName(entry.DisplayName));
            modelImage.texture = BuildMiniatureModelTexture(entry);
            modelImage.color = Color.white;

            LayoutElement layout = modelImage.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 58f;
            layout.flexibleWidth = 0f;
        }

        private Texture BuildMiniatureModelTexture(MatchResultEntry entry)
        {
            EnsureMiniatureModelStageRoot();

            GameObject slotRoot = new GameObject("MiniModelSlot_" + SanitizeName(entry.DisplayName));
            slotRoot.transform.SetParent(_miniModelStageRoot.transform, false);
            slotRoot.transform.localPosition = new Vector3(_miniModelSlotIndex * 4.0f, 0f, 0f);
            _miniModelSlotIndex++;

            RenderTexture texture = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32)
            {
                name = "ResultsMiniModelTexture_" + SanitizeName(entry.DisplayName),
                antiAliasing = 4
            };
            texture.Create();
            _miniModelRenderTextures.Add(texture);

            Camera camera = CreateMiniatureModelCamera(slotRoot.transform);
            camera.targetTexture = texture;
            CreateMiniatureModelLights(slotRoot.transform);
            CreateMiniatureModelFloor(slotRoot.transform);

            GameObject model = CreateResultModelObject(entry, entry.Team);
            if (model != null)
            {
                model.transform.SetParent(slotRoot.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                NormalizeResultModel(model, 0.92f);

                bool celebrating = entry.IsStarPlayer ||
                                   (MatchResultBoard.WinnerKnown && MatchResultBoard.Winner == entry.Team);
                _resultModels.Add(new ResultModelView
                {
                    Root = model.transform,
                    BasePosition = model.transform.localPosition,
                    Celebrating = celebrating,
                    Phase = _miniModelSlotIndex * 0.43f
                });
            }

            return texture;
        }

        private void EnsureMiniatureModelStageRoot()
        {
            if (_miniModelStageRoot != null)
                return;

            _miniModelStageRoot = new GameObject("ResultsMiniModelStage");
            _miniModelStageRoot.transform.position = new Vector3(0f, -220f, 0f);
        }

        private Camera CreateMiniatureModelCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("MiniModelCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.05f, -3.2f);
            cameraObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.010f, 0.018f, 0.040f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 0.96f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 14f;
            return camera;
        }

        private static void CreateMiniatureModelLights(Transform parent)
        {
            GameObject keyObject = new GameObject("MiniModelKeyLight");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.localRotation = Quaternion.Euler(42f, -22f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.94f, 0.84f, 1f);

            GameObject rimObject = new GameObject("MiniModelRimLight");
            rimObject.transform.SetParent(parent, false);
            rimObject.transform.localPosition = new Vector3(0.35f, 1.2f, -1.2f);
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.range = 4f;
            rim.intensity = 0.85f;
            rim.color = new Color(0.55f, 0.72f, 1f, 1f);
        }

        private static void CreateMiniatureModelFloor(Transform parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "MiniModelFloor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.08f, 0.52f);
            floor.transform.localScale = new Vector3(1.25f, 0.045f, 0.72f);

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.08f, 0.11f, 0.18f, 1f);

            Collider collider = floor.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private static void CreateCell(
            Transform parent,
            string text,
            float width,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style)
        {
            Text cell = CreateText(parent, "Cell", text, fontSize, alignment, color, style);
            LayoutElement layout = cell.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }

        private void BuildRuntimeModelShowcase(Transform parent, MatchResultEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return;

            GameObject frame = CreatePanel(parent, "RuntimeModelShowcase", new Color(0.015f, 0.025f, 0.055f, 0.88f));
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = new Vector2(0f, 188f);
            frameRect.sizeDelta = new Vector2(1120f, 220f);

            Text star = CreateText(
                frame.transform,
                "StarPlayerBadge",
                string.IsNullOrWhiteSpace(MatchResultBoard.MvpName)
                    ? string.Empty
                    : "STAR PLAYER: " + MatchResultBoard.MvpName.ToUpperInvariant(),
                24,
                TextAnchor.UpperCenter,
                new Color(1f, 0.82f, 0.22f, 1f),
                FontStyle.Bold);
            RectTransform starRect = star.GetComponent<RectTransform>();
            starRect.anchorMin = new Vector2(0f, 0.80f);
            starRect.anchorMax = new Vector2(1f, 0.98f);
            starRect.offsetMin = new Vector2(20f, 0f);
            starRect.offsetMax = new Vector2(-20f, 0f);

            RawImage rawImage = CreateRawImage(frame.transform, "ModelRender");
            RectTransform rawRect = rawImage.GetComponent<RectTransform>();
            rawRect.anchorMin = new Vector2(0.02f, 0.04f);
            rawRect.anchorMax = new Vector2(0.98f, 0.82f);
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            _modelRenderTexture = new RenderTexture(1400, 330, 16, RenderTextureFormat.ARGB32)
            {
                name = "ResultsModelRenderTexture",
                antiAliasing = 4
            };
            _modelRenderTexture.Create();
            rawImage.texture = _modelRenderTexture;

            _modelStageRoot = new GameObject("ResultsModelStage");
            _modelStageRoot.transform.position = new Vector3(0f, -150f, 0f);

            Camera camera = CreateModelCamera(_modelStageRoot.transform);
            camera.targetTexture = _modelRenderTexture;
            CreateModelLights(_modelStageRoot.transform);
            CreateModelStageFloor(_modelStageRoot.transform);
            SpawnResultModels(entries);
        }

        private Camera CreateModelCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("ResultsModelCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, -7.4f);
            cameraObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.075f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 2.25f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            return camera;
        }

        private static void CreateModelLights(Transform parent)
        {
            GameObject keyObject = new GameObject("ResultsKeyLight");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.localRotation = Quaternion.Euler(42f, -30f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.color = new Color(1f, 0.92f, 0.80f, 1f);

            GameObject fillObject = new GameObject("ResultsFillLight");
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.localPosition = new Vector3(0f, 2.2f, -3f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 12f;
            fill.intensity = 1.5f;
            fill.color = new Color(0.55f, 0.72f, 1f, 1f);
        }

        private static void CreateModelStageFloor(Transform parent)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ResultsStageFloor";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.08f, 0.55f);
            floor.transform.localScale = new Vector3(10.8f, 0.08f, 2.2f);

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.08f, 0.10f, 0.16f, 1f);

            Collider collider = floor.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private void SpawnResultModels(MatchResultEntry[] entries)
        {
            if (SceneSelection.SelectedMode == GameModeId.SoloShowdown)
            {
                SpawnTeamModels(CollectSoloEntries(entries), MatchResultBoard.LocalPlayerTeam, 0f);
                return;
            }

            List<MatchResultEntry> blue = CollectTeamEntries(entries, TeamType.Blue);
            List<MatchResultEntry> red = CollectTeamEntries(entries, TeamType.Red);

            SpawnTeamModels(blue, TeamType.Blue, -2.55f);
            SpawnTeamModels(red, TeamType.Red, 2.55f);
        }

        private void SpawnTeamModels(List<MatchResultEntry> entries, TeamType team, float teamCenterX)
        {
            if (entries == null || entries.Count == 0 || _modelStageRoot == null)
                return;

            bool winner = MatchResultBoard.WinnerKnown && MatchResultBoard.Winner == team;
            int count = Mathf.Min(entries.Count, 3);
            float spacing = count > 2 ? 1.72f : 1.90f;
            float firstX = teamCenterX - (count - 1) * spacing * 0.5f;
            float modelHeight = winner ? 1.16f : 1.06f;

            for (int i = 0; i < count; i++)
            {
                MatchResultEntry entry = entries[i];
                GameObject model = CreateResultModelObject(entry, team);
                if (model == null)
                    continue;

                model.transform.SetParent(_modelStageRoot.transform, false);
                model.transform.localPosition = new Vector3(firstX + i * spacing, 0f, winner ? 0.04f : 0.34f);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                NormalizeResultModel(model, modelHeight);

                _resultModels.Add(new ResultModelView
                {
                    Root = model.transform,
                    BasePosition = model.transform.localPosition,
                    Celebrating = winner,
                    Phase = i * 0.7f + (team == TeamType.Red ? 0.25f : 0f)
                });
            }
        }

        private static GameObject CreateResultModelObject(MatchResultEntry entry, TeamType team)
        {
            GameObject modelRoot = new GameObject("ResultModel_" + SanitizeName(entry.DisplayName));
            if (entry.Definition != null &&
                ProceduralBrawlerModelFactory.TryCreate(entry.Definition, modelRoot.transform, null, out GameObject proceduralModel) &&
                proceduralModel != null)
            {
                StripResultModelBehaviours(modelRoot);
                return modelRoot;
            }

            GameObject prefab = entry.Definition != null ? entry.Definition.ModelPrefab : null;
            GameObject model = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(modelRoot.transform, false);

            model.name = "ResultModel_" + SanitizeName(entry.DisplayName);
            StripResultModelBehaviours(modelRoot);

            if (prefab == null)
                TintFallbackModel(model, team);

            return modelRoot;
        }

        private static void StripResultModelBehaviours(GameObject model)
        {
            MonoBehaviour[] behaviours = model.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;

            Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        private static void TintFallbackModel(GameObject model, TeamType team)
        {
            Renderer renderer = model.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return;

            renderer.material.color = team == TeamType.Blue
                ? new Color(0.18f, 0.42f, 1f, 1f)
                : new Color(1f, 0.22f, 0.28f, 1f);
        }

        private static void NormalizeResultModel(GameObject model, float targetHeight)
        {
            if (model == null || !TryGetRendererBounds(model, out Bounds bounds))
                return;

            float height = Mathf.Max(0.1f, bounds.size.y);
            float scale = Mathf.Clamp(targetHeight / height, 0.2f, 4.0f);
            model.transform.localScale *= scale;

            if (!TryGetRendererBounds(model, out bounds))
                return;

            Vector3 localPosition = model.transform.localPosition;
            localPosition.x -= bounds.center.x - model.transform.position.x;
            localPosition.y -= bounds.min.y - model.transform.position.y;
            localPosition.z -= bounds.center.z - model.transform.position.z;
            model.transform.localPosition = localPosition;
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
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

        private static RawImage CreateRawImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            RawImage image = go.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
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
            {
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = new Vector2(270f, 74f);
            }
        }

        private static void StyleResultButton(Button button, string label, Color color)
        {
            if (button == null)
                return;

            MenuUITheme.StyleButton(button, label, color, 23f);
        }

        private void SetLegacyScoreText(string text)
        {
            if (_scoreTextTmp != null)
                _scoreTextTmp.text = text;
            else if (_scoreTextLegacy != null)
                _scoreTextLegacy.text = text;
        }

        private static void MoveResultText(
            TMP_Text tmp,
            Text legacy,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize)
        {
            RectTransform rect = tmp != null
                ? tmp.rectTransform
                : legacy != null ? legacy.rectTransform : null;

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
            }

            if (tmp != null)
                tmp.fontSize = fontSize;
            else if (legacy != null)
                legacy.fontSize = fontSize;
        }

        private static string ResolveTeamScoreLabel(TeamType team)
        {
            if (team == TeamType.Blue)
                return MatchResultBoard.BlueScore.ToString();

            if (team == TeamType.Red)
                return MatchResultBoard.RedScore.ToString();

            return "-";
        }

        private static string ResolveScoreText()
        {
            if (SceneSelection.SelectedMode == GameModeId.SoloShowdown)
                return "Solo Showdown";

            return $"Blue {MatchResultBoard.BlueScore} — Red {MatchResultBoard.RedScore}";
        }

        private static Color ResolveTeamColor(TeamType team)
        {
            if (team == TeamType.Blue)
                return new Color(0.45f, 0.68f, 1f, 1f);

            if (team == TeamType.Red)
                return new Color(1f, 0.42f, 0.46f, 1f);

            return Color.white;
        }

        private static Color ResolvePanelColor(TeamType team)
        {
            if (team == TeamType.Blue)
                return new Color(0.035f, 0.090f, 0.230f, 0.94f);

            if (team == TeamType.Red)
                return new Color(0.235f, 0.035f, 0.055f, 0.94f);

            return new Color(0.040f, 0.055f, 0.095f, 0.94f);
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
            return RuntimeUIFontUtility.GetDefaultFont();
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinue);
            if (_rematchButton != null) _rematchButton.onClick.RemoveListener(OnRematch);

            if (_modelRenderTexture != null)
            {
                _modelRenderTexture.Release();
                Destroy(_modelRenderTexture);
                _modelRenderTexture = null;
            }

            if (_modelStageRoot != null)
            {
                Destroy(_modelStageRoot);
                _modelStageRoot = null;
            }

            for (int i = 0; i < _miniModelRenderTextures.Count; i++)
            {
                RenderTexture texture = _miniModelRenderTextures[i];
                if (texture == null)
                    continue;

                texture.Release();
                Destroy(texture);
            }

            _miniModelRenderTextures.Clear();

            if (_miniModelStageRoot != null)
            {
                Destroy(_miniModelStageRoot);
                _miniModelStageRoot = null;
            }
        }

        private void OnContinue() => SceneFlow.Instance?.ReturnToMainMenu();
        private void OnRematch() => SceneFlow.Instance?.LoadScene(SceneId.Match);

        private static string ResolveWinnerText()
        {
            if (MatchResultBoard.LocalResultKnown)
                return MatchResultBoard.LocalPlayerWon ? "You win!" : "You lose!";

            if (MatchResultBoard.Draw)
                return "Draw";

            return MatchResultBoard.WinnerKnown
                ? $"{MatchResultBoard.Winner} wins!"
                : "Match Over";
        }
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
        public static bool LocalPlayerKnown;
        public static TeamType LocalPlayerTeam;
        public static bool LocalResultKnown;
        public static bool LocalPlayerWon;

        // MVP snapshot, written by MatchEndRouter from MatchStatsTracker.
        // Empty / 0 if no stats tracker was in the scene.
        public static string MvpName;
        public static MatchStats MvpStats;
        public static MatchResultEntry[] Entries = EmptyEntries;
        public static MatchResultEntry StarPlayer;

        public static void Capture(
            TeamType winner,
            int blue,
            int red,
            TeamType localPlayerTeam = TeamType.Neutral)
        {
            WinnerKnown = winner != TeamType.Neutral;
            Draw = winner == TeamType.Neutral;
            Winner = winner;
            BlueScore = blue;
            RedScore = red;
            LocalPlayerKnown = localPlayerTeam != TeamType.Neutral;
            LocalPlayerTeam = localPlayerTeam;
            LocalResultKnown = LocalPlayerKnown && WinnerKnown;
            LocalPlayerWon = LocalResultKnown && localPlayerTeam == winner;
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
            LocalPlayerKnown = false;
            LocalPlayerTeam = TeamType.Neutral;
            LocalResultKnown = false;
            LocalPlayerWon = false;
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
        public BrawlerDefinition Definition;
        public MatchStats Stats;
        public float StarScore;
        public bool IsStarPlayer;
        public bool IsLocalPlayer;
    }

    internal struct ResultModelView
    {
        public Transform Root;
        public Vector3 BasePosition;
        public bool Celebrating;
        public float Phase;
    }
}
