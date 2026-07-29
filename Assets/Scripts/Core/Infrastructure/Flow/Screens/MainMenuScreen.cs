using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Main menu landing screen. Play advances to brawler selection, while
    /// Map opens the combined mode/map picker.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        private const string RuntimeHeaderName = "RuntimeHomeHeader";
        private const string RuntimeActionRailName = "RuntimeHomeActionRail";
        private const string RuntimeButtonAccentName = MenuUITheme.ButtonAccentName;
        private const string RuntimeVignetteName = "RuntimeHomeVignette";
        private const string RuntimeSideRailName = "RuntimeHomeSideRail";
        private const string RuntimeEventDockName = "RuntimeHomeEventDock";
        private const string RuntimeArenaBackdropName = "RuntimeHomeArenaBackdrop";
        private const string LoadingPresentationName = "RuntimeLoadingPresentation";

        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _mapSelectButton;
        [SerializeField] private Button _questsButton;


        [Header("Defaults (used if nothing was picked yet)")]
        [SerializeField] private BrawlerDefinition _defaultBrawler;
        [SerializeField] private MapDefinition _defaultMap;

        [SerializeField] private GameModeId _defaultMode = GameModeId.GemGrab;
        private QuestsPanelView _questsView;

        private void OnEnable()
        {
            // Note: do NOT call SceneSelection.Reset() here — Reset
            // intentionally preserves SelectedBrawler so MainMenu keeps
            // showing the player's last pick. Mode-only reset isn't useful
            // when Play goes straight to Match with the current selection.

            EnsureQuestSection();
            EnsureHomePresentation();

            if (_playButton != null) _playButton.onClick.AddListener(OnPlay);
            if (_mapSelectButton != null) _mapSelectButton.onClick.AddListener(OnMapSelect);
            if (_questsButton != null) _questsButton.onClick.AddListener(OnQuests);
        }

        private void OnDisable()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlay);
            if (_mapSelectButton != null) _mapSelectButton.onClick.RemoveListener(OnMapSelect);
            if (_questsButton != null) _questsButton.onClick.RemoveListener(OnQuests);
        }

        private void OnPlay()
        {
            SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);
        }

        private void OnMapSelect()
        {
            if (SceneSelection.SelectedBrawler == null)
                SceneSelection.SelectedBrawler = _defaultBrawler;
            if (SceneSelection.SelectedMap == null)
                SceneSelection.SelectedMap = _defaultMap;
            SceneSelection.SelectedMode = SceneSelection.SelectedMap != null &&
                                          SceneSelection.SelectedMap.SupportsMode(SceneSelection.SelectedMode)
                ? SceneSelection.SelectedMode
                : _defaultMode;
            SceneSelection.MapSelectReturnScene = SceneId.MainMenu;

            SceneFlow.Instance?.LoadScene(SceneId.MapSelect);
        }

        private void OnQuests()
        {
            EnsureQuestSection();
            _questsView?.Show();
        }

        private void EnsureQuestSection()
        {
            if (_questsView == null)
            {
                _questsView = GetComponentInChildren<QuestsPanelView>(true);
                if (_questsView == null)
                    _questsView = gameObject.AddComponent<QuestsPanelView>();
            }

            if (_questsButton == null)
                _questsButton = CreateRuntimeQuestButton();
        }

        private void EnsureHomePresentation()
        {
            StyleBackground();
            EnsureHomeHeader();
            EnsureSideRail();
            EnsureEventDock();
            EnsureActionRail();

            StyleMenuButton(
                _mapSelectButton,
                "MAP",
                MenuUITheme.SecondaryButton,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(34f, 64f),
                new Vector2(230f, 70f));

            StyleMenuButton(
                _questsButton,
                "QUESTS",
                MenuUITheme.QuestButton,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(34f, -22f),
                new Vector2(230f, 70f));

            StyleMenuButton(
                _playButton,
                "PLAY",
                MenuUITheme.PrimaryButton,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-38f, 30f),
                new Vector2(360f, 104f));
        }

        private void StyleBackground()
        {
            RemoveLeakedLoadingPresentation();

            Transform background = transform.Find("Background");
            if (background == null)
                background = CreatePanel(transform, "Background", Color.white).transform;

            Image image = background.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
                image.color = new Color(0.065f, 0.100f, 0.128f, 1f);
                image.preserveAspect = false;
                image.raycastTarget = false;
            }

            RectTransform rect = background.GetComponent<RectTransform>();
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.SetAsFirstSibling();

            EnsureArenaBackdrop();

            Transform vignette = transform.Find(RuntimeVignetteName);
            if (vignette == null)
            {
                GameObject overlay = CreatePanel(transform, RuntimeVignetteName, new Color(0.005f, 0.011f, 0.030f, 0.14f));
                vignette = overlay.transform;
            }

            Image vignetteImage = vignette.GetComponent<Image>();
            if (vignetteImage != null)
            {
                vignetteImage.color = new Color(0.005f, 0.011f, 0.030f, 0.14f);
                vignetteImage.raycastTarget = false;
            }

            Anchor(vignette.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            vignette.SetSiblingIndex(Mathf.Min(2, transform.childCount - 1));
        }

        private void RemoveLeakedLoadingPresentation()
        {
            Transform localLeak = transform.Find(LoadingPresentationName);
            if (localLeak != null)
                DestroyRuntimeObject(localLeak.gameObject);

            GameObject globalLeak = GameObject.Find(LoadingPresentationName);
            if (globalLeak != null && globalLeak.transform.root != transform.root)
                DestroyRuntimeObject(globalLeak);
        }

        private void EnsureArenaBackdrop()
        {
            Transform existing = transform.Find(RuntimeArenaBackdropName);
            if (existing != null)
            {
                existing.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
                return;
            }

            GameObject arena = CreatePanel(transform, RuntimeArenaBackdropName, Color.clear);
            SetPassive(arena);
            RectTransform arenaRect = arena.GetComponent<RectTransform>();
            Anchor(arenaRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateArenaLayer(arena.transform, "Sky", new Vector2(0f, 0.48f), Vector2.one, new Color(0.070f, 0.160f, 0.220f, 1f));
            CreateArenaLayer(arena.transform, "Horizon", new Vector2(0f, 0.42f), new Vector2(1f, 0.64f), new Color(0.160f, 0.260f, 0.250f, 1f));
            CreateArenaLayer(arena.transform, "SandFloor", Vector2.zero, new Vector2(1f, 0.54f), new Color(0.565f, 0.390f, 0.220f, 1f));
            CreateArenaLayer(arena.transform, "ArenaBoard", new Vector2(0.23f, 0.14f), new Vector2(0.83f, 0.62f), new Color(0.400f, 0.560f, 0.370f, 0.96f));

            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float y = Mathf.Lerp(0.16f, 0.60f, t);
                CreateArenaLine(arena.transform, $"LaneH{i}", new Vector2(0.24f, y), new Vector2(0.82f, y + 0.004f), new Color(0.220f, 0.390f, 0.260f, 0.42f));
            }

            for (int i = 0; i < 8; i++)
            {
                float x = Mathf.Lerp(0.26f, 0.80f, i / 7f);
                CreateArenaLine(arena.transform, $"LaneV{i}", new Vector2(x, 0.15f), new Vector2(x + 0.003f, 0.61f), new Color(0.235f, 0.415f, 0.275f, 0.32f));
            }

            CreateArenaBlock(arena.transform, "LeftCover", new Vector2(0.19f, 0.35f), new Vector2(0.31f, 0.52f), new Color(0.750f, 0.500f, 0.285f, 1f));
            CreateArenaBlock(arena.transform, "RightCover", new Vector2(0.75f, 0.24f), new Vector2(0.88f, 0.42f), new Color(0.710f, 0.455f, 0.260f, 1f));
            CreateArenaBlock(arena.transform, "BackCover", new Vector2(0.47f, 0.55f), new Vector2(0.61f, 0.72f), new Color(0.650f, 0.410f, 0.245f, 1f));
            CreateArenaBlock(arena.transform, "FrontCover", new Vector2(0.42f, 0.04f), new Vector2(0.57f, 0.17f), new Color(0.700f, 0.445f, 0.250f, 1f));

            CreateArenaPatch(arena.transform, "WheatPatchLeft", new Vector2(0.08f, 0.12f), new Vector2(0.22f, 0.24f));
            CreateArenaPatch(arena.transform, "WheatPatchRight", new Vector2(0.83f, 0.54f), new Vector2(0.98f, 0.69f));
            CreateArenaPatch(arena.transform, "WheatPatchCenter", new Vector2(0.50f, 0.30f), new Vector2(0.63f, 0.42f));

            CreateArenaMine(arena.transform);
            CreateArenaLight(arena.transform, new Vector2(0.49f, 0.60f), new Vector2(0.72f, 0.84f), new Color(0.550f, 0.900f, 1f, 0.10f));
            CreateArenaLight(arena.transform, new Vector2(0.08f, 0.58f), new Vector2(0.30f, 0.82f), new Color(1f, 0.730f, 0.320f, 0.10f));

            arena.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
        }

        private static void CreateArenaLayer(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject layer = CreatePanel(parent, name, color);
            SetPassive(layer);
            Anchor(layer.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);
        }

        private static void CreateArenaLine(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            CreateArenaLayer(parent, name, min, max, color);
        }

        private static void CreateArenaBlock(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject shadow = CreatePanel(parent, name + "Shadow", new Color(0f, 0f, 0f, 0.28f));
            SetPassive(shadow);
            Anchor(shadow.GetComponent<RectTransform>(), min + new Vector2(0.012f, -0.016f), max + new Vector2(0.012f, -0.016f), Vector2.zero, Vector2.zero);

            GameObject block = CreatePanel(parent, name, color);
            SetPassive(block);
            RectTransform blockRect = block.GetComponent<RectTransform>();
            Anchor(blockRect, min, max, Vector2.zero, Vector2.zero);
            blockRect.localRotation = Quaternion.Euler(0f, 0f, -3.5f);

            GameObject top = CreatePanel(block.transform, "TopFace", new Color(
                Mathf.Min(color.r + 0.12f, 1f),
                Mathf.Min(color.g + 0.10f, 1f),
                Mathf.Min(color.b + 0.08f, 1f),
                0.96f));
            SetPassive(top);
            Anchor(top.GetComponent<RectTransform>(), new Vector2(0f, 0.64f), Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void CreateArenaPatch(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject patch = CreatePanel(parent, name, new Color(0.760f, 0.615f, 0.235f, 0.86f));
            SetPassive(patch);
            Anchor(patch.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);

            for (int i = 0; i < 8; i++)
            {
                float x = 0.08f + i * 0.11f;
                GameObject stalk = CreatePanel(patch.transform, "Stalk" + i, i % 2 == 0
                    ? new Color(0.950f, 0.750f, 0.250f, 0.78f)
                    : new Color(0.640f, 0.510f, 0.160f, 0.78f));
                SetPassive(stalk);
                RectTransform rect = stalk.GetComponent<RectTransform>();
                Anchor(rect, new Vector2(x, 0.08f), new Vector2(x + 0.045f, 0.94f), Vector2.zero, Vector2.zero);
                rect.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -10f : 10f);
            }
        }

        private static void CreateArenaMine(Transform parent)
        {
            Image glow = CreatePanel(parent, "GemMineGlow", new Color(0.800f, 0.160f, 1f, 0.18f)).GetComponent<Image>();
            SetPassive(glow.gameObject);
            glow.sprite = RuntimeUISpriteUtility.GetSoftCircleSprite();
            Anchor(glow.rectTransform, new Vector2(0.435f, 0.285f), new Vector2(0.585f, 0.465f), Vector2.zero, Vector2.zero);

            Image hole = CreatePanel(parent, "GemMineHole", new Color(0.030f, 0.020f, 0.055f, 0.92f)).GetComponent<Image>();
            SetPassive(hole.gameObject);
            hole.sprite = RuntimeUISpriteUtility.GetSoftCircleSprite();
            Anchor(hole.rectTransform, new Vector2(0.475f, 0.345f), new Vector2(0.545f, 0.425f), Vector2.zero, Vector2.zero);

            GameObject gem = CreatePanel(parent, "GemMineShard", new Color(1.000f, 0.220f, 0.920f, 0.96f));
            SetPassive(gem);
            RectTransform gemRect = gem.GetComponent<RectTransform>();
            Anchor(gemRect, new Vector2(0.505f, 0.405f), new Vector2(0.525f, 0.440f), Vector2.zero, Vector2.zero);
            gemRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void CreateArenaLight(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            Image light = CreatePanel(parent, "ArenaLight", color).GetComponent<Image>();
            SetPassive(light.gameObject);
            light.sprite = RuntimeUISpriteUtility.GetSoftCircleSprite();
            Anchor(light.rectTransform, min, max, Vector2.zero, Vector2.zero);
        }

        private void EnsureHomeHeader()
        {
            Transform existing = transform.Find(RuntimeHeaderName);
            if (existing != null)
            {
                existing.SetAsLastSibling();
                return;
            }

            GameObject header = CreatePanel(transform, RuntimeHeaderName, MenuUITheme.Header);
            RectTransform rect = header.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.035f, 0.84f), new Vector2(0.39f, 0.955f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(header.transform, "Title", "MOBA CORE", 36f, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.05f, 0.42f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);

            TMP_Text subtitle = CreateText(
                header.transform,
                "Subtitle",
                "Storm Arena lobby",
                17f,
                TextAlignmentOptions.Left,
                MenuUITheme.TextMuted);
            Anchor(subtitle.rectTransform, new Vector2(0.052f, 0.14f), new Vector2(0.96f, 0.42f), Vector2.zero, Vector2.zero);

            header.transform.SetAsLastSibling();
        }

        private void EnsureSideRail()
        {
            Transform existing = transform.Find(RuntimeSideRailName);
            if (existing == null)
            {
                GameObject rail = CreatePanel(transform, RuntimeSideRailName, new Color(0.012f, 0.026f, 0.068f, 0.76f));
                existing = rail.transform;
                Anchor(rail.GetComponent<RectTransform>(), new Vector2(0.018f, 0.31f), new Vector2(0.185f, 0.62f), Vector2.zero, Vector2.zero);
            }

            Image image = existing.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.012f, 0.026f, 0.068f, 0.76f);
                image.raycastTarget = false;
            }

            existing.SetSiblingIndex(Mathf.Min(2, transform.childCount - 1));
        }

        private void EnsureEventDock()
        {
            Transform existing = transform.Find(RuntimeEventDockName);
            if (existing == null)
            {
                GameObject dock = CreatePanel(transform, RuntimeEventDockName, new Color(0.010f, 0.020f, 0.048f, 0.88f));
                existing = dock.transform;
                RectTransform rect = dock.GetComponent<RectTransform>();
                Anchor(rect, new Vector2(0.34f, 0.03f), new Vector2(0.70f, 0.135f), Vector2.zero, Vector2.zero);

                TMP_Text eyebrow = CreateText(dock.transform, "Eyebrow", "SELECTED EVENT", 13f, TextAlignmentOptions.Left, MenuUITheme.Cyan);
                eyebrow.fontStyle = FontStyles.Bold;
                Anchor(eyebrow.rectTransform, new Vector2(0.055f, 0.54f), new Vector2(0.45f, 0.86f), Vector2.zero, Vector2.zero);

                TMP_Text title = CreateText(dock.transform, "Title", string.Empty, 25f, TextAlignmentOptions.Left, Color.white);
                title.fontStyle = FontStyles.Bold;
                Anchor(title.rectTransform, new Vector2(0.055f, 0.14f), new Vector2(0.74f, 0.58f), Vector2.zero, Vector2.zero);

                TMP_Text mode = CreateText(dock.transform, "Mode", string.Empty, 18f, TextAlignmentOptions.Right, MenuUITheme.Gold);
                mode.fontStyle = FontStyles.Bold;
                Anchor(mode.rectTransform, new Vector2(0.70f, 0.18f), new Vector2(0.94f, 0.76f), Vector2.zero, Vector2.zero);
            }

            TMP_Text titleText = existing.Find("Title")?.GetComponent<TMP_Text>();
            if (titleText != null)
                titleText.text = ResolveSelectedMapName().ToUpperInvariant();

            TMP_Text modeText = existing.Find("Mode")?.GetComponent<TMP_Text>();
            if (modeText != null)
                modeText.text = FormatMode(SceneSelection.SelectedMode).ToUpperInvariant();

            existing.SetAsLastSibling();
        }

        private void EnsureActionRail()
        {
            Transform existing = transform.Find(RuntimeActionRailName);
            if (existing == null)
            {
                GameObject rail = CreatePanel(transform, RuntimeActionRailName, MenuUITheme.ActionRail);
                RectTransform rect = rail.GetComponent<RectTransform>();
                Anchor(rect, new Vector2(0f, 0f), new Vector2(1f, 0.145f), Vector2.zero, Vector2.zero);
                existing = rail.transform;
            }

            Image image = existing.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.012f, 0.024f, 0.058f, 0.80f);
                image.raycastTarget = false;
            }

            existing.SetSiblingIndex(Mathf.Min(3, transform.childCount - 1));
        }

        private static void StyleMenuButton(
            Button button,
            string label,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (button == null)
                return;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
            }

            Image image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();

            if (image != null)
            {
                image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
                image.color = color;
                image.raycastTarget = true;
                button.targetGraphic = image;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.62f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Transform accent = button.transform.Find(RuntimeButtonAccentName);
            if (accent == null)
            {
                GameObject accentObject = CreatePanel(button.transform, RuntimeButtonAccentName, MenuUITheme.ButtonAccent);
                accent = accentObject.transform;
            }

            RectTransform accentRect = accent.GetComponent<RectTransform>();
            Anchor(accentRect, new Vector2(0f, 0.78f), Vector2.one, Vector2.zero, Vector2.zero);
            accent.SetAsFirstSibling();

            TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
            Text legacy = button.GetComponentInChildren<Text>(true);
            if (tmp != null)
            {
                tmp.text = label;
                tmp.fontSize = 24f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                Anchor(tmp.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 3f), new Vector2(-10f, -3f));
                EnsureShadow(tmp.gameObject);
            }
            else if (legacy != null)
            {
                legacy.text = label;
                legacy.fontSize = 26;
                legacy.fontStyle = FontStyle.Bold;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.color = Color.white;
                legacy.raycastTarget = false;
                legacy.resizeTextForBestFit = true;
                legacy.resizeTextMinSize = 16;
                legacy.resizeTextMaxSize = 30;

                RectTransform legacyRect = legacy.GetComponent<RectTransform>();
                Anchor(legacyRect, Vector2.zero, Vector2.one, new Vector2(10f, 3f), new Vector2(-10f, -3f));
                EnsureShadow(legacy.gameObject);
            }

            button.transform.SetAsLastSibling();
        }

        private string ResolveSelectedMapName()
        {
            MapDefinition selected = SceneSelection.SelectedMap != null
                ? SceneSelection.SelectedMap
                : _defaultMap;

            if (selected == null)
                return "Crystal Yard";

            return !string.IsNullOrWhiteSpace(selected.DisplayName)
                ? selected.DisplayName
                : selected.name;
        }

        private static string FormatMode(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.GemGrab:
                    return "Gem Grab";
                case GameModeId.Knockout:
                    return "Knockout";
                case GameModeId.BrawlBall:
                    return "Brawl Ball";
                case GameModeId.SoloShowdown:
                    return "Solo Showdown";
                default:
                    return mode.ToString();
            }
        }

        private Button CreateRuntimeQuestButton()
        {
            Transform existing = transform.Find("RuntimeQuestsButton");
            if (existing != null)
            {
                Button existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                    return existingButton;
            }

            GameObject buttonObject = new GameObject("RuntimeQuestsButton", typeof(RectTransform));
            buttonObject.transform.SetParent(transform, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.78f, 0.855f);
            rect.anchorMax = new Vector2(0.955f, 0.925f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = new Color(0.10f, 0.44f, 0.90f, 0.96f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);

            TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = "QUESTS";
            label.fontSize = 18f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            return button;
        }

        private static void SetPassive(GameObject target)
        {
            if (target == null)
                return;

            Image image = target.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;
        }

        private static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            return MenuUITheme.CreatePanel(name, parent, color);
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            float size,
            TextAlignmentOptions alignment,
            Color color)
        {
            return MenuUITheme.CreateText(parent, name, text, size, alignment, color);
        }

        private static void EnsureShadow(GameObject target)
        {
            MenuUITheme.EnsureShadow(target);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            MenuUITheme.Anchor(rect, min, max, offsetMin, offsetMax);
        }
    }
}
