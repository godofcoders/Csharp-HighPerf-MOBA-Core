using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Game-mode selection. Phase 1 has only Gem Grab so this is a single-
    /// button confirmation screen, but the structure supports adding more
    /// modes by enabling the corresponding inspector buttons + assigning
    /// their target GameModeId.
    /// </summary>
    public class GameModeSelectScreen : MonoBehaviour
    {
        [Header("Mode buttons")]
        [SerializeField] private Button _gemGrabButton;
        [SerializeField] private Button _knockoutButton;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private void OnEnable()
        {
            if (_gemGrabButton != null) _gemGrabButton.onClick.AddListener(OnGemGrab);
            if (_knockoutButton != null) _knockoutButton.onClick.AddListener(OnKnockout);
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);

        }

        private void OnDisable()
        {
            if (_gemGrabButton != null) _gemGrabButton.onClick.RemoveListener(OnGemGrab);
            if (_knockoutButton != null) _knockoutButton.onClick.RemoveListener(OnKnockout);
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
        }

        private void OnGemGrab()
        {
            SceneSelection.SelectedMode = GameModeId.GemGrab;
            SceneFlow.Instance?.LoadScene(SceneId.Match);
        }

        private void OnKnockout()
        {
            SceneSelection.SelectedMode = GameModeId.Knockout;
            SceneFlow.Instance?.LoadScene(SceneId.Match);
        }

        private void OnBack() => SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);
    }
}
