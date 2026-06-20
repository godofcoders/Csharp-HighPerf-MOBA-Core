using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Brawler-pick screen with a Brawl Stars-inspired runtime layout:
    /// roster grid, large focused brawler panel, stat meters, ability details,
    /// loadout slot cycling, and a single confirm path that carries the chosen
    /// build into match spawn.
    /// </summary>
    public class BrawlerSelectScreen : MonoBehaviour
    {
        [Header("Source data")]
        [Tooltip("Available brawlers shown as cards.")]
        [SerializeField] private BrawlerDefinition[] _availableBrawlers;

        [Header("Legacy card spawning")]
        [Tooltip("Legacy prefab for one compact card. Used only when runtime revamp is disabled.")]
        [SerializeField] private GameObject _cardPrefab;
        [Tooltip("Legacy container under which cards spawn when runtime revamp is disabled.")]
        [SerializeField] private Transform _cardContainer;

        [Header("Legacy detail preview")]
        [SerializeField] private BrawlerCardView _detailPanel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private bool _autoPreviewFirst = true;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        [Header("Runtime Revamp")]
        [SerializeField] private bool _useBrawlInspiredRuntimeView = true;
        [SerializeField] private bool _hideLegacySceneWidgetsWhenRuntimeViewActive = true;
        [SerializeField] private Color _screenBackground = new Color(0.035f, 0.075f, 0.18f, 1f);
        [SerializeField] private Color _panelColor = new Color(0.06f, 0.11f, 0.24f, 0.96f);
        [SerializeField] private Color _panelDarkColor = new Color(0.025f, 0.045f, 0.11f, 0.98f);
        [SerializeField] private Color _goldColor = new Color(1f, 0.76f, 0.12f, 1f);
        [SerializeField] private Color _cyanColor = new Color(0.12f, 0.76f, 1f, 1f);

        [Header("Loadout preview")]
        [Tooltip("Container for generated gadget/star power/gear/hypercharge slot buttons. If empty, the runtime view provides one.")]
        [SerializeField] private Transform _loadoutContainer;
        [SerializeField] private TMP_Text _loadoutStatusText;
        [Tooltip("Power level used when previewing build slots. Prototype defaults to 11 so every slot can be tested.")]
        [Range(1, 11)]
        [SerializeField] private int _previewPowerLevel = PlayerBrawlerProgress.MaxLevel;
        [SerializeField] private bool _createRuntimeLoadoutPanelWhenMissing = true;
        [SerializeField] private bool _autoSelectFirstOptionPerSlot = true;

        [Header("Temporary Flow")]
        [SerializeField] private bool _commitImmediatelyOnCardClick = false;

        private BrawlerDefinition _previewed;

        private readonly List<GameObject> _spawnedCards = new List<GameObject>(12);
        private readonly List<GameObject> _loadoutRows = new List<GameObject>(8);
        private readonly List<BrawlerBuildSlotDefinition> _previewSlots =
            new List<BrawlerBuildSlotDefinition>(8);
        private readonly Dictionary<string, BrawlerBuildOptionDefinition> _selectedOptions =
            new Dictionary<string, BrawlerBuildOptionDefinition>(8);
        private readonly Dictionary<BrawlerDefinition, RosterCardView> _rosterCards =
            new Dictionary<BrawlerDefinition, RosterCardView>(12);

        private RectTransform _runtimeRoot;
        private Transform _runtimeRosterContainer;
        private Image _heroPortraitImage;
        private TMP_Text _heroInitialText;
        private TMP_Text _heroNameText;
        private TMP_Text _heroRoleText;
        private TMP_Text _heroPowerText;
        private TMP_Text _heroSummaryText;
        private TMP_Text _attackTitleText;
        private TMP_Text _attackDetailText;
        private TMP_Text _superTitleText;
        private TMP_Text _superDetailText;
        private StatRowView _healthStat;
        private StatRowView _attackStat;
        private StatRowView _superStat;
        private StatRowView _rangeStat;
        private StatRowView _speedStat;
        private StatRowView _teamStat;

        private void Start()
        {
            if (_useBrawlInspiredRuntimeView)
            {
                BuildRuntimeView();
                BuildRuntimeRosterCards();
            }
            else
            {
                EnsureFallbackLoadoutPanel();
                BuildLegacyCards();
            }

            if (_backButton != null)
                _backButton.onClick.AddListener(OnBack);
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirm);

            UpdateConfirmButtonInteractable();

            if (_autoPreviewFirst)
                PreviewInitialBrawler();
        }

        private void OnDestroy()
        {
            if (_backButton != null)
                _backButton.onClick.RemoveListener(OnBack);
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirm);
        }

        private void PreviewInitialBrawler()
        {
            if (_availableBrawlers == null)
                return;

            for (int i = 0; i < _availableBrawlers.Length; i++)
            {
                if (_availableBrawlers[i] == null)
                    continue;

                SetPreview(_availableBrawlers[i]);
                return;
            }
        }

        private void OnCardClicked(BrawlerDefinition def)
        {
            SetPreview(def);

            if (_commitImmediatelyOnCardClick && !_useBrawlInspiredRuntimeView)
                Commit(def);
        }

        private void SetPreview(BrawlerDefinition def)
        {
            _previewed = def;

            if (_detailPanel != null && !_useBrawlInspiredRuntimeView)
                _detailPanel.Bind(def);

            SeedSelectedLoadout(def);
            RefreshLoadoutUI();
            RefreshRuntimePreview();
            UpdateConfirmButtonInteractable();
        }

        private void OnConfirm()
        {
            if (_previewed != null)
                Commit(_previewed);
        }

        private void Commit(BrawlerDefinition def)
        {
            SceneSelection.SelectedBrawler = def;
            SceneSelection.SelectedBuildPowerLevel = Mathf.Clamp(
                _previewPowerLevel,
                PlayerBrawlerProgress.MinLevel,
                PlayerBrawlerProgress.MaxLevel);
            ReleaseRuntimeSelectedBuild();
            SceneSelection.SelectedBuild = CreateSelectedBuild(def, true);

            if (SceneSelection.PickerReturnsToMainMenu)
            {
                SceneSelection.PickerReturnsToMainMenu = false;
                SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
                return;
            }

            SceneFlow.Instance?.LoadScene(SceneId.GameModeSelect);
        }

        private void OnBack()
        {
            SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
        }

        private void BuildRuntimeView()
        {
            RectTransform host = transform as RectTransform;
            Transform parent = host != null ? host : transform;

            GameObject rootObject = CreatePanel(
                "BrawlStyleBrawlerSelect",
                parent,
                _screenBackground);
            _runtimeRoot = rootObject.GetComponent<RectTransform>();
            Stretch(_runtimeRoot);
            _runtimeRoot.SetAsLastSibling();

            if (_hideLegacySceneWidgetsWhenRuntimeViewActive)
                HideLegacySceneWidgets(rootObject.transform);

            BuildHeader(_runtimeRoot);
            BuildRosterPanel(_runtimeRoot);
            BuildHeroPanel(_runtimeRoot);
            BuildStatsPanel(_runtimeRoot);
            BuildLoadoutBar(_runtimeRoot);
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
            GameObject header = CreatePanel(
                "Header",
                parent,
                new Color(0.04f, 0.13f, 0.32f, 0.98f));
            RectTransform rect = header.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0f, 0.89f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            TMP_Text title = CreateText(
                header.transform,
                "Title",
                "BRAWLERS",
                42,
                TextAlignmentOptions.Left,
                Color.white);
            Anchor(title.rectTransform, new Vector2(0.035f, 0f), new Vector2(0.45f, 1f), Vector2.zero, Vector2.zero);
            title.fontStyle = FontStyles.Bold;

            TMP_Text subtitle = CreateText(
                header.transform,
                "Subtitle",
                "SELECT YOUR FIGHTER",
                18,
                TextAlignmentOptions.Right,
                _goldColor);
            Anchor(subtitle.rectTransform, new Vector2(0.55f, 0f), new Vector2(0.965f, 1f), Vector2.zero, Vector2.zero);
            subtitle.fontStyle = FontStyles.Bold;
        }

        private void BuildRosterPanel(Transform parent)
        {
            GameObject panel = CreatePanel("RosterPanel", parent, _panelDarkColor);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.025f, 0.15f), new Vector2(0.32f, 0.86f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(12, 12, 12, 12);
            panelLayout.spacing = 10f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            TMP_Text label = CreateText(
                panel.transform,
                "RosterLabel",
                "BRAWLERS",
                20,
                TextAlignmentOptions.Left,
                _goldColor);
            label.fontStyle = FontStyles.Bold;
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 30f;

            GameObject grid = new GameObject("RosterGrid", typeof(RectTransform));
            grid.transform.SetParent(panel.transform, false);
            GridLayoutGroup gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(132f, 92f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            LayoutElement gridLayoutElement = grid.AddComponent<LayoutElement>();
            gridLayoutElement.flexibleHeight = 1f;
            gridLayoutElement.minHeight = 360f;

            _runtimeRosterContainer = grid.transform;
            _cardContainer = _runtimeRosterContainer;
        }

        private void BuildHeroPanel(Transform parent)
        {
            GameObject panel = CreatePanel("HeroPanel", parent, _panelColor);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.34f, 0.15f), new Vector2(0.67f, 0.86f), Vector2.zero, Vector2.zero);

            Image accent = CreatePanel("HeroAccent", panel.transform, _cyanColor).GetComponent<Image>();
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            Anchor(accentRect, new Vector2(0f, 0f), new Vector2(1f, 0.035f), Vector2.zero, Vector2.zero);

            _heroNameText = CreateText(
                panel.transform,
                "HeroName",
                "BRAWLER",
                38,
                TextAlignmentOptions.Center,
                Color.white);
            _heroNameText.fontStyle = FontStyles.Bold;
            Anchor(_heroNameText.rectTransform, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.96f), Vector2.zero, Vector2.zero);

            _heroRoleText = CreateText(
                panel.transform,
                "HeroRole",
                "ROLE",
                18,
                TextAlignmentOptions.Center,
                _goldColor);
            _heroRoleText.fontStyle = FontStyles.Bold;
            Anchor(_heroRoleText.rectTransform, new Vector2(0.15f, 0.75f), new Vector2(0.85f, 0.84f), Vector2.zero, Vector2.zero);

            GameObject portraitFrame = CreatePanel(
                "PortraitFrame",
                panel.transform,
                new Color(0.08f, 0.20f, 0.42f, 1f));
            RectTransform portraitFrameRect = portraitFrame.GetComponent<RectTransform>();
            Anchor(portraitFrameRect, new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.73f), Vector2.zero, Vector2.zero);

            _heroPortraitImage = CreatePanel("Portrait", portraitFrame.transform, Color.white).GetComponent<Image>();
            RectTransform portraitRect = _heroPortraitImage.GetComponent<RectTransform>();
            Anchor(portraitRect, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);
            _heroPortraitImage.preserveAspect = true;

            _heroInitialText = CreateText(
                portraitFrame.transform,
                "PortraitInitial",
                "?",
                86,
                TextAlignmentOptions.Center,
                Color.white);
            _heroInitialText.fontStyle = FontStyles.Bold;
            Anchor(_heroInitialText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _heroPowerText = CreateText(
                panel.transform,
                "PowerBadge",
                "POWER 11",
                24,
                TextAlignmentOptions.Center,
                Color.black);
            _heroPowerText.fontStyle = FontStyles.Bold;
            Image powerBg = CreatePanel("PowerBadgeBg", panel.transform, _goldColor).GetComponent<Image>();
            powerBg.transform.SetSiblingIndex(_heroPowerText.transform.GetSiblingIndex());
            Anchor(powerBg.rectTransform, new Vector2(0.31f, 0.23f), new Vector2(0.69f, 0.31f), Vector2.zero, Vector2.zero);
            Anchor(_heroPowerText.rectTransform, new Vector2(0.31f, 0.23f), new Vector2(0.69f, 0.31f), Vector2.zero, Vector2.zero);

            _heroSummaryText = CreateText(
                panel.transform,
                "HeroSummary",
                "",
                17,
                TextAlignmentOptions.Center,
                new Color(0.88f, 0.94f, 1f, 1f));
            Anchor(_heroSummaryText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);
        }

        private void BuildStatsPanel(Transform parent)
        {
            GameObject panel = CreatePanel("StatsPanel", parent, _panelDarkColor);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.69f, 0.15f), new Vector2(0.975f, 0.86f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 9f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text title = CreateText(
                panel.transform,
                "StatsTitle",
                "STATS",
                22,
                TextAlignmentOptions.Left,
                _goldColor);
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            _healthStat = CreateStatRow(panel.transform, "Health");
            _attackStat = CreateStatRow(panel.transform, "Attack");
            _superStat = CreateStatRow(panel.transform, "Super");
            _rangeStat = CreateStatRow(panel.transform, "Range");
            _speedStat = CreateStatRow(panel.transform, "Speed");
            _teamStat = CreateStatRow(panel.transform, "Teamplay");

            _attackTitleText = CreateAbilityBox(panel.transform, "Main Attack", out _attackDetailText);
            _superTitleText = CreateAbilityBox(panel.transform, "Super", out _superDetailText);
        }

        private void BuildLoadoutBar(Transform parent)
        {
            GameObject panel = CreatePanel("LoadoutPanel", parent, new Color(0.035f, 0.07f, 0.16f, 0.98f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.34f, 0.025f), new Vector2(0.975f, 0.125f), Vector2.zero, Vector2.zero);

            HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _loadoutStatusText = CreateText(
                panel.transform,
                "LoadoutStatus",
                "LOADOUT",
                16,
                TextAlignmentOptions.Center,
                _goldColor);
            _loadoutStatusText.fontStyle = FontStyles.Bold;
            LayoutElement statusLayout = _loadoutStatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 170f;
            statusLayout.flexibleWidth = 0f;

            _loadoutContainer = panel.transform;
        }

        private void BuildActionButtons(Transform parent)
        {
            _backButton = CreateButton(
                parent,
                "BackButton",
                "BACK",
                new Color(0.18f, 0.26f, 0.42f, 1f),
                null);
            Anchor(_backButton.GetComponent<RectTransform>(), new Vector2(0.025f, 0.025f), new Vector2(0.145f, 0.105f), Vector2.zero, Vector2.zero);

            _confirmButton = CreateButton(
                parent,
                "SelectButton",
                "SELECT",
                new Color(0.96f, 0.66f, 0.10f, 1f),
                null);
            Anchor(_confirmButton.GetComponent<RectTransform>(), new Vector2(0.16f, 0.025f), new Vector2(0.32f, 0.105f), Vector2.zero, Vector2.zero);
        }

        private void BuildRuntimeRosterCards()
        {
            ClearSpawnedCards();
            _rosterCards.Clear();

            if (_runtimeRosterContainer == null || _availableBrawlers == null)
                return;

            for (int i = 0; i < _availableBrawlers.Length; i++)
            {
                BrawlerDefinition def = _availableBrawlers[i];
                if (def == null)
                    continue;

                GameObject card = CreatePanel(
                    $"RosterCard_{ResolveBrawlerName(def)}",
                    _runtimeRosterContainer,
                    ResolveArchetypeColor(def.Archetype) * 0.82f);
                _spawnedCards.Add(card);

                Button button = card.AddComponent<Button>();
                button.targetGraphic = card.GetComponent<Image>();
                BrawlerDefinition captured = def;
                button.onClick.AddListener(() => OnCardClicked(captured));

                Image border = CreatePanel("SelectedBorder", card.transform, _goldColor).GetComponent<Image>();
                Anchor(border.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                border.enabled = false;

                Image portrait = CreatePanel("Portrait", card.transform, Color.white).GetComponent<Image>();
                Anchor(portrait.rectTransform, new Vector2(0.06f, 0.28f), new Vector2(0.40f, 0.90f), Vector2.zero, Vector2.zero);
                portrait.preserveAspect = true;

                TMP_Text initial = CreateText(
                    card.transform,
                    "Initial",
                    ResolveInitial(def),
                    30,
                    TextAlignmentOptions.Center,
                    Color.white);
                initial.fontStyle = FontStyles.Bold;
                Anchor(initial.rectTransform, new Vector2(0.06f, 0.28f), new Vector2(0.40f, 0.90f), Vector2.zero, Vector2.zero);

                TMP_Text name = CreateText(
                    card.transform,
                    "Name",
                    ResolveBrawlerName(def).ToUpperInvariant(),
                    16,
                    TextAlignmentOptions.Left,
                    Color.white);
                name.fontStyle = FontStyles.Bold;
                Anchor(name.rectTransform, new Vector2(0.44f, 0.48f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);

                TMP_Text role = CreateText(
                    card.transform,
                    "Role",
                    def.Archetype.ToString().ToUpperInvariant(),
                    11,
                    TextAlignmentOptions.Left,
                    new Color(0.88f, 0.94f, 1f, 1f));
                Anchor(role.rectTransform, new Vector2(0.44f, 0.22f), new Vector2(0.96f, 0.48f), Vector2.zero, Vector2.zero);

                TMP_Text power = CreateText(
                    card.transform,
                    "Power",
                    $"P{Mathf.Clamp(_previewPowerLevel, 1, 11)}",
                    13,
                    TextAlignmentOptions.Right,
                    _goldColor);
                power.fontStyle = FontStyles.Bold;
                Anchor(power.rectTransform, new Vector2(0.60f, 0.02f), new Vector2(0.95f, 0.24f), Vector2.zero, Vector2.zero);

                if (def.Portrait != null)
                {
                    portrait.sprite = def.Portrait;
                    portrait.enabled = true;
                    initial.enabled = false;
                }
                else
                {
                    portrait.enabled = false;
                    initial.enabled = true;
                }

                _rosterCards[def] = new RosterCardView(border, card.GetComponent<Image>());
            }
        }

        private void BuildLegacyCards()
        {
            ClearSpawnedCards();

            if (_cardPrefab == null || _cardContainer == null || _availableBrawlers == null)
                return;

            for (int i = 0; i < _availableBrawlers.Length; i++)
            {
                BrawlerDefinition def = _availableBrawlers[i];
                if (def == null)
                    continue;

                GameObject card = Instantiate(_cardPrefab, _cardContainer);
                _spawnedCards.Add(card);

                BrawlerCardView view = card.GetComponent<BrawlerCardView>();
                if (view == null)
                    view = card.GetComponentInChildren<BrawlerCardView>();
                if (view != null)
                {
                    view.Bind(def);
                }
                else
                {
                    TMP_Text labelTmp = card.GetComponentInChildren<TMP_Text>();
                    if (labelTmp != null)
                    {
                        labelTmp.text = ResolveBrawlerName(def);
                    }
                    else
                    {
                        Text labelLegacy = card.GetComponentInChildren<Text>();
                        if (labelLegacy != null)
                            labelLegacy.text = ResolveBrawlerName(def);
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

        private void ClearSpawnedCards()
        {
            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    Destroy(_spawnedCards[i]);
            }

            _spawnedCards.Clear();
        }

        private void RefreshRuntimePreview()
        {
            if (!_useBrawlInspiredRuntimeView)
                return;

            RefreshRosterSelection();

            if (_previewed == null)
                return;

            Color archetypeColor = ResolveArchetypeColor(_previewed.Archetype);

            if (_heroNameText != null)
                _heroNameText.text = ResolveBrawlerName(_previewed).ToUpperInvariant();
            if (_heroRoleText != null)
            {
                _heroRoleText.text = _previewed.Archetype.ToString().ToUpperInvariant();
                _heroRoleText.color = _goldColor;
            }
            if (_heroPowerText != null)
                _heroPowerText.text = $"POWER {Mathf.Clamp(_previewPowerLevel, 1, 11)}";
            if (_heroSummaryText != null)
            {
                _heroSummaryText.text =
                    $"{ResolveAbilityName(_previewed.MainAttack)} / {ResolveAbilityName(_previewed.SuperAbility)}";
            }

            if (_heroPortraitImage != null && _heroInitialText != null)
            {
                if (_previewed.Portrait != null)
                {
                    _heroPortraitImage.sprite = _previewed.Portrait;
                    _heroPortraitImage.color = Color.white;
                    _heroPortraitImage.enabled = true;
                    _heroInitialText.enabled = false;
                }
                else
                {
                    _heroPortraitImage.enabled = true;
                    _heroPortraitImage.sprite = null;
                    _heroPortraitImage.color = archetypeColor * 0.78f;
                    _heroInitialText.text = ResolveInitial(_previewed);
                    _heroInitialText.enabled = true;
                }
            }

            int powerLevel = Mathf.Clamp(
                _previewPowerLevel,
                PlayerBrawlerProgress.MinLevel,
                PlayerBrawlerProgress.MaxLevel);
            BrawlerProgressionBonus bonus = _previewed.GetProgressionBonus(powerLevel);

            float health = Mathf.Max(1f, _previewed.BaseHealth + bonus.BonusHealth);
            float moveSpeed = Mathf.Max(0f, _previewed.BaseMoveSpeed + bonus.BonusMoveSpeed);
            float mainDamage = Mathf.Max(0f, ResolveAbilityDamageTotal(_previewed.MainAttack, _previewed.BaseDamage + bonus.BonusDamage));
            float superDamage = Mathf.Max(0f, ResolveAbilityDamageTotal(_previewed.SuperAbility, 0f));
            float range = Mathf.Max(0f, ResolveAbilityRange(_previewed.MainAttack));

            SetStat(_healthStat, Mathf.RoundToInt(health).ToString(), Mathf.InverseLerp(2500f, 9000f, health));
            SetStat(_attackStat, ResolveAbilityDamageText(_previewed.MainAttack, _previewed.BaseDamage + bonus.BonusDamage), Mathf.InverseLerp(300f, 3200f, mainDamage));
            SetStat(_superStat, ResolveAbilityDamageText(_previewed.SuperAbility, 0f), Mathf.InverseLerp(0f, 4200f, superDamage));
            SetStat(_rangeStat, range.ToString("0.0"), Mathf.Clamp01(range / 12f));
            SetStat(_speedStat, moveSpeed.ToString("0.0"), Mathf.Clamp01(moveSpeed / 8f));
            SetStat(_teamStat, _previewed.TeamplayWeight.ToString("0.0"), Mathf.Clamp01(_previewed.TeamplayWeight * 0.5f));

            if (_attackTitleText != null)
                _attackTitleText.text = ResolveAbilityName(_previewed.MainAttack).ToUpperInvariant();
            if (_attackDetailText != null)
                _attackDetailText.text = ResolveAbilityDetail(_previewed.MainAttack, _previewed.BaseDamage + bonus.BonusDamage);
            if (_superTitleText != null)
                _superTitleText.text = ResolveAbilityName(_previewed.SuperAbility).ToUpperInvariant();
            if (_superDetailText != null)
                _superDetailText.text = ResolveAbilityDetail(_previewed.SuperAbility, 0f);
        }

        private void RefreshRosterSelection()
        {
            foreach (KeyValuePair<BrawlerDefinition, RosterCardView> entry in _rosterCards)
            {
                bool selected = entry.Key == _previewed;
                if (entry.Value.Border != null)
                    entry.Value.Border.enabled = selected;
                if (entry.Value.Background != null)
                    entry.Value.Background.color = selected
                        ? _cyanColor
                        : ResolveArchetypeColor(entry.Key.Archetype) * 0.82f;
            }
        }

        private void SetStat(StatRowView stat, string value, float fill)
        {
            if (stat == null)
                return;

            if (stat.ValueText != null)
                stat.ValueText.text = value;
            if (stat.FillImage != null)
                stat.FillImage.fillAmount = Mathf.Clamp01(fill);
        }

        private void EnsureFallbackLoadoutPanel()
        {
            if (!_createRuntimeLoadoutPanelWhenMissing)
                return;

            if (_loadoutContainer != null && _confirmButton != null)
                return;

            RectTransform root = transform as RectTransform;
            Transform parent = root != null ? root : transform;

            GameObject panel = CreatePanel(
                "RuntimeLoadoutPanel",
                parent,
                new Color(0.08f, 0.10f, 0.14f, 0.92f));

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Anchor(panelRect, new Vector2(0.56f, 0.08f), new Vector2(0.96f, 0.46f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            if (_loadoutStatusText == null)
            {
                _loadoutStatusText = CreateText(
                    panel.transform,
                    "LoadoutStatus",
                    "Choose loadout",
                    18,
                    TextAlignmentOptions.Center,
                    new Color(0.90f, 0.94f, 1f, 1f));

                _loadoutStatusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            }

            if (_loadoutContainer == null)
            {
                GameObject slotList = new GameObject("RuntimeLoadoutSlots", typeof(RectTransform));
                slotList.transform.SetParent(panel.transform, false);

                VerticalLayoutGroup slotLayout = slotList.AddComponent<VerticalLayoutGroup>();
                slotLayout.spacing = 6f;
                slotLayout.childControlWidth = true;
                slotLayout.childControlHeight = true;
                slotLayout.childForceExpandWidth = true;
                slotLayout.childForceExpandHeight = false;

                LayoutElement slotListLayout = slotList.AddComponent<LayoutElement>();
                slotListLayout.flexibleHeight = 1f;
                slotListLayout.minHeight = 120f;

                _loadoutContainer = slotList.transform;
            }

            if (_confirmButton == null)
            {
                _confirmButton = CreateButton(
                    panel.transform,
                    "RuntimeConfirmButton",
                    "PLAY",
                    new Color(0.16f, 0.64f, 0.86f, 1f),
                    null);
            }
        }

        private void SeedSelectedLoadout(BrawlerDefinition def)
        {
            _previewSlots.Clear();
            _selectedOptions.Clear();

            if (def == null || def.BuildLayout == null || def.BuildLayout.Slots == null)
                return;

            int powerLevel = Mathf.Clamp(
                _previewPowerLevel,
                PlayerBrawlerProgress.MinLevel,
                PlayerBrawlerProgress.MaxLevel);

            BrawlerBuildSlotDefinition[] slots = def.BuildLayout.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                BrawlerBuildSlotDefinition slot = slots[i];
                if (string.IsNullOrWhiteSpace(slot.SlotId))
                    continue;

                if (powerLevel < slot.UnlockPowerLevel)
                    continue;

                _previewSlots.Add(slot);
            }

            BrawlerBuildDefinition defaultBuild = def.GetUsableDefaultBuild(powerLevel);
            if (defaultBuild != null && defaultBuild.Selections != null)
            {
                for (int i = 0; i < defaultBuild.Selections.Length; i++)
                {
                    BrawlerBuildSlotSelection selection = defaultBuild.Selections[i];
                    if (selection.SelectedOption == null ||
                        string.IsNullOrWhiteSpace(selection.SlotId))
                    {
                        continue;
                    }

                    if (!TryGetPreviewSlot(selection.SlotId, out BrawlerBuildSlotDefinition slot))
                        continue;

                    if (IsOptionAvailableForSlot(def, slot, selection.SelectedOption))
                        _selectedOptions[selection.SlotId] = selection.SelectedOption;
                }
            }

            if (!_autoSelectFirstOptionPerSlot)
                return;

            for (int i = 0; i < _previewSlots.Count; i++)
            {
                BrawlerBuildSlotDefinition slot = _previewSlots[i];
                if (_selectedOptions.ContainsKey(slot.SlotId))
                    continue;

                List<BrawlerBuildOptionDefinition> options = BuildOptionsForSlot(def, slot);
                BrawlerBuildOptionDefinition first = FindFirstAllowedOption(slot, options);
                if (first != null)
                    _selectedOptions[slot.SlotId] = first;
            }
        }

        private void RefreshLoadoutUI()
        {
            for (int i = 0; i < _loadoutRows.Count; i++)
            {
                if (_loadoutRows[i] != null)
                    Destroy(_loadoutRows[i]);
            }

            _loadoutRows.Clear();

            if (_loadoutContainer == null)
            {
                UpdateLoadoutStatus();
                return;
            }

            if (_previewed == null)
            {
                UpdateLoadoutStatus();
                return;
            }

            if (_previewSlots.Count == 0)
            {
                GameObject row = CreateLoadoutRow("DEFAULT LOADOUT", null);
                _loadoutRows.Add(row);
                UpdateLoadoutStatus();
                return;
            }

            for (int i = 0; i < _previewSlots.Count; i++)
            {
                BrawlerBuildSlotDefinition slot = _previewSlots[i];
                _selectedOptions.TryGetValue(slot.SlotId, out BrawlerBuildOptionDefinition selected);
                string label = $"{ResolveSlotDisplayName(slot)}\n{ResolveOptionDisplayName(selected)}";

                Button button = CreateButton(
                    _loadoutContainer,
                    $"LoadoutSlot_{slot.SlotId}",
                    label,
                    ResolveSlotColor(slot.SlotType),
                    () => CycleSlot(slot));

                LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = _useBrawlInspiredRuntimeView ? 68f : 42f;
                layout.preferredWidth = _useBrawlInspiredRuntimeView ? 140f : 0f;
                layout.flexibleWidth = 1f;
                _loadoutRows.Add(button.gameObject);
            }

            UpdateLoadoutStatus();
        }

        private GameObject CreateLoadoutRow(string text, UnityAction action)
        {
            Button button = CreateButton(
                _loadoutContainer,
                "LoadoutRow",
                text,
                new Color(0.20f, 0.22f, 0.28f, 0.92f),
                action);
            button.interactable = action != null;

            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = _useBrawlInspiredRuntimeView ? 68f : 42f;
            layout.flexibleWidth = 1f;
            return button.gameObject;
        }

        private void CycleSlot(BrawlerBuildSlotDefinition slot)
        {
            if (_previewed == null)
                return;

            List<BrawlerBuildOptionDefinition> options = BuildOptionsForSlot(_previewed, slot);
            if (options.Count == 0)
                return;

            _selectedOptions.TryGetValue(slot.SlotId, out BrawlerBuildOptionDefinition current);
            int startIndex = Mathf.Max(-1, options.IndexOf(current));

            for (int step = 1; step <= options.Count; step++)
            {
                int index = (startIndex + step) % options.Count;
                BrawlerBuildOptionDefinition candidate = options[index];
                if (candidate == null)
                    continue;

                if (WouldViolateDuplicateRule(slot, candidate))
                    continue;

                _selectedOptions[slot.SlotId] = candidate;
                RefreshLoadoutUI();
                RefreshRuntimePreview();
                UpdateConfirmButtonInteractable();
                return;
            }
        }

        private BrawlerBuildOptionDefinition FindFirstAllowedOption(
            BrawlerBuildSlotDefinition slot,
            List<BrawlerBuildOptionDefinition> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                BrawlerBuildOptionDefinition option = options[i];
                if (option != null && !WouldViolateDuplicateRule(slot, option))
                    return option;
            }

            return null;
        }

        private bool WouldViolateDuplicateRule(
            BrawlerBuildSlotDefinition slot,
            BrawlerBuildOptionDefinition candidate)
        {
            if (slot.AllowDuplicateSelectionInSameTypeGroup || candidate == null)
                return false;

            for (int i = 0; i < _previewSlots.Count; i++)
            {
                BrawlerBuildSlotDefinition otherSlot = _previewSlots[i];
                if (otherSlot.SlotId == slot.SlotId)
                    continue;

                if (!_selectedOptions.TryGetValue(otherSlot.SlotId, out BrawlerBuildOptionDefinition selected))
                    continue;

                if (selected == candidate)
                    return true;
            }

            return false;
        }

        private bool TryGetPreviewSlot(string slotId, out BrawlerBuildSlotDefinition slot)
        {
            for (int i = 0; i < _previewSlots.Count; i++)
            {
                if (_previewSlots[i].SlotId == slotId)
                {
                    slot = _previewSlots[i];
                    return true;
                }
            }

            slot = default;
            return false;
        }

        private bool IsOptionAvailableForSlot(
            BrawlerDefinition def,
            BrawlerBuildSlotDefinition slot,
            BrawlerBuildOptionDefinition option)
        {
            if (def == null || option == null || !option.CanEquipInBuildSlot(slot.SlotType))
                return false;

            List<BrawlerBuildOptionDefinition> options = BuildOptionsForSlot(def, slot);
            return options.Contains(option);
        }

        private List<BrawlerBuildOptionDefinition> BuildOptionsForSlot(
            BrawlerDefinition def,
            BrawlerBuildSlotDefinition slot)
        {
            List<BrawlerBuildOptionDefinition> options =
                new List<BrawlerBuildOptionDefinition>(4);

            if (def == null)
                return options;

            switch (slot.SlotType)
            {
                case BrawlerBuildSlotType.Gadget:
                    AddOptions(options, def.GadgetOptions);
                    AddOption(options, def.Gadget);
                    break;

                case BrawlerBuildSlotType.StarPower:
                    AddOptions(options, def.StarPowerOptions);
                    AddOption(options, def.StarPower);
                    break;

                case BrawlerBuildSlotType.Hypercharge:
                    AddOptions(options, def.HyperchargeOptions);
                    AddOption(options, def.Hypercharge);
                    break;

                case BrawlerBuildSlotType.Gear:
                    AddOptions(options, def.BuildAvailableGearOptions());
                    break;
            }

            return options;
        }

        private static void AddOptions<T>(
            List<BrawlerBuildOptionDefinition> target,
            T[] options) where T : BrawlerBuildOptionDefinition
        {
            if (target == null || options == null)
                return;

            for (int i = 0; i < options.Length; i++)
                AddOption(target, options[i]);
        }

        private static void AddOptions<T>(
            List<BrawlerBuildOptionDefinition> target,
            List<T> options) where T : BrawlerBuildOptionDefinition
        {
            if (target == null || options == null)
                return;

            for (int i = 0; i < options.Count; i++)
                AddOption(target, options[i]);
        }

        private static void AddOption(
            List<BrawlerBuildOptionDefinition> target,
            BrawlerBuildOptionDefinition option)
        {
            if (target == null || option == null || target.Contains(option))
                return;

            target.Add(option);
        }

        private void UpdateLoadoutStatus()
        {
            if (_loadoutStatusText == null)
                return;

            if (_previewed == null)
            {
                _loadoutStatusText.text = "CHOOSE BRAWLER";
                return;
            }

            BrawlerBuildValidationResult validation = ValidateCurrentLoadout();
            _loadoutStatusText.text = validation.IsValid
                ? ResolveReadyStatusText(_previewed, _previewSlots.Count)
                : validation.Message;
        }

        private void UpdateConfirmButtonInteractable()
        {
            if (_confirmButton != null)
                _confirmButton.interactable = _previewed != null && IsCurrentLoadoutValid();
        }

        private bool IsCurrentLoadoutValid()
        {
            if (_previewed == null)
                return false;

            return ValidateCurrentLoadout().IsValid;
        }

        private BrawlerBuildValidationResult ValidateCurrentLoadout()
        {
            if (_previewed == null)
                return BrawlerBuildValidationResult.Invalid("No brawler selected.");

            if (_previewSlots.Count == 0)
                return BrawlerBuildValidationResult.Valid();

            BrawlerBuildDefinition build = CreateSelectedBuild(_previewed, false);
            if (build == null)
                return BrawlerBuildValidationResult.Valid();

            BrawlerBuildValidationResult validation = BrawlerBuildValidator.Validate(
                _previewed,
                build,
                Mathf.Clamp(_previewPowerLevel, PlayerBrawlerProgress.MinLevel, PlayerBrawlerProgress.MaxLevel));

            DestroyRuntimeBuild(build);
            return validation;
        }

        private BrawlerBuildDefinition CreateSelectedBuild(
            BrawlerDefinition def,
            bool keepAlive)
        {
            if (def == null || _previewSlots.Count == 0)
                return null;

            BrawlerBuildDefinition build = ScriptableObject.CreateInstance<BrawlerBuildDefinition>();
            build.name = $"{def.name}_RuntimeSelectedBuild";
            build.hideFlags = HideFlags.DontSave;

            List<BrawlerBuildSlotSelection> selections =
                new List<BrawlerBuildSlotSelection>(_previewSlots.Count);

            for (int i = 0; i < _previewSlots.Count; i++)
            {
                BrawlerBuildSlotDefinition slot = _previewSlots[i];
                if (!_selectedOptions.TryGetValue(slot.SlotId, out BrawlerBuildOptionDefinition option))
                    continue;

                selections.Add(new BrawlerBuildSlotSelection
                {
                    SlotId = slot.SlotId,
                    SelectedOption = option
                });
            }

            build.Selections = selections.ToArray();
            return build;
        }

        private static void ReleaseRuntimeSelectedBuild()
        {
            DestroyRuntimeBuild(SceneSelection.SelectedBuild);
            SceneSelection.SelectedBuild = null;
        }

        private static void DestroyRuntimeBuild(BrawlerBuildDefinition build)
        {
            if (build == null || (build.hideFlags & HideFlags.DontSave) == 0)
                return;

            if (Application.isPlaying)
                Object.Destroy(build);
            else
                Object.DestroyImmediate(build);
        }

        private StatRowView CreateStatRow(Transform parent, string label)
        {
            GameObject row = CreatePanel("Stat_" + label, parent, new Color(0.08f, 0.13f, 0.26f, 0.86f));
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 34f;

            TMP_Text labelText = CreateText(row.transform, "Label", label.ToUpperInvariant(), 12, TextAlignmentOptions.Left, Color.white);
            labelText.fontStyle = FontStyles.Bold;
            Anchor(labelText.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.31f, 1f), Vector2.zero, Vector2.zero);

            GameObject barBack = CreatePanel("BarBack", row.transform, new Color(0.02f, 0.04f, 0.08f, 0.9f));
            Anchor(barBack.GetComponent<RectTransform>(), new Vector2(0.33f, 0.28f), new Vector2(0.73f, 0.72f), Vector2.zero, Vector2.zero);

            Image fill = CreatePanel("Fill", barBack.transform, _goldColor).GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform);

            TMP_Text value = CreateText(row.transform, "Value", "-", 13, TextAlignmentOptions.Right, _goldColor);
            value.fontStyle = FontStyles.Bold;
            Anchor(value.rectTransform, new Vector2(0.75f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);

            return new StatRowView(value, fill);
        }

        private TMP_Text CreateAbilityBox(
            Transform parent,
            string label,
            out TMP_Text detailText)
        {
            GameObject box = CreatePanel("Ability_" + label, parent, new Color(0.08f, 0.13f, 0.26f, 0.90f));
            LayoutElement boxLayout = box.AddComponent<LayoutElement>();
            boxLayout.preferredHeight = 68f;

            TMP_Text title = CreateText(box.transform, "Title", label.ToUpperInvariant(), 15, TextAlignmentOptions.Left, _goldColor);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);

            detailText = CreateText(box.transform, "Detail", "", 12, TextAlignmentOptions.Left, new Color(0.88f, 0.94f, 1f, 1f));
            Anchor(detailText.rectTransform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.52f), Vector2.zero, Vector2.zero);

            return title;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return go;
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

            TMP_Text text = CreateText(
                go.transform,
                "Label",
                label,
                16,
                TextAlignmentOptions.Center,
                Color.white);
            text.fontStyle = FontStyles.Bold;

            RectTransform textRect = text.GetComponent<RectTransform>();
            Anchor(textRect, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));

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
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.enableWordWrapping = true;

            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string ResolveSlotDisplayName(BrawlerBuildSlotDefinition slot)
        {
            if (!string.IsNullOrWhiteSpace(slot.DisplayName))
                return slot.DisplayName.ToUpperInvariant();

            return slot.SlotType.ToString().ToUpperInvariant();
        }

        private static string ResolveOptionDisplayName(BrawlerBuildOptionDefinition option)
        {
            if (option == null)
                return "NONE";

            if (!string.IsNullOrWhiteSpace(option.OptionName))
                return option.OptionName.ToUpperInvariant();

            return option.name.ToUpperInvariant();
        }

        private static string ResolveBrawlerName(BrawlerDefinition brawler)
        {
            if (brawler == null)
                return "Brawler";

            return !string.IsNullOrWhiteSpace(brawler.BrawlerName)
                ? brawler.BrawlerName
                : brawler.name;
        }

        private static string ResolveInitial(BrawlerDefinition brawler)
        {
            string name = ResolveBrawlerName(brawler);
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        private static string ResolveReadyStatusText(BrawlerDefinition brawler, int slotCount)
        {
            string name = ResolveBrawlerName(brawler).ToUpperInvariant();
            return slotCount > 0
                ? $"{name} LOADOUT READY"
                : $"{name} DEFAULT LOADOUT";
        }

        private static string ResolveAbilityName(AbilityDefinition ability)
        {
            if (ability == null)
                return "-";

            return !string.IsNullOrWhiteSpace(ability.AbilityName)
                ? ability.AbilityName
                : ability.name;
        }

        private static string ResolveAbilityDetail(AbilityDefinition ability, float fallbackDamage)
        {
            if (ability == null)
                return "No ability equipped";

            string damage = ResolveAbilityDamageText(ability, fallbackDamage);
            float range = ResolveAbilityRange(ability);
            string cooldown = ability.Cooldown > 0f ? $"{ability.Cooldown:0.0}s" : "Ready";
            return $"DMG {damage}   RNG {range:0.0}   CD {cooldown}";
        }

        private static string ResolveAbilityDamageText(AbilityDefinition ability, float fallbackDamage)
        {
            if (ability == null)
                return "-";

            if (ability is ProjectileAbilityDefinition projectile)
                return projectile.ProjectileCount > 1
                    ? $"{Mathf.RoundToInt(projectile.Damage)} x {projectile.ProjectileCount}"
                    : Mathf.RoundToInt(projectile.Damage).ToString();

            if (ability is BasicProjectileAttackDefinition basic)
                return Mathf.RoundToInt(basic.Damage).ToString();

            if (ability is VolleyProjectileAbilityDefinition volley)
                return volley.ProjectileCount > 1
                    ? $"{Mathf.RoundToInt(volley.Damage)} x {volley.ProjectileCount}"
                    : Mathf.RoundToInt(volley.Damage).ToString();

            if (ability is ChainProjectileAbilityDefinition chain)
                return Mathf.RoundToInt(chain.Damage).ToString();

            if (ability is AoEAbilityDefinition aoe)
                return Mathf.RoundToInt(aoe.Damage).ToString();

            if (ability is BasicSuperDefinition super)
                return Mathf.RoundToInt(super.Damage).ToString();

            if (ability is ThrownVolleyAoEAbilityDefinition thrownVolley)
                return thrownVolley.ProjectileCount > 1
                    ? $"{Mathf.RoundToInt(thrownVolley.EnemyDamage)} x {thrownVolley.ProjectileCount}"
                    : Mathf.RoundToInt(thrownVolley.EnemyDamage).ToString();

            if (ability is ThrownHybridAoEAbilityDefinition thrownHybrid)
                return Mathf.RoundToInt(thrownHybrid.EnemyDamage).ToString();

            if (ability is HybridAoEAbilityDefinition hybrid)
                return Mathf.RoundToInt(hybrid.EnemyDamage).ToString();

            return fallbackDamage > 0f
                ? Mathf.RoundToInt(fallbackDamage).ToString()
                : "-";
        }

        private static float ResolveAbilityDamageTotal(AbilityDefinition ability, float fallbackDamage)
        {
            if (ability == null)
                return fallbackDamage;

            if (ability is ProjectileAbilityDefinition projectile)
                return projectile.Damage * Mathf.Max(1, projectile.ProjectileCount);

            if (ability is BasicProjectileAttackDefinition basic)
                return basic.Damage;

            if (ability is VolleyProjectileAbilityDefinition volley)
                return volley.Damage * Mathf.Max(1, volley.ProjectileCount);

            if (ability is ChainProjectileAbilityDefinition chain)
                return chain.Damage;

            if (ability is AoEAbilityDefinition aoe)
                return aoe.Damage;

            if (ability is BasicSuperDefinition super)
                return super.Damage;

            if (ability is ThrownVolleyAoEAbilityDefinition thrownVolley)
                return thrownVolley.EnemyDamage * Mathf.Max(1, thrownVolley.ProjectileCount);

            if (ability is ThrownHybridAoEAbilityDefinition thrownHybrid)
                return thrownHybrid.EnemyDamage;

            if (ability is HybridAoEAbilityDefinition hybrid)
                return hybrid.EnemyDamage;

            return fallbackDamage;
        }

        private static float ResolveAbilityRange(AbilityDefinition ability)
        {
            if (ability == null)
                return 0f;

            return Mathf.Max(0f, ability.GetAIMaxRange());
        }

        private Color ResolveSlotColor(BrawlerBuildSlotType slotType)
        {
            switch (slotType)
            {
                case BrawlerBuildSlotType.Gadget:
                    return new Color(0.08f, 0.62f, 0.36f, 1f);
                case BrawlerBuildSlotType.StarPower:
                    return new Color(0.96f, 0.62f, 0.08f, 1f);
                case BrawlerBuildSlotType.Hypercharge:
                    return new Color(0.56f, 0.18f, 0.86f, 1f);
                case BrawlerBuildSlotType.Gear:
                    return new Color(0.15f, 0.42f, 0.78f, 1f);
                default:
                    return new Color(0.20f, 0.24f, 0.32f, 1f);
            }
        }

        private static Color ResolveArchetypeColor(BrawlerArchetype archetype)
        {
            switch (archetype)
            {
                case BrawlerArchetype.Tank:
                    return new Color(0.92f, 0.24f, 0.22f, 1f);
                case BrawlerArchetype.Assassin:
                    return new Color(0.70f, 0.22f, 0.92f, 1f);
                case BrawlerArchetype.Sniper:
                    return new Color(0.18f, 0.62f, 0.98f, 1f);
                case BrawlerArchetype.Support:
                    return new Color(0.18f, 0.78f, 0.36f, 1f);
                case BrawlerArchetype.Fighter:
                    return new Color(0.96f, 0.54f, 0.12f, 1f);
                case BrawlerArchetype.Controller:
                    return new Color(0.28f, 0.72f, 0.86f, 1f);
                case BrawlerArchetype.Artillery:
                    return new Color(0.90f, 0.34f, 0.12f, 1f);
                default:
                    return new Color(0.45f, 0.55f, 0.70f, 1f);
            }
        }

        private sealed class RosterCardView
        {
            public readonly Image Border;
            public readonly Image Background;

            public RosterCardView(Image border, Image background)
            {
                Border = border;
                Background = background;
            }
        }

        private sealed class StatRowView
        {
            public readonly TMP_Text ValueText;
            public readonly Image FillImage;

            public StatRowView(TMP_Text valueText, Image fillImage)
            {
                ValueText = valueText;
                FillImage = fillImage;
            }
        }
    }
}
