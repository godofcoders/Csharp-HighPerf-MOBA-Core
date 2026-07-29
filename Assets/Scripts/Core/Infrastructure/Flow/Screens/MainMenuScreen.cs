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
            EnsureActionRail();

            StyleMenuButton(
                _mapSelectButton,
                "MAP",
                MenuUITheme.SecondaryButton,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-250f, 34f),
                new Vector2(320f, 86f));

            StyleMenuButton(
                _questsButton,
                "QUESTS",
                MenuUITheme.QuestButton,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(96f, 34f),
                new Vector2(300f, 86f));

            StyleMenuButton(
                _playButton,
                "PLAY",
                MenuUITheme.PrimaryButton,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-34f, 34f),
                new Vector2(300f, 86f));
        }

        private void StyleBackground()
        {
            Transform background = transform.Find("Background");
            Image image = background != null ? background.GetComponent<Image>() : null;
            if (image != null)
                image.color = MenuUITheme.ScreenBackground;
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
            Anchor(rect, new Vector2(0.045f, 0.82f), new Vector2(0.43f, 0.95f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(header.transform, "Title", "MOBA CORE", 34f, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.045f, 0.42f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);

            TMP_Text subtitle = CreateText(
                header.transform,
                "Subtitle",
                "Brawlers, maps, quests",
                17f,
                TextAlignmentOptions.Left,
                MenuUITheme.TextMuted);
            Anchor(subtitle.rectTransform, new Vector2(0.048f, 0.14f), new Vector2(0.96f, 0.42f), Vector2.zero, Vector2.zero);

            header.transform.SetAsLastSibling();
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

            existing.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
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
