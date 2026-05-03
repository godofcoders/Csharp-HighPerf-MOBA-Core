using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Brawler-pick screen. Designer drops the available BrawlerDefinitions
    /// into _availableBrawlers; the screen instantiates one button-card per
    /// brawler from a prefab. Clicking a card stores the choice in
    /// SceneSelection and advances to game-mode select.
    ///
    /// The card prefab is expected to have a Button + (optional) TMP_Text
    /// label that this script populates. For Phase 1 we keep card visuals
    /// simple — just the brawler's display name. Portraits / archetype
    /// icons are a polish pass.
    /// </summary>
    public class BrawlerSelectScreen : MonoBehaviour
    {
        [Header("Source data")]
        [Tooltip("Available brawlers shown as cards. Designer fills with all 4 BrawlerDefinitions.")]
        [SerializeField] private BrawlerDefinition[] _availableBrawlers;

        [Header("Card spawning")]
        [Tooltip("Prefab for one brawler card. Must have a Button at root + optional TMP_Text child for the label.")]
        [SerializeField] private GameObject _cardPrefab;
        [Tooltip("Container under which the cards spawn (e.g. a HorizontalLayoutGroup).")]
        [SerializeField] private Transform _cardContainer;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private void Start()
        {
            BuildCards();
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
        }

        private void BuildCards()
        {
            if (_cardPrefab == null || _cardContainer == null || _availableBrawlers == null) return;

            for (int i = 0; i < _availableBrawlers.Length; i++)
            {
                BrawlerDefinition def = _availableBrawlers[i];
                if (def == null) continue;

                GameObject card = Instantiate(_cardPrefab, _cardContainer);

                // Label: prefer TMP, fall back to legacy.
                TMP_Text labelTmp = card.GetComponentInChildren<TMP_Text>();
                if (labelTmp != null) labelTmp.text = def.name;
                else
                {
                    Text labelLegacy = card.GetComponentInChildren<Text>();
                    if (labelLegacy != null) labelLegacy.text = def.name;
                }

                Button btn = card.GetComponent<Button>();
                if (btn != null)
                {
                    BrawlerDefinition captured = def;
                    btn.onClick.AddListener(() => OnPick(captured));
                }
            }
        }

        private void OnPick(BrawlerDefinition def)
        {
            SceneSelection.SelectedBrawler = def;
            SceneFlow.Instance?.LoadScene(SceneId.GameModeSelect);
        }

        private void OnBack() => SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
    }
}
