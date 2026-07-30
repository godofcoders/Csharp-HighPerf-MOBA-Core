using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Combined mode/map picker. Mode tabs filter MapCatalog, then the
    /// chosen map commits SceneSelection.SelectedMap before returning Home.
    /// </summary>
    public class MapSelectScreen : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private MapCatalog _catalog;

        [Header("Defaults (used if nothing was picked yet)")]
        [Tooltip("Used when SceneSelection.SelectedBrawler is null on Confirm.")]
        [SerializeField] private BrawlerDefinition _defaultBrawler;
        [Tooltip("Optional roster used to restore the player's saved brawler when opening Map Select directly.")]
        [SerializeField] private BrawlerDefinition[] _availableBrawlers;

        [Header("Legacy card spawning")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private Transform _cardContainer;

        [Header("Legacy detail preview")]
        [SerializeField] private TMP_Text _previewNameTmp;
        [SerializeField] private Text _previewNameLegacy;
        [SerializeField] private Image _previewIcon;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private bool _autoPreviewFirst = true;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        [Header("Runtime View")]
        [SerializeField] private bool _useBrawlInspiredRuntimeView = true;
        [SerializeField] private bool _hideLegacySceneWidgetsWhenRuntimeViewActive = true;
        [SerializeField] private Color _screenBackground = MenuUITheme.ScreenBackground;
        [SerializeField] private Color _panelColor = MenuUITheme.Panel;
        [SerializeField] private Color _panelDarkColor = MenuUITheme.PanelDark;
        [SerializeField] private Color _goldColor = MenuUITheme.Gold;
        [SerializeField] private Color _cyanColor = MenuUITheme.Cyan;

        private static readonly GameModeId[] ModeDisplayOrder =
        {
            GameModeId.GemGrab,
            GameModeId.BrawlBall,
            GameModeId.Knockout,
            GameModeId.SoloShowdown,
            GameModeId.HotZone
        };

        private MapDefinition _previewed;
        private readonly List<GameObject> _spawnedCards = new List<GameObject>(8);
        private readonly Dictionary<MapDefinition, RuntimeMapCardView> _runtimeCards =
            new Dictionary<MapDefinition, RuntimeMapCardView>(8);
        private readonly Dictionary<GameModeId, RuntimeModeTabView> _runtimeModeTabs =
            new Dictionary<GameModeId, RuntimeModeTabView>(8);

        private RectTransform _runtimeRoot;
        private Transform _runtimeModeTabContainer;
        private Transform _runtimeCardContainer;
        private TMP_Text _modeHeaderText;
        private TMP_Text _detailNameText;
        private TMP_Text _detailModeText;
        private TMP_Text _detailTagText;
        private TMP_Text _detailDescriptionText;
        private TMP_Text _detailPrefabText;
        private TMP_Text _detailSelectedText;
        private Image _detailPreviewBackground;
        private Image _detailSelectedBackground;

        private void Start()
        {
            ApplyRuntimeTheme();
            NormalizeSelectedMode();

            if (_useBrawlInspiredRuntimeView)
            {
                BuildRuntimeView();
                BuildRuntimeCards();
            }
            else
            {
                BuildLegacyCards();
            }

            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirm);
            UpdateConfirmInteractable();
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirm);
        }

        private void ApplyRuntimeTheme()
        {
            _screenBackground = MenuUITheme.ScreenBackground;
            _panelColor = MenuUITheme.Panel;
            _panelDarkColor = MenuUITheme.PanelDark;
            _goldColor = MenuUITheme.Gold;
            _cyanColor = MenuUITheme.Cyan;
        }

        private void NormalizeSelectedMode()
        {
            if (HasMapsForMode(SceneSelection.SelectedMode))
                return;

            for (int i = 0; i < ModeDisplayOrder.Length; i++)
            {
                if (!HasMapsForMode(ModeDisplayOrder[i]))
                    continue;

                SceneSelection.SelectedMode = ModeDisplayOrder[i];
                return;
            }
        }

        private bool HasMapsForMode(GameModeId mode)
        {
            if (_catalog == null || _catalog.Maps == null)
                return false;

            for (int i = 0; i < _catalog.Maps.Length; i++)
            {
                MapDefinition map = _catalog.Maps[i];
                if (map != null && map.SupportsMode(mode))
                    return true;
            }

            return false;
        }

        private void BuildRuntimeView()
        {
            RectTransform host = transform as RectTransform;
            Transform parent = host != null ? host : transform;

            GameObject rootObject = CreatePanel("BrawlStyleMapSelect", parent, _screenBackground);
            _runtimeRoot = rootObject.GetComponent<RectTransform>();
            Stretch(_runtimeRoot);
            _runtimeRoot.SetAsLastSibling();

            if (_hideLegacySceneWidgetsWhenRuntimeViewActive)
                HideLegacySceneWidgets(rootObject.transform);

            BuildHeader(_runtimeRoot);
            BuildModeTabs(_runtimeRoot);
            BuildMapListPanel(_runtimeRoot);
            BuildDetailPanel(_runtimeRoot);
            BuildActionButtons(_runtimeRoot);
        }

        private void HideLegacySceneWidgets(Transform runtimeRoot)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == runtimeRoot)
                    continue;

                child.gameObject.SetActive(false);
            }
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = CreatePanel("Header", parent, MenuUITheme.Header);
            RectTransform rect = header.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0f, 0.90f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText(header.transform, "Title", "EVENTS", 42, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.035f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);

            _modeHeaderText = CreateText(
                header.transform,
                "Mode",
                ResolveModeLabel(SceneSelection.SelectedMode) + " MAPS",
                22,
                TextAlignmentOptions.Right,
                _goldColor);
            _modeHeaderText.fontStyle = FontStyles.Bold;
            Anchor(_modeHeaderText.rectTransform, new Vector2(0.48f, 0f), new Vector2(0.965f, 1f), Vector2.zero, Vector2.zero);
        }

        private void BuildModeTabs(Transform parent)
        {
            GameObject tabs = CreatePanel("ModeTabs", parent, MenuUITheme.ActionRail);
            RectTransform rect = tabs.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.035f, 0.805f), new Vector2(0.965f, 0.885f), Vector2.zero, Vector2.zero);

            HorizontalLayoutGroup layout = tabs.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _runtimeModeTabContainer = tabs.transform;
            _runtimeModeTabs.Clear();

            for (int i = 0; i < ModeDisplayOrder.Length; i++)
            {
                GameModeId mode = ModeDisplayOrder[i];
                if (!HasMapsForMode(mode))
                    continue;

                CreateRuntimeModeTab(mode);
            }

            RefreshRuntimeModeTabs();
        }

        private void CreateRuntimeModeTab(GameModeId mode)
        {
            GameObject tab = CreatePanel("ModeTab_" + ResolveModeLabel(mode).Replace(" ", ""), _runtimeModeTabContainer, _panelColor);
            LayoutElement layoutElement = tab.AddComponent<LayoutElement>();
            layoutElement.minHeight = 46f;
            layoutElement.flexibleWidth = 1f;

            Button button = tab.AddComponent<Button>();
            button.targetGraphic = tab.GetComponent<Image>();
            GameModeId captured = mode;
            button.onClick.AddListener(() => SelectMode(captured));

            TMP_Text label = CreateText(tab.transform, "Label", ResolveModeLabel(mode), 18, TextAlignmentOptions.Center, Color.white);
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 18f;
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));
            MenuUITheme.StyleButton(button, ResolveModeLabel(mode), _panelColor, 18f);

            _runtimeModeTabs[mode] = new RuntimeModeTabView(tab.GetComponent<Image>(), label);
        }

        private void SelectMode(GameModeId mode)
        {
            if (!HasMapsForMode(mode))
                return;

            SceneSelection.SelectedMode = mode;
            if (SceneSelection.SelectedMap != null && !SceneSelection.SelectedMap.SupportsMode(mode))
                SceneSelection.SelectedMap = null;

            if (_useBrawlInspiredRuntimeView)
                BuildRuntimeCards();
            else
                BuildLegacyCards();

            RefreshRuntimeModeTabs();
        }

        private void BuildMapListPanel(Transform parent)
        {
            GameObject panel = CreatePanel("MapListPanel", parent, _panelDarkColor);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.035f, 0.16f), new Vector2(0.625f, 0.78f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(16, 16, 14, 14);
            panelLayout.spacing = 12f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            TMP_Text label = CreateText(panel.transform, "ListTitle", "AVAILABLE MAPS", 22, TextAlignmentOptions.Left, _goldColor);
            label.fontStyle = FontStyles.Bold;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 34f;

            GameObject grid = new GameObject("MapGrid", typeof(RectTransform));
            grid.transform.SetParent(panel.transform, false);
            GridLayoutGroup gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(260f, 138f);
            gridLayout.spacing = new Vector2(14f, 14f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            gridLayout.childAlignment = TextAnchor.UpperLeft;

            LayoutElement gridLayoutElement = grid.AddComponent<LayoutElement>();
            gridLayoutElement.flexibleHeight = 1f;
            gridLayoutElement.minHeight = 440f;

            _runtimeCardContainer = grid.transform;
            _cardContainer = _runtimeCardContainer;
        }

        private void BuildDetailPanel(Transform parent)
        {
            GameObject panel = CreatePanel("DetailPanel", parent, _panelColor);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.655f, 0.16f), new Vector2(0.965f, 0.78f), Vector2.zero, Vector2.zero);

            _detailPreviewBackground = CreatePanel("MapPreview", panel.transform, MenuUITheme.PreviewPanel).GetComponent<Image>();
            Anchor(_detailPreviewBackground.rectTransform, new Vector2(0.07f, 0.52f), new Vector2(0.93f, 0.94f), Vector2.zero, Vector2.zero);
            BuildPreviewGraphic(_detailPreviewBackground.transform);

            _detailSelectedText = CreateText(panel.transform, "SelectedBadge", "SELECTED", 15, TextAlignmentOptions.Center, Color.black);
            _detailSelectedText.fontStyle = FontStyles.Bold;
            _detailSelectedBackground = CreatePanel("SelectedBadgeBg", panel.transform, _goldColor).GetComponent<Image>();
            _detailSelectedBackground.transform.SetSiblingIndex(_detailSelectedText.transform.GetSiblingIndex());
            Anchor(_detailSelectedBackground.rectTransform, new Vector2(0.57f, 0.86f), new Vector2(0.90f, 0.925f), Vector2.zero, Vector2.zero);
            Anchor(_detailSelectedText.rectTransform, new Vector2(0.57f, 0.86f), new Vector2(0.90f, 0.925f), Vector2.zero, Vector2.zero);

            _detailNameText = CreateText(panel.transform, "MapName", "SELECT MAP", 34, TextAlignmentOptions.Left, Color.white);
            _detailNameText.fontStyle = FontStyles.Bold;
            _detailNameText.enableAutoSizing = true;
            _detailNameText.fontSizeMin = 20f;
            _detailNameText.fontSizeMax = 34f;
            Anchor(_detailNameText.rectTransform, new Vector2(0.07f, 0.39f), new Vector2(0.93f, 0.50f), Vector2.zero, Vector2.zero);

            _detailModeText = CreateText(panel.transform, "MapMode", ResolveModeLabel(SceneSelection.SelectedMode), 18, TextAlignmentOptions.Left, _goldColor);
            _detailModeText.fontStyle = FontStyles.Bold;
            Anchor(_detailModeText.rectTransform, new Vector2(0.07f, 0.335f), new Vector2(0.93f, 0.39f), Vector2.zero, Vector2.zero);

            _detailTagText = CreateText(panel.transform, "MapTag", "", 18, TextAlignmentOptions.Left, _cyanColor);
            _detailTagText.fontStyle = FontStyles.Bold;
            Anchor(_detailTagText.rectTransform, new Vector2(0.07f, 0.275f), new Vector2(0.93f, 0.33f), Vector2.zero, Vector2.zero);

            _detailDescriptionText = CreateText(panel.transform, "MapDescription", "", 15, TextAlignmentOptions.Left, MenuUITheme.TextSoft);
            Anchor(_detailDescriptionText.rectTransform, new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.265f), Vector2.zero, Vector2.zero);

            _detailPrefabText = CreateText(panel.transform, "MapPrefab", "", 13, TextAlignmentOptions.Left, MenuUITheme.TextMuted);
            Anchor(_detailPrefabText.rectTransform, new Vector2(0.07f, 0.055f), new Vector2(0.93f, 0.12f), Vector2.zero, Vector2.zero);
        }

        private void BuildPreviewGraphic(Transform parent)
        {
            CreatePreviewLane(parent, "TopLane", new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.77f), new Color(0.90f, 0.34f, 0.18f, 1f));
            CreatePreviewLane(parent, "MidLane", new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.56f), _goldColor);
            CreatePreviewLane(parent, "BotLane", new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.32f), new Color(0.12f, 0.70f, 0.42f, 1f));

            GameObject gem = CreatePanel("GemMine", parent, new Color(0.94f, 0.18f, 0.84f, 1f));
            Anchor(gem.GetComponent<RectTransform>(), new Vector2(0.43f, 0.39f), new Vector2(0.57f, 0.62f), Vector2.zero, Vector2.zero);
        }

        private void CreatePreviewLane(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject lane = CreatePanel(name, parent, color);
            Image image = lane.GetComponent<Image>();
            image.raycastTarget = false;
            Anchor(lane.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);
        }

        private void BuildActionButtons(Transform parent)
        {
            _backButton = CreateButton(parent, "BackButton", "BACK", MenuUITheme.SecondaryButton, null);
            Anchor(_backButton.GetComponent<RectTransform>(), new Vector2(0.035f, 0.045f), new Vector2(0.18f, 0.12f), Vector2.zero, Vector2.zero);

            _confirmButton = CreateButton(parent, "SelectButton", "SELECT", MenuUITheme.PrimaryButton, null);
            Anchor(_confirmButton.GetComponent<RectTransform>(), new Vector2(0.735f, 0.045f), new Vector2(0.965f, 0.12f), Vector2.zero, Vector2.zero);
        }

        private void BuildRuntimeCards()
        {
            ClearSpawnedCards();
            _runtimeCards.Clear();

            if (_runtimeCardContainer == null || _catalog == null)
            {
                SetPreview(null);
                return;
            }

            List<MapDefinition> maps = _catalog.GetMapsForMode(SceneSelection.SelectedMode);
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
                if (map == null)
                    continue;

                CreateRuntimeMapCard(map, i);

                if (preferred == null && _autoPreviewFirst && _previewed == null)
                    SetPreview(map);
            }

            if (preferred != null)
                SetPreview(preferred);
        }

        private void CreateRuntimeMapCard(MapDefinition map, int index)
        {
            Color baseColor = ResolveMapColor(map, index);
            GameObject card = CreatePanel("MapCard_" + ResolveMapName(map), _runtimeCardContainer, baseColor * 0.82f);
            _spawnedCards.Add(card);

            Button button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            MapDefinition captured = map;
            button.onClick.AddListener(() => SetPreview(captured));

            Image accent = CreatePanel("Accent", card.transform, _cyanColor).GetComponent<Image>();
            accent.raycastTarget = false;
            Anchor(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0.035f, 1f), Vector2.zero, Vector2.zero);

            Image selectedOverlay = CreatePanel("SelectedOverlay", card.transform, new Color(1f, 0.76f, 0.12f, 0.22f)).GetComponent<Image>();
            selectedOverlay.raycastTarget = false;
            Anchor(selectedOverlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            selectedOverlay.enabled = false;

            TMP_Text selectedLabel = CreateText(card.transform, "SelectedLabel", "SELECTED", 13, TextAlignmentOptions.Right, _goldColor);
            selectedLabel.fontStyle = FontStyles.Bold;
            Anchor(selectedLabel.rectTransform, new Vector2(0.52f, 0.74f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
            selectedLabel.gameObject.SetActive(false);

            TMP_Text title = CreateText(card.transform, "MapName", ResolveMapName(map).ToUpperInvariant(), 25, TextAlignmentOptions.Left, Color.white);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;
            title.fontSizeMin = 15f;
            title.fontSizeMax = 25f;
            Anchor(title.rectTransform, new Vector2(0.10f, 0.46f), new Vector2(0.94f, 0.78f), Vector2.zero, Vector2.zero);

            TMP_Text mode = CreateText(card.transform, "Mode", ResolveModeLabel(SceneSelection.SelectedMode), 14, TextAlignmentOptions.Left, _goldColor);
            mode.fontStyle = FontStyles.Bold;
            Anchor(mode.rectTransform, new Vector2(0.10f, 0.27f), new Vector2(0.94f, 0.45f), Vector2.zero, Vector2.zero);

            TMP_Text tag = CreateText(card.transform, "Tag", ResolveMapTag(map, SceneSelection.SelectedMode), 13, TextAlignmentOptions.Left, MenuUITheme.TextSoft);
            Anchor(tag.rectTransform, new Vector2(0.10f, 0.08f), new Vector2(0.94f, 0.26f), Vector2.zero, Vector2.zero);

            _runtimeCards[map] = new RuntimeMapCardView(card.GetComponent<Image>(), accent, selectedOverlay, selectedLabel);
        }

        private void BuildLegacyCards()
        {
            ClearSpawnedCards();

            if (_cardPrefab == null || _cardContainer == null || _catalog == null)
            {
                SetPreview(null);
                return;
            }

            List<MapDefinition> maps = _catalog.GetMapsForMode(SceneSelection.SelectedMode);
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

                TMP_Text labelTmp = card.GetComponentInChildren<TMP_Text>();
                string nm = ResolveMapName(map);
                if (labelTmp != null) labelTmp.text = nm;
                else
                {
                    Text labelLegacy = card.GetComponentInChildren<Text>();
                    if (labelLegacy != null) labelLegacy.text = nm;
                }

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

            string nm = ResolveMapName(map);
            if (_previewNameTmp != null) _previewNameTmp.text = nm;
            else if (_previewNameLegacy != null) _previewNameLegacy.text = nm;

            if (_previewIcon != null)
            {
                _previewIcon.sprite = map != null ? map.Icon : null;
                _previewIcon.enabled = map != null && map.Icon != null;
            }

            RefreshRuntimePreview();
            UpdateConfirmInteractable();
        }

        private void RefreshRuntimePreview()
        {
            if (!_useBrawlInspiredRuntimeView)
                return;

            RefreshRuntimeCardSelection();

            if (_modeHeaderText != null)
                _modeHeaderText.text = ResolveModeLabel(SceneSelection.SelectedMode) + " MAPS";

            RefreshRuntimeModeTabs();

            if (_previewed == null)
            {
                if (_detailNameText != null) _detailNameText.text = "NO MAPS";
                if (_detailModeText != null) _detailModeText.text = ResolveModeLabel(SceneSelection.SelectedMode);
                if (_detailTagText != null) _detailTagText.text = "";
                if (_detailDescriptionText != null) _detailDescriptionText.text = "No maps are available for this mode.";
                if (_detailPrefabText != null) _detailPrefabText.text = "";
                if (_detailSelectedText != null) _detailSelectedText.text = "";
                if (_detailSelectedBackground != null) _detailSelectedBackground.gameObject.SetActive(false);
                return;
            }

            Color mapColor = ResolveMapColor(_previewed, 0);
            if (_detailPreviewBackground != null)
                _detailPreviewBackground.color = mapColor * 0.74f;
            if (_detailSelectedText != null)
                _detailSelectedText.text = "SELECTED";
            if (_detailSelectedBackground != null)
                _detailSelectedBackground.gameObject.SetActive(true);
            if (_detailNameText != null)
                _detailNameText.text = ResolveMapName(_previewed).ToUpperInvariant();
            if (_detailModeText != null)
                _detailModeText.text = ResolveModeLabel(SceneSelection.SelectedMode);
            if (_detailTagText != null)
                _detailTagText.text = ResolveMapTag(_previewed, SceneSelection.SelectedMode);
            if (_detailDescriptionText != null)
                _detailDescriptionText.text = ResolveMapDescription(_previewed, SceneSelection.SelectedMode);
            if (_detailPrefabText != null)
                _detailPrefabText.text = _previewed.MapPrefab != null ? _previewed.MapPrefab.name : "NO PREFAB";
        }

        private void RefreshRuntimeCardSelection()
        {
            foreach (KeyValuePair<MapDefinition, RuntimeMapCardView> entry in _runtimeCards)
            {
                bool selected = entry.Key == _previewed;
                if (entry.Value.Background != null)
                    entry.Value.Background.color = selected
                        ? _cyanColor
                        : ResolveMapColor(entry.Key, 0) * 0.82f;
                if (entry.Value.Accent != null)
                    entry.Value.Accent.color = selected ? _goldColor : _cyanColor;
                if (entry.Value.SelectedOverlay != null)
                    entry.Value.SelectedOverlay.enabled = selected;
                if (entry.Value.SelectedLabel != null)
                    entry.Value.SelectedLabel.gameObject.SetActive(selected);
            }
        }

        private void RefreshRuntimeModeTabs()
        {
            foreach (KeyValuePair<GameModeId, RuntimeModeTabView> entry in _runtimeModeTabs)
            {
                bool selected = entry.Key == SceneSelection.SelectedMode;
                if (entry.Value.Background != null)
                    entry.Value.Background.color = selected ? _goldColor : _panelColor;
                if (entry.Value.Label != null)
                    entry.Value.Label.color = selected ? Color.black : Color.white;
            }
        }

        private void OnConfirm()
        {
            if (_previewed == null) return;
            SceneSelection.SelectedMap = _previewed;
            if (SceneSelection.SelectedBrawler == null)
            {
                if (PlayerBrawlerProgress.TryGetSelectedBrawler(_availableBrawlers, out BrawlerDefinition saved))
                    AssignSelectedBrawler(saved);
                else if (!PlayerBrawlerProgress.HasSavedSelectedBrawler())
                    AssignSelectedBrawler(_defaultBrawler);
            }

            SceneSelection.MapSelectReturnScene = SceneId.MainMenu;
            SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
        }

        private static void AssignSelectedBrawler(BrawlerDefinition brawler)
        {
            SceneSelection.SelectedBrawler = brawler;

            if (brawler != null)
                SceneSelection.SelectedBuildPowerLevel = PlayerBrawlerProgress.GetLevel(brawler);
        }

        private void UpdateConfirmInteractable()
        {
            if (_confirmButton != null) _confirmButton.interactable = _previewed != null;
        }

        private void OnBack()
        {
            SceneId target = SceneSelection.MapSelectReturnScene;
            if (target == SceneId.MapSelect || target == SceneId.Match || target == SceneId.Results)
                target = SceneId.MainMenu;

            SceneFlow.Instance?.LoadScene(target);
        }

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

        private static string ResolveMapName(MapDefinition map)
        {
            if (map == null)
                return "";

            return !string.IsNullOrWhiteSpace(map.DisplayName)
                ? map.DisplayName
                : map.name;
        }

        private static string ResolveModeLabel(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.GemGrab:
                    return "GEM GRAB";
                case GameModeId.Knockout:
                    return "KNOCKOUT";
                case GameModeId.BrawlBall:
                    return "BRAWL BALL";
                case GameModeId.HotZone:
                    return "HOT ZONE";
                case GameModeId.SoloShowdown:
                    return "SOLO SHOWDOWN";
                default:
                    return mode.ToString().ToUpperInvariant();
            }
        }

        private static string ResolveMapTag(MapDefinition map, GameModeId mode)
        {
            string name = ResolveMapName(map).ToLowerInvariant();
            string modifier = map != null && map.EnablesNanopowersForMode(mode)
                ? "NANO EVENT"
                : "CLASSIC START";

            if (name.Contains("crossfire"))
                return "MID CONTROL | " + modifier;
            if (name.Contains("side"))
                return "SIDE LANES | " + modifier;
            if (name.Contains("yard"))
                return "BALANCED | " + modifier;

            return "STANDARD | " + modifier;
        }

        private static string ResolveMapDescription(MapDefinition map, GameModeId mode)
        {
            string name = ResolveMapName(map).ToLowerInvariant();
            string modifier = map != null && map.EnablesNanopowersForMode(mode)
                ? " This map opens with a short nanopower draft before the countdown finishes."
                : " This map starts with the normal countdown and no nanopower draft.";

            if (name.Contains("crossfire"))
                return "Compact center cover creates quick fights around the gem mine and rewards clean lane pressure." + modifier;
            if (name.Contains("side"))
                return "Wider side routes give flankers room while the center stays open enough for ranged control." + modifier;
            if (name.Contains("yard"))
                return "Classic three-lane layout with simple cover, readable rotations, and a clear center objective." + modifier;

            return "Playable arena for the selected mode." + modifier;
        }

        private Color ResolveMapColor(MapDefinition map, int fallbackIndex)
        {
            string name = ResolveMapName(map).ToLowerInvariant();
            if (name.Contains("crossfire"))
                return new Color(0.82f, 0.20f, 0.24f, 1f);
            if (name.Contains("side"))
                return new Color(0.18f, 0.58f, 0.78f, 1f);
            if (name.Contains("yard"))
                return new Color(0.18f, 0.68f, 0.35f, 1f);

            switch (fallbackIndex % 3)
            {
                case 0:
                    return new Color(0.18f, 0.68f, 0.35f, 1f);
                case 1:
                    return new Color(0.82f, 0.20f, 0.24f, 1f);
                default:
                    return new Color(0.18f, 0.58f, 0.78f, 1f);
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            return MenuUITheme.CreatePanel(name, parent, color);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color color,
            UnityAction onClick)
        {
            GameObject go = CreatePanel(name, parent, color);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();

            if (onClick != null)
                button.onClick.AddListener(onClick);

            TMP_Text text = CreateText(go.transform, "Label", label, 17, TextAlignmentOptions.Center, Color.white);
            text.fontStyle = FontStyles.Bold;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            MenuUITheme.StyleButton(button, label, color, 17f);

            return button;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            return MenuUITheme.CreateText(parent, name, text, fontSize, alignment, color);
        }

        private static void Stretch(RectTransform rect)
        {
            MenuUITheme.Stretch(rect);
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            MenuUITheme.Anchor(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        private sealed class RuntimeMapCardView
        {
            public readonly Image Background;
            public readonly Image Accent;
            public readonly Image SelectedOverlay;
            public readonly TMP_Text SelectedLabel;

            public RuntimeMapCardView(
                Image background,
                Image accent,
                Image selectedOverlay,
                TMP_Text selectedLabel)
            {
                Background = background;
                Accent = accent;
                SelectedOverlay = selectedOverlay;
                SelectedLabel = selectedLabel;
            }
        }

        private sealed class RuntimeModeTabView
        {
            public readonly Image Background;
            public readonly TMP_Text Label;

            public RuntimeModeTabView(Image background, TMP_Text label)
            {
                Background = background;
                Label = label;
            }
        }
    }
}
