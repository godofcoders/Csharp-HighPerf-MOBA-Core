using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Game-mode selection. The scene can wire mode buttons directly, and
    /// this script can synthesize newer mode buttons from an existing
    /// template so old menu scenes stay usable while modes are added.
    /// </summary>
    public class GameModeSelectScreen : MonoBehaviour
    {
        [Header("Mode buttons")]
        [SerializeField] private Button _gemGrabButton;
        [SerializeField] private Button _knockoutButton;
        [SerializeField] private Button _brawlBallButton;
        [SerializeField] private Button _soloShowdownButton;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private GameObject _showdownChoiceRoot;
        private Button _soloChoiceButton;
        private Button _duoChoiceButton;
        private Button _cancelChoiceButton;

        private void OnEnable()
        {
            EnsureBrawlBallButton();
            EnsureShowdownChoicePanel();
            SetShowdownChoiceVisible(false);

            if (_gemGrabButton != null) _gemGrabButton.onClick.AddListener(OnGemGrab);
            if (_knockoutButton != null) _knockoutButton.onClick.AddListener(OnKnockout);
            if (_brawlBallButton != null) _brawlBallButton.onClick.AddListener(OnBrawlBall);
            if (_soloShowdownButton != null) _soloShowdownButton.onClick.AddListener(OnShowdown);
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);

        }

        private void OnDisable()
        {
            if (_gemGrabButton != null) _gemGrabButton.onClick.RemoveListener(OnGemGrab);
            if (_knockoutButton != null) _knockoutButton.onClick.RemoveListener(OnKnockout);
            if (_brawlBallButton != null) _brawlBallButton.onClick.RemoveListener(OnBrawlBall);
            if (_soloShowdownButton != null) _soloShowdownButton.onClick.RemoveListener(OnShowdown);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
        }

        private void OnGemGrab()
        {
            SelectMode(GameModeId.GemGrab);
        }

        private void OnKnockout()
        {
            SelectMode(GameModeId.Knockout);
        }

        private void OnBrawlBall()
        {
            SelectMode(GameModeId.BrawlBall);
        }

        private void OnShowdown()
        {
            SetShowdownChoiceVisible(true);
        }

        private void OnSoloShowdown()
        {
            SelectShowdownVariant(ShowdownVariant.Solo);
        }

        private void OnDuoShowdown()
        {
            SelectShowdownVariant(ShowdownVariant.Duo);
        }

        private void SelectShowdownVariant(ShowdownVariant variant)
        {
            SceneSelection.SelectedShowdownVariant = variant;
            SelectMode(GameModeId.SoloShowdown);
        }

        private void SelectMode(GameModeId mode)
        {
            SceneSelection.SelectedMode = mode;
            SceneFlow.Instance?.LoadScene(SceneId.MapSelect);
        }

        private void OnBack()
        {
            if (_showdownChoiceRoot != null && _showdownChoiceRoot.activeSelf)
            {
                SetShowdownChoiceVisible(false);
                return;
            }

            SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);
        }

        private void EnsureBrawlBallButton()
        {
            if (_brawlBallButton != null)
                return;

            Button template = _knockoutButton != null ? _knockoutButton : _gemGrabButton;
            if (template == null)
                return;

            _brawlBallButton = Instantiate(template, template.transform.parent);
            _brawlBallButton.name = "BrawlBallButton";
            _brawlBallButton.onClick.RemoveAllListeners();
            SetButtonLabel(_brawlBallButton, "Brawl Ball");

            RectTransform rect = _brawlBallButton.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = new Vector2(0f, -252f);
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
                text.text = label;
        }

        private void EnsureShowdownChoicePanel()
        {
            if (_showdownChoiceRoot != null)
                return;

            if (_soloShowdownButton != null)
                MenuUITheme.StyleButtonLabel(_soloShowdownButton, "Showdown", 17f);

            _showdownChoiceRoot = MenuUITheme.CreatePanel(
                "ShowdownChoiceOverlay",
                transform,
                new Color(0.005f, 0.012f, 0.03f, 0.88f));

            RectTransform overlayRect = _showdownChoiceRoot.GetComponent<RectTransform>();
            MenuUITheme.Stretch(overlayRect);

            GameObject panel = MenuUITheme.CreatePanel(
                "ShowdownChoicePanel",
                _showdownChoiceRoot.transform,
                MenuUITheme.Panel);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(620f, 330f);
            panelRect.anchoredPosition = Vector2.zero;

            TMP_Text title = MenuUITheme.CreateText(
                panel.transform,
                "Title",
                "CHOOSE SHOWDOWN",
                31f,
                TextAlignmentOptions.Center,
                Color.white);
            MenuUITheme.Anchor(
                title.rectTransform,
                new Vector2(0.08f, 0.73f),
                new Vector2(0.92f, 0.94f),
                Vector2.zero,
                Vector2.zero);
            title.fontStyle = FontStyles.Bold;

            TMP_Text subtitle = MenuUITheme.CreateText(
                panel.transform,
                "Subtitle",
                "Fight alone or enter with a teammate.",
                18f,
                TextAlignmentOptions.Center,
                MenuUITheme.TextMuted);
            MenuUITheme.Anchor(
                subtitle.rectTransform,
                new Vector2(0.08f, 0.59f),
                new Vector2(0.92f, 0.74f),
                Vector2.zero,
                Vector2.zero);

            _soloChoiceButton = CreateChoiceButton(
                panel.transform,
                "SoloChoiceButton",
                "SOLO",
                MenuUITheme.PrimaryButton,
                new Vector2(0.08f, 0.29f),
                new Vector2(0.48f, 0.55f));
            _duoChoiceButton = CreateChoiceButton(
                panel.transform,
                "DuoChoiceButton",
                "DUO",
                MenuUITheme.PositiveButton,
                new Vector2(0.52f, 0.29f),
                new Vector2(0.92f, 0.55f));
            _cancelChoiceButton = CreateChoiceButton(
                panel.transform,
                "CancelChoiceButton",
                "BACK",
                MenuUITheme.SecondaryButton,
                new Vector2(0.30f, 0.07f),
                new Vector2(0.70f, 0.23f));

            _soloChoiceButton.onClick.AddListener(OnSoloShowdown);
            _duoChoiceButton.onClick.AddListener(OnDuoShowdown);
            _cancelChoiceButton.onClick.AddListener(() => SetShowdownChoiceVisible(false));
            _showdownChoiceRoot.transform.SetAsLastSibling();
        }

        private static Button CreateChoiceButton(
            Transform parent,
            string name,
            string label,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Button button = buttonObject.GetComponent<Button>();
            MenuUITheme.StyleButton(button, label, color, 21f);

            TMP_Text text = MenuUITheme.CreateText(
                buttonObject.transform,
                "Label",
                label,
                21f,
                TextAlignmentOptions.Center,
                Color.white);
            text.fontStyle = FontStyles.Bold;
            MenuUITheme.Stretch(text.rectTransform);
            MenuUITheme.StyleButton(button, label, color, 21f);

            MenuUITheme.Anchor(
                buttonObject.GetComponent<RectTransform>(),
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private void SetShowdownChoiceVisible(bool visible)
        {
            if (_showdownChoiceRoot == null)
                return;

            _showdownChoiceRoot.SetActive(visible);
            if (visible)
                _showdownChoiceRoot.transform.SetAsLastSibling();
        }
    }
}
