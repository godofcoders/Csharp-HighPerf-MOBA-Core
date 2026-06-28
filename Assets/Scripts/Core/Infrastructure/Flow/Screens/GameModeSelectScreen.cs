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

        private void OnEnable()
        {
            EnsureBrawlBallButton();

            if (_gemGrabButton != null) _gemGrabButton.onClick.AddListener(OnGemGrab);
            if (_knockoutButton != null) _knockoutButton.onClick.AddListener(OnKnockout);
            if (_brawlBallButton != null) _brawlBallButton.onClick.AddListener(OnBrawlBall);
            if (_soloShowdownButton != null) _soloShowdownButton.onClick.AddListener(OnSoloShowdown);
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);

        }

        private void OnDisable()
        {
            if (_gemGrabButton != null) _gemGrabButton.onClick.RemoveListener(OnGemGrab);
            if (_knockoutButton != null) _knockoutButton.onClick.RemoveListener(OnKnockout);
            if (_brawlBallButton != null) _brawlBallButton.onClick.RemoveListener(OnBrawlBall);
            if (_soloShowdownButton != null) _soloShowdownButton.onClick.RemoveListener(OnSoloShowdown);
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

        private void OnSoloShowdown()
        {
            SelectMode(GameModeId.SoloShowdown);
        }

        private void SelectMode(GameModeId mode)
        {
            SceneSelection.SelectedMode = mode;
            SceneFlow.Instance?.LoadScene(SceneId.MapSelect);
        }

        private void OnBack() => SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);

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
    }
}
