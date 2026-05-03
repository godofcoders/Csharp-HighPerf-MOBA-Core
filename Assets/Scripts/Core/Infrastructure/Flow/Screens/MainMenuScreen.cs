using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Main menu landing screen. Single "Play" button advances to brawler
    /// selection. Optional Quit button. Resets SceneSelection on enable so
    /// returning here from end-of-match clears stale picks.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _quitButton;

        private void OnEnable()
        {
            SceneSelection.Reset();

            if (_playButton != null) _playButton.onClick.AddListener(OnPlay);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuit);
        }

        private void OnDisable()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlay);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuit);
        }

        private void OnPlay() => SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);
        private void OnQuit() => Application.Quit();
    }
}
