using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Map-select screen. Filters MapCatalog by SceneSelection.SelectedMode
    /// and instantiates one card per supported map. Card click previews;
    /// Confirm commits SceneSelection.SelectedMap and loads Match.
    ///
    /// Mirrors BrawlerSelectScreen's two-pane shape (cards + detail panel +
    /// confirm). Detail panel here is just title + description Text since
    /// maps don't have a rich stat block. Designer can extend.
    /// </summary>
    public class MapSelectScreen : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private MapCatalog _catalog;

        [Header("Defaults (used if nothing was picked yet)")]
        [Tooltip("Used when SceneSelection.SelectedBrawler is null on Confirm — happens when the player skipped BrawlerSelect (e.g. tapped Play directly).")]
        [SerializeField] private BrawlerDefinition _defaultBrawler;

        [Header("Card spawning")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private Transform _cardContainer;

        [Header("Detail preview (optional)")]
        [SerializeField] private TMP_Text _previewNameTmp;
        [SerializeField] private Text _previewNameLegacy;
        [SerializeField] private Image _previewIcon;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private bool _autoPreviewFirst = true;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private MapDefinition _previewed;
        private readonly List<GameObject> _spawnedCards = new List<GameObject>(8);

        private void Start()
        {
            BuildCards();
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirm);
            UpdateConfirmInteractable();
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirm);
        }

        private void BuildCards()
        {
            ClearSpawnedCards();

            if (_cardPrefab == null || _cardContainer == null || _catalog == null)
            {
                SetPreview(null);
                return;
            }

            var maps = _catalog.GetMapsForMode(SceneSelection.SelectedMode);
            if (maps.Count == 0)
            {
                SetPreview(null);
                return;
            }

            MapDefinition preferred = SceneSelection.SelectedMap != null &&
                                      SceneSelection.SelectedMap.SupportsMode(SceneSelection.SelectedMode)
                ? SceneSelection.SelectedMap
                : null;

            for (int i = 0; i < maps.Count; i++)
            {
                MapDefinition map = maps[i];
                if (map == null) continue;

                GameObject card = Instantiate(_cardPrefab, _cardContainer);
                _spawnedCards.Add(card);

                // Name label.
                TMP_Text labelTmp = card.GetComponentInChildren<TMP_Text>();
                string nm = !string.IsNullOrWhiteSpace(map.DisplayName) ? map.DisplayName : map.name;
                if (labelTmp != null) labelTmp.text = nm;
                else
                {
                    Text labelLegacy = card.GetComponentInChildren<Text>();
                    if (labelLegacy != null) labelLegacy.text = nm;
                }

                // Icon (assigns the FIRST Image in the card that ISN'T the
                // root Button background — heuristic: child Image).
                if (map.Icon != null)
                {
                    Image[] images = card.GetComponentsInChildren<Image>();
                    for (int k = 0; k < images.Length; k++)
                    {
                        if (images[k].gameObject == card) continue;
                        images[k].sprite = map.Icon;
                        break;
                    }
                }

                Button btn = card.GetComponent<Button>();
                if (btn != null)
                {
                    MapDefinition captured = map;
                    btn.onClick.AddListener(() => SetPreview(captured));
                }

                if (preferred == null && _autoPreviewFirst && _previewed == null)
                    SetPreview(map);
            }

            if (preferred != null)
                SetPreview(preferred);
        }

        private void SetPreview(MapDefinition map)
        {
            _previewed = map;
            string nm = map != null && !string.IsNullOrWhiteSpace(map.DisplayName) ? map.DisplayName : (map != null ? map.name : "");
            if (_previewNameTmp != null) _previewNameTmp.text = nm;
            else if (_previewNameLegacy != null) _previewNameLegacy.text = nm;
            if (_previewIcon != null)
            {
                _previewIcon.sprite = map != null ? map.Icon : null;
                _previewIcon.enabled = map != null && map.Icon != null;
            }

            UpdateConfirmInteractable();
        }

        private void OnConfirm()
        {
            if (_previewed == null) return;
            SceneSelection.SelectedMap = _previewed;
            // Backfill brawler default so Match doesn't NRE when the player
            // skipped BrawlerSelect (Play → GameModeSelect → MapSelect path).
            if (SceneSelection.SelectedBrawler == null) SceneSelection.SelectedBrawler = _defaultBrawler;
            SceneFlow.Instance?.LoadScene(SceneId.Match);
        }

        private void UpdateConfirmInteractable()
        {
            if (_confirmButton != null) _confirmButton.interactable = _previewed != null;
        }

        private void OnBack() => SceneFlow.Instance?.LoadScene(SceneId.GameModeSelect);

        private void ClearSpawnedCards()
        {
            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    Destroy(_spawnedCards[i]);
            }

            _spawnedCards.Clear();
            _previewed = null;
        }
    }
}
