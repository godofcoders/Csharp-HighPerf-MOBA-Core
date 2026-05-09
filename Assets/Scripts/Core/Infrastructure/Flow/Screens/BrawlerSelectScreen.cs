using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Brawler-pick screen. AAA-style two-pane layout:
    ///   - Card grid on one side (compact card per BrawlerDefinition).
    ///   - Detail panel on the other side, populated when the player
    ///     clicks a card (preview).
    ///   - Confirm button commits the previewed brawler into
    ///     SceneSelection and advances to GameModeSelect.
    ///
    /// Backward-compatible: if _detailPanel and _confirmButton are unwired,
    /// the screen falls back to the original "card click commits
    /// immediately" flow so simple/minimal scenes still work.
    /// </summary>
    public class BrawlerSelectScreen : MonoBehaviour
    {
        [Header("Source data")]
        [Tooltip("Available brawlers shown as cards. Designer fills with all 4 BrawlerDefinitions.")]
        [SerializeField] private BrawlerDefinition[] _availableBrawlers;

        [Header("Card spawning")]
        [Tooltip("Prefab for one compact card. Should carry a BrawlerCardView and a Button at the root.")]
        [SerializeField] private GameObject _cardPrefab;
        [Tooltip("Container under which the cards spawn (e.g. a HorizontalLayoutGroup or GridLayoutGroup).")]
        [SerializeField] private Transform _cardContainer;

        [Header("Detail preview (optional)")]
        [Tooltip("Big detail-panel BrawlerCardView. Updated when the player clicks a card.")]
        [SerializeField] private BrawlerCardView _detailPanel;
        [Tooltip("Confirm button. When clicked, commits the previewed brawler and advances.")]
        [SerializeField] private Button _confirmButton;
        [Tooltip("If true, auto-preview the first available brawler on Start so the detail panel isn't empty initially.")]
        [SerializeField] private bool _autoPreviewFirst = true;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private BrawlerDefinition _previewed;

        private void Start()
        {
            BuildCards();
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirm);
            UpdateConfirmButtonInteractable();

            if (_autoPreviewFirst && _availableBrawlers != null && _availableBrawlers.Length > 0)
            {
                for (int i = 0; i < _availableBrawlers.Length; i++)
                {
                    if (_availableBrawlers[i] != null)
                    {
                        SetPreview(_availableBrawlers[i]);
                        break;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirm);
        }

        private void BuildCards()
        {
            if (_cardPrefab == null || _cardContainer == null || _availableBrawlers == null) return;

            for (int i = 0; i < _availableBrawlers.Length; i++)
            {
                BrawlerDefinition def = _availableBrawlers[i];
                if (def == null) continue;

                GameObject card = Instantiate(_cardPrefab, _cardContainer);

                BrawlerCardView view = card.GetComponent<BrawlerCardView>();
                if (view == null) view = card.GetComponentInChildren<BrawlerCardView>();
                if (view != null)
                {
                    view.Bind(def);
                }
                else
                {
                    TMP_Text labelTmp = card.GetComponentInChildren<TMP_Text>();
                    if (labelTmp != null) labelTmp.text = def.name;
                    else
                    {
                        Text labelLegacy = card.GetComponentInChildren<Text>();
                        if (labelLegacy != null) labelLegacy.text = def.name;
                    }
                }

                Button btn = card.GetComponent<Button>();
                if (btn != null)
                {
                    BrawlerDefinition captured = def;
                    btn.onClick.AddListener(() => OnCardClicked(captured));
                }
            }
        }

        private void OnCardClicked(BrawlerDefinition def)
        {
            // Two flows depending on whether detail panel is wired:
            //   - With detail panel: click previews; Confirm button commits.
            //   - Without detail panel: click commits directly (legacy).
            if (_detailPanel != null)
                SetPreview(def);
            else
                Commit(def);
        }

        private void SetPreview(BrawlerDefinition def)
        {
            _previewed = def;
            if (_detailPanel != null) _detailPanel.Bind(def);
            UpdateConfirmButtonInteractable();
        }

        private void OnConfirm()
        {
            if (_previewed != null) Commit(_previewed);
        }

        private void Commit(BrawlerDefinition def)
        {
            SceneSelection.SelectedBrawler = def;

            // If MainMenu opened the picker just to swap the showcased
            // brawler, return to MainMenu and clear the flag instead of
            // advancing the play flow.
            if (SceneSelection.PickerReturnsToMainMenu)
            {
                SceneSelection.PickerReturnsToMainMenu = false;
                SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
                return;
            }

            SceneFlow.Instance?.LoadScene(SceneId.GameModeSelect);
        }

        private void UpdateConfirmButtonInteractable()
        {
            if (_confirmButton != null) _confirmButton.interactable = (_previewed != null);
        }

        private void OnBack() => SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
    }
}
