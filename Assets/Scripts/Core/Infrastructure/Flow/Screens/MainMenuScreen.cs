using UnityEngine;
using UnityEngine.UI;
using MOBA.Core.Definitions;

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
        [SerializeField] private Button _mapSelectButton;


        [Header("Defaults (used if nothing was picked yet)")]
        [SerializeField] private BrawlerDefinition _defaultBrawler;
        [SerializeField] private MapDefinition _defaultMap;

        [SerializeField] private GameModeId _defaultMode = GameModeId.GemGrab;

        private void OnEnable()
        {
            // Note: do NOT call SceneSelection.Reset() here — Reset
            // intentionally preserves SelectedBrawler so MainMenu keeps
            // showing the player's last pick. Mode-only reset isn't useful
            // when Play goes straight to Match with the current selection.

            if (_playButton != null) _playButton.onClick.AddListener(OnPlay);
            if (_mapSelectButton != null) _mapSelectButton.onClick.AddListener(OnMapSelect);
        }

        private void OnDisable()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlay);
            if (_mapSelectButton != null) _mapSelectButton.onClick.RemoveListener(OnMapSelect);
        }

        private void OnPlay()
        {
            // Backfill defaults if menu interactions didn't set everything.
            if (SceneSelection.SelectedBrawler == null) SceneSelection.SelectedBrawler = _defaultBrawler;
            if (SceneSelection.SelectedMap == null) SceneSelection.SelectedMap = _defaultMap;
            if (SceneSelection.SelectedMode == default) SceneSelection.SelectedMode = _defaultMode;

            SceneFlow.Instance?.LoadScene(SceneId.Match);
        }

        private void OnMapSelect()
        {
            Debug.Log("[MainMenuScreen] Map Select clicked" + SceneId.MapSelect);
            SceneFlow.Instance?.LoadScene((SceneId.MapSelect));
        }
    }
}
