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
            Transform background = transform.Find("Background");
            if (background == null)
                background = CreatePanel(transform, "Background", Color.white).transform;

            Image image = background.GetComponent<Image>();
            if (image != null)
            {
                Sprite scenic = BrawlerGeneratedArtLibrary.LoadLoadingHomeBackground();
                image.sprite = scenic != null ? scenic : RuntimeUISpriteUtility.GetSolidWhiteSprite();
                image.color = scenic != null ? Color.white : MenuUITheme.ScreenBackground;
                image.preserveAspect = false;
                image.raycastTarget = false;
            }

            RectTransform rect = background.GetComponent<RectTransform>();
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.SetAsFirstSibling();

            Transform vignette = transform.Find(RuntimeVignetteName);
            if (vignette == null)
            {
                GameObject overlay = CreatePanel(transform, RuntimeVignetteName, new Color(0.005f, 0.011f, 0.030f, 0.30f));
                vignette = overlay.transform;
            }

            Image vignetteImage = vignette.GetComponent<Image>();
            if (vignetteImage != null)
            {
                vignetteImage.color = new Color(0.005f, 0.011f, 0.030f, 0.30f);
                vignetteImage.raycastTarget = false;
            }

            Anchor(vignette.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            vignette.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
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
