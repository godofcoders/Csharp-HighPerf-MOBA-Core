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
        private readonly List<ResultModelView> _resultModels = new List<ResultModelView>(8);
        private RenderTexture _modelRenderTexture;
        private GameObject _modelStageRoot;

        private void Start()
        {
            bool hasRuntimeEntries = MatchResultBoard.Entries != null &&
                                     MatchResultBoard.Entries.Length > 0;
            string winnerStr = MatchResultBoard.WinnerKnown
                ? $"{MatchResultBoard.Winner} wins!"
                : (MatchResultBoard.Draw ? "Draw" : "Match Over");
            string scoreStr = $"Blue {MatchResultBoard.BlueScore} — Red {MatchResultBoard.RedScore}";

            if (_winnerTextTmp != null) _winnerTextTmp.text = winnerStr;
            else if (_winnerTextLegacy != null) _winnerTextLegacy.text = winnerStr;

            if (_scoreTextTmp != null) _scoreTextTmp.text = scoreStr;
            else if (_scoreTextLegacy != null) _scoreTextLegacy.text = scoreStr;

            if (hasRuntimeEntries)
            {
                SetLegacyScoreText(string.Empty);
                MoveResultText(_winnerTextTmp, _winnerTextLegacy, new Vector2(0f, 435f), new Vector2(980f, 86f), 54);
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

            CreateTeamPanel(
                parent,
                TeamType.Blue,
                CollectTeamEntries(entries, TeamType.Blue),
                new Vector2(-445f, -142f),
                MatchResultBoard.WinnerKnown && MatchResultBoard.Winner == TeamType.Blue);

            CreateTeamPanel(
                parent,
                TeamType.Red,
                CollectTeamEntries(entries, TeamType.Red),
                new Vector2(445f, -142f),
                MatchResultBoard.WinnerKnown && MatchResultBoard.Winner == TeamType.Red);

            MoveButton(_continueButton, new Vector2(-170f, -482f));
            MoveButton(_rematchButton, new Vector2(170f, -482f));
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

        private static void CreateTeamPanel(
            Transform parent,
            TeamType team,
            List<MatchResultEntry> entries,
            Vector2 anchoredPosition,
            bool isWinner)
        {
            GameObject panel = CreatePanel(
                parent,
                team + "ResultPanel",
                team == TeamType.Blue
                    ? new Color(0.035f, 0.090f, 0.230f, 0.94f)
                    : new Color(0.235f, 0.035f, 0.055f, 0.94f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = new Vector2(800f, 392f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateTeamPanelHeader(panel.transform, team, isWinner);
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
            bool isWinner)
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
                team == TeamType.Blue ? "BLUE TEAM" : "RED TEAM",
                28,
                TextAnchor.MiddleLeft,
                ResolveTeamColor(team),
                FontStyle.Bold);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;

            Text score = CreateText(
                header.transform,
                "TeamScore",
                ResolveTeamScoreLabel(team),
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
            CreateCell(header.transform, "PLAYER", 220f, 14, TextAnchor.MiddleLeft, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "K/D/A", 92f, 14, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "DAMAGE", 104f, 14, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "TAKEN", 96f, 14, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "GEMS", 58f, 14, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
            CreateCell(header.transform, "RATING", 82f, 14, TextAnchor.MiddleCenter, headerColor, FontStyle.Bold);
        }

        private static void CreateTeamStatsRow(Transform parent, MatchResultEntry entry)
        {
            GameObject row = CreatePanel(parent, "StatsRow_" + SanitizeName(entry.DisplayName), ResolveRowColor(entry));
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 82f;

            HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
            rowGroup.padding = new RectOffset(12, 12, 7, 7);
            rowGroup.spacing = 8f;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandHeight = true;

            string displayName = entry.IsStarPlayer
                ? "* " + entry.DisplayName.ToUpperInvariant()
                : entry.DisplayName.ToUpperInvariant();
            CreateCell(row.transform, displayName, 220f, 18, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            CreateCell(row.transform, $"{entry.Stats.Kills} / {entry.Stats.Deaths} / {entry.Stats.Assists}", 92f, 17, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            CreateCell(row.transform, Mathf.RoundToInt(entry.Stats.DamageDealt).ToString(), 104f, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, Mathf.RoundToInt(entry.Stats.DamageTaken).ToString(), 96f, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, entry.Stats.GemsCollected.ToString(), 58f, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            CreateCell(row.transform, Mathf.RoundToInt(entry.StarScore).ToString(), 82f, 17, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.22f, 1f), FontStyle.Bold);
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
            frameRect.anchoredPosition = new Vector2(0f, 190f);
            frameRect.sizeDelta = new Vector2(1210f, 245f);

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
            GameObject prefab = entry.Definition != null ? entry.Definition.ModelPrefab : null;
            GameObject model = prefab != null
                ? Instantiate(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            model.name = "ResultModel_" + SanitizeName(entry.DisplayName);
            StripResultModelBehaviours(model);

            if (prefab == null)
                TintFallbackModel(model, team);

            return model;
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
                rect.anchoredPosition = anchoredPosition;
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
        public BrawlerDefinition Definition;
        public MatchStats Stats;
        public float StarScore;
        public bool IsStarPlayer;
    }

    internal struct ResultModelView
    {
        public Transform Root;
        public Vector3 BasePosition;
        public bool Celebrating;
        public float Phase;
    }
}
