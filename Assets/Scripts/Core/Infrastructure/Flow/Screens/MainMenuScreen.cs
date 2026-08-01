using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Main menu landing screen. Play starts the match with the current
    /// brawler/map selection, while Map opens the combined mode/map picker.
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
        private const string RuntimeTopStatusName = "RuntimeHomeTopStatus";
        private const string LoadingPresentationName = "RuntimeLoadingPresentation";

        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _mapSelectButton;
        [SerializeField] private Button _questsButton;


        [Header("Defaults (used if nothing was picked yet)")]
        [SerializeField] private BrawlerDefinition _defaultBrawler;
        [Tooltip("Optional roster used to resolve the last selected brawler after a fresh app launch.")]
        [SerializeField] private BrawlerDefinition[] _availableBrawlers;
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
            EnsureSelectedBrawlerLoaded(false);
            EnsureHomePresentation();
            SyncHomeBrawlerPreview();

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
            EnsureSelectedBrawlerLoaded(false);
            EnsureSelectedMapLoaded();
            SceneFlow.Instance?.LoadScene(SceneId.Match);
        }

        private void OnMapSelect()
        {
            EnsureSelectedBrawlerLoaded(false);
            EnsureSelectedMapLoaded();
            SceneSelection.MapSelectReturnScene = SceneId.MainMenu;

            SceneFlow.Instance?.LoadScene(SceneId.MapSelect);
        }

        private void OnQuests()
        {
            EnsureQuestSection();
            _questsView?.Show();
        }

        private void EnsureSelectedBrawlerLoaded(bool allowDefaultFallback)
        {
            if (PlayerBrawlerProgress.TryGetSelectedBrawler(_availableBrawlers, out BrawlerDefinition saved))
            {
                AssignSelectedBrawler(saved);
                return;
            }

            if (SceneSelection.SelectedBrawler != null)
            {
                AssignSelectedBrawler(SceneSelection.SelectedBrawler);
                return;
            }

            if (allowDefaultFallback || !PlayerBrawlerProgress.HasSavedSelectedBrawler())
                AssignSelectedBrawler(_defaultBrawler);
        }

        private void EnsureSelectedMapLoaded()
        {
            if (SceneSelection.SelectedMap == null)
                SceneSelection.SelectedMap = _defaultMap;

            if (SceneSelection.SelectedMap == null)
            {
                SceneSelection.SelectedMode = _defaultMode;
                return;
            }

            if (SceneSelection.SelectedMap.SupportsMode(SceneSelection.SelectedMode))
                return;

            SceneSelection.SelectedMode = ResolvePlayableMode(SceneSelection.SelectedMap);
        }

        private GameModeId ResolvePlayableMode(MapDefinition map)
        {
            if (map != null && map.SupportsMode(_defaultMode))
                return _defaultMode;

            if (map != null && map.SupportedModes != null && map.SupportedModes.Length > 0)
                return map.SupportedModes[0];

            return _defaultMode;
        }

        private static void AssignSelectedBrawler(BrawlerDefinition brawler)
        {
            SceneSelection.SelectedBrawler = brawler;

            if (brawler != null)
                SceneSelection.SelectedBuildPowerLevel = PlayerBrawlerProgress.GetLevel(brawler);
        }

        private void SyncHomeBrawlerPreview()
        {
            MainMenuBrawlerPreview[] previews = GetComponentsInChildren<MainMenuBrawlerPreview>(true);
            if (previews == null || previews.Length == 0)
                return;

            BrawlerDefinition selected = SceneSelection.SelectedBrawler != null
                ? SceneSelection.SelectedBrawler
                : PlayerBrawlerProgress.ResolveSelectedBrawler(_availableBrawlers, _defaultBrawler);

            for (int i = 0; i < previews.Length; i++)
            {
                if (previews[i] == null)
                    continue;

                previews[i].ConfigureSelection(selected, _availableBrawlers, _defaultBrawler);
            }
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
            EnsureTopStatus();
            RemoveSideRailBackdrop();
            EnsureEventDock();
            EnsureActionRail();

            StyleMenuButton(
                _mapSelectButton,
                "MAP",
                MenuUITheme.SecondaryButton,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(64f, 76f),
                new Vector2(150f, 74f));

            StyleMenuButton(
                _questsButton,
                "QUESTS",
                MenuUITheme.QuestButton,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(64f, -12f),
                new Vector2(150f, 74f));

            StyleMenuButton(
                _playButton,
                "PLAY",
                MenuUITheme.PrimaryButton,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-54f, 28f),
                new Vector2(340f, 96f));
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
                Sprite lobbyBackground = BrawlerGeneratedArtLibrary.LoadHomeLobbyBackground();
                image.sprite = lobbyBackground != null
                    ? lobbyBackground
                    : RuntimeUISpriteUtility.GetSolidWhiteSprite();
                image.color = lobbyBackground != null
                    ? Color.white
                    : new Color(0.018f, 0.034f, 0.075f, 1f);
                image.preserveAspect = false;
                image.raycastTarget = false;
            }

            RectTransform rect = background.GetComponent<RectTransform>();
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.SetAsFirstSibling();

            if (BrawlerGeneratedArtLibrary.LoadHomeLobbyBackground() != null)
                RemoveArenaBackdrop();
            else
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
                vignetteImage.color = new Color(0.005f, 0.011f, 0.030f, 0.20f);
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
                DestroyRuntimeObject(existing.gameObject);

            GameObject arena = CreatePanel(transform, RuntimeArenaBackdropName, Color.clear);
            SetPassive(arena);
            RectTransform arenaRect = arena.GetComponent<RectTransform>();
            Anchor(arenaRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateArenaLayer(arena.transform, "DeepSky", new Vector2(0f, 0.49f), Vector2.one, new Color(0.018f, 0.060f, 0.145f, 1f));
            CreateArenaLayer(arena.transform, "LowerSky", new Vector2(0f, 0.43f), new Vector2(1f, 0.69f), new Color(0.020f, 0.135f, 0.190f, 0.96f));
            CreateArenaLayer(arena.transform, "SeaLine", new Vector2(0f, 0.40f), new Vector2(1f, 0.50f), new Color(0.020f, 0.225f, 0.235f, 0.90f));
            CreateArenaLayer(arena.transform, "DeckFloor", Vector2.zero, new Vector2(1f, 0.41f), new Color(0.045f, 0.070f, 0.095f, 1f));
            CreateArenaLayer(arena.transform, "DeckLip", new Vector2(0f, 0.405f), new Vector2(1f, 0.435f), new Color(0.155f, 0.135f, 0.110f, 0.98f));

            for (int i = 0; i < 7; i++)
            {
                float y = Mathf.Lerp(0.035f, 0.365f, i / 6f);
                CreateArenaLine(arena.transform, $"DeckPlank{i}", new Vector2(0f, y), new Vector2(1f, y + 0.003f), new Color(0.140f, 0.175f, 0.210f, 0.28f));
            }

            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(0.07f, 0.93f, i / 5f);
                CreateArenaLine(arena.transform, $"DockPost{i}", new Vector2(x, 0.39f), new Vector2(x + 0.010f, 0.58f), new Color(0.095f, 0.080f, 0.070f, 0.46f));
            }

            CreateArenaLight(arena.transform, new Vector2(0.34f, 0.21f), new Vector2(0.66f, 0.79f), new Color(0.300f, 0.910f, 0.850f, 0.20f));
            CreateArenaLight(arena.transform, new Vector2(0.405f, 0.315f), new Vector2(0.595f, 0.655f), new Color(1f, 0.920f, 0.560f, 0.13f));
            CreateArenaLight(arena.transform, new Vector2(0.08f, 0.55f), new Vector2(0.25f, 0.86f), new Color(0.100f, 0.700f, 0.980f, 0.09f));
            CreateArenaLayer(arena.transform, "LeftRope", new Vector2(0.04f, 0.43f), new Vector2(0.055f, 0.88f), new Color(0.120f, 0.165f, 0.190f, 0.60f));
            CreateArenaLayer(arena.transform, "LeftRope2", new Vector2(0.105f, 0.43f), new Vector2(0.118f, 0.91f), new Color(0.120f, 0.165f, 0.190f, 0.50f));
            CreateArenaLayer(arena.transform, "RightShipShape", new Vector2(0.78f, 0.44f), new Vector2(0.98f, 0.66f), new Color(0.060f, 0.065f, 0.090f, 0.30f));
            CreateArenaLight(arena.transform, new Vector2(0.82f, 0.61f), new Vector2(0.98f, 0.84f), new Color(0.090f, 0.280f, 0.420f, 0.12f));

            arena.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
        }

        private void RemoveArenaBackdrop()
        {
            Transform existing = transform.Find(RuntimeArenaBackdropName);
            if (existing != null)
                DestroyRuntimeObject(existing.gameObject);
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
                DestroyRuntimeObject(existing.gameObject);

            GameObject header = CreatePanel(transform, RuntimeHeaderName, MenuUITheme.Header);
            RectTransform rect = header.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.035f, 0.895f), new Vector2(0.250f, 0.980f), Vector2.zero, Vector2.zero);

            Image portrait = CreatePanel(header.transform, "Portrait", new Color(0.100f, 0.085f, 0.155f, 1f)).GetComponent<Image>();
            Anchor(portrait.rectTransform, new Vector2(0.03f, 0.16f), new Vector2(0.19f, 0.86f), Vector2.zero, Vector2.zero);

            TMP_Text portraitLabel = CreateText(header.transform, "PortraitLabel", "M", 25f, TextAlignmentOptions.Center, MenuUITheme.Gold);
            portraitLabel.fontStyle = FontStyles.Bold;
            Anchor(portraitLabel.rectTransform, new Vector2(0.03f, 0.16f), new Vector2(0.19f, 0.86f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(header.transform, "Title", "MOBA CORE", 25f, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.23f, 0.46f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);

            TMP_Text subtitle = CreateText(
                header.transform,
                "Subtitle",
                "Storm Arena lobby",
                13f,
                TextAlignmentOptions.Left,
                MenuUITheme.TextMuted);
            Anchor(subtitle.rectTransform, new Vector2(0.23f, 0.14f), new Vector2(0.96f, 0.44f), Vector2.zero, Vector2.zero);

            header.transform.SetAsLastSibling();
        }

        private void EnsureTopStatus()
        {
            Transform existing = transform.Find(RuntimeTopStatusName);
            if (existing != null)
                DestroyRuntimeObject(existing.gameObject);

            GameObject strip = CreatePanel(transform, RuntimeTopStatusName, Color.clear);
            RectTransform stripRect = strip.GetComponent<RectTransform>();
            Anchor(stripRect, new Vector2(0.275f, 0.905f), new Vector2(0.735f, 0.980f), Vector2.zero, Vector2.zero);

            BrawlerDefinition selected = SceneSelection.SelectedBrawler != null
                ? SceneSelection.SelectedBrawler
                : PlayerBrawlerProgress.ResolveSelectedBrawler(_availableBrawlers, _defaultBrawler);
            string brawlerName = selected != null && !string.IsNullOrWhiteSpace(selected.BrawlerName)
                ? selected.BrawlerName
                : selected != null ? selected.name : "Brawler";
            int powerLevel = selected != null ? PlayerBrawlerProgress.GetLevel(selected) : 1;

            CreateStatusChip(strip.transform, "BrawlerChip", brawlerName.ToUpperInvariant(), "SELECTED", new Vector2(0.00f, 0.05f), new Vector2(0.36f, 0.95f), MenuUITheme.Cyan);
            CreateStatusChip(strip.transform, "PowerChip", "POWER " + powerLevel, "LOADOUT", new Vector2(0.38f, 0.05f), new Vector2(0.60f, 0.95f), MenuUITheme.Gold);
            CreateStatusChip(strip.transform, "ModeChip", FormatMode(SceneSelection.SelectedMode).ToUpperInvariant(), "EVENT", new Vector2(0.62f, 0.05f), new Vector2(1.00f, 0.95f), MenuUITheme.QuestButton);

            strip.transform.SetAsLastSibling();
        }

        private static void CreateStatusChip(
            Transform parent,
            string name,
            string value,
            string caption,
            Vector2 min,
            Vector2 max,
            Color accentColor)
        {
            GameObject chip = CreatePanel(parent, name, new Color(0.012f, 0.022f, 0.055f, 0.88f));
            Anchor(chip.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);

            GameObject accent = CreatePanel(chip.transform, "Accent", accentColor);
            Anchor(accent.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.05f, 1f), Vector2.zero, Vector2.zero);

            TMP_Text captionText = CreateText(chip.transform, "Caption", caption, 10f, TextAlignmentOptions.Left, MenuUITheme.TextMuted);
            captionText.fontStyle = FontStyles.Bold;
            Anchor(captionText.rectTransform, new Vector2(0.12f, 0.56f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero);

            TMP_Text valueText = CreateText(chip.transform, "Value", value, 16f, TextAlignmentOptions.Left, Color.white);
            valueText.fontStyle = FontStyles.Bold;
            Anchor(valueText.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.94f, 0.58f), Vector2.zero, Vector2.zero);
            EnsureShadow(valueText.gameObject);
        }

        private void RemoveSideRailBackdrop()
        {
            Transform existing = transform.Find(RuntimeSideRailName);
            if (existing != null)
                DestroyRuntimeObject(existing.gameObject);
        }

        private void EnsureEventDock()
        {
            Transform existing = transform.Find(RuntimeEventDockName);
            if (existing != null)
                DestroyRuntimeObject(existing.gameObject);

            GameObject dock = CreatePanel(transform, RuntimeEventDockName, new Color(0.025f, 0.026f, 0.044f, 0.96f));
            existing = dock.transform;
            RectTransform rect = dock.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.335f, 0.028f), new Vector2(0.705f, 0.130f), Vector2.zero, Vector2.zero);

            GameObject eventIcon = CreatePanel(dock.transform, "EventIcon", new Color(0.950f, 0.150f, 0.120f, 1f));
            Anchor(eventIcon.GetComponent<RectTransform>(), new Vector2(0.035f, 0.18f), new Vector2(0.135f, 0.82f), Vector2.zero, Vector2.zero);

            TMP_Text eventNumber = CreateText(eventIcon.transform, "EventNumber", FormatModeBadge(SceneSelection.SelectedMode), 18f, TextAlignmentOptions.Center, Color.white);
            eventNumber.fontStyle = FontStyles.Bold;
            Anchor(eventNumber.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            TMP_Text eyebrow = CreateText(dock.transform, "Eyebrow", "SELECTED EVENT", 11f, TextAlignmentOptions.Left, MenuUITheme.Cyan);
            eyebrow.fontStyle = FontStyles.Bold;
            Anchor(eyebrow.rectTransform, new Vector2(0.165f, 0.58f), new Vector2(0.52f, 0.86f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(dock.transform, "Title", string.Empty, 21f, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.165f, 0.16f), new Vector2(0.72f, 0.58f), Vector2.zero, Vector2.zero);

            TMP_Text mode = CreateText(dock.transform, "Mode", string.Empty, 16f, TextAlignmentOptions.Right, MenuUITheme.Gold);
            mode.fontStyle = FontStyles.Bold;
            Anchor(mode.rectTransform, new Vector2(0.70f, 0.20f), new Vector2(0.94f, 0.76f), Vector2.zero, Vector2.zero);

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
            if (existing != null)
                DestroyRuntimeObject(existing.gameObject);

            GameObject rail = CreatePanel(transform, RuntimeActionRailName, MenuUITheme.ActionRail);
            RectTransform rect = rail.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0f, 0f), new Vector2(1f, 0.155f), Vector2.zero, Vector2.zero);
            existing = rail.transform;

            Image image = existing.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.008f, 0.012f, 0.028f, 0.92f);
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
            EnsureShadow(button.gameObject);

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
                tmp.fontSize = label == "PLAY" ? 30f : 22f;
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

        private static string FormatModeBadge(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.GemGrab:
                    return "G";
                case GameModeId.Knockout:
                    return "KO";
                case GameModeId.BrawlBall:
                    return "BB";
                case GameModeId.SoloShowdown:
                    return "SD";
                default:
                    return "!";
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

            target.SetActive(false);

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
