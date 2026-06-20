using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
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
    /// Backward-compatible: if the confirm/loadout UI is unwired, the screen
    /// creates a compact runtime panel so simple/minimal scenes still work.
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

        [Header("Loadout preview")]
        [Tooltip("Container for generated gadget/star power/gear/hypercharge slot buttons. If empty, the screen creates a compact runtime panel.")]
        [SerializeField] private Transform _loadoutContainer;
        [Tooltip("Optional status label for loadout validation feedback.")]
        [SerializeField] private TMP_Text _loadoutStatusText;
        [Tooltip("Power level used when previewing build slots. Prototype defaults to 11 so every slot can be tested.")]
        [Range(1, 11)]
        [SerializeField] private int _previewPowerLevel = PlayerBrawlerProgress.MaxLevel;
        [SerializeField] private bool _createRuntimeLoadoutPanelWhenMissing = true;
        [SerializeField] private bool _autoSelectFirstOptionPerSlot = true;

        [Header("Temporary Flow")]
        [SerializeField]
        private bool _commitImmediatelyOnCardClick = false;

        private BrawlerDefinition _previewed;
        private readonly List<GameObject> _spawnedCards = new List<GameObject>(8);
        private readonly List<GameObject> _loadoutRows = new List<GameObject>(8);
        private readonly List<BrawlerBuildSlotDefinition> _previewSlots =
            new List<BrawlerBuildSlotDefinition>(8);
        private readonly Dictionary<string, BrawlerBuildOptionDefinition> _selectedOptions =
            new Dictionary<string, BrawlerBuildOptionDefinition>(8);
        private Button _runtimeConfirmButton;

        private void Start()
        {
            EnsureRuntimeLoadoutPanel();
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

        private void OnCardClicked(BrawlerDefinition def)
        {
            SetPreview(def);

            if (_commitImmediatelyOnCardClick)
                Commit(def);
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirm);
        }

        private void BuildCards()
        {
            if (_cardPrefab == null || _cardContainer == null || _availableBrawlers == null) return;

            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                if (_spawnedCards[i] != null)
                    Destroy(_spawnedCards[i]);
            }

            _spawnedCards.Clear();

            for (int i = 0; i < _availableBrawlers.Length; i++)
            {
                BrawlerDefinition def = _availableBrawlers[i];
                if (def == null) continue;

                GameObject card = Instantiate(_cardPrefab, _cardContainer);
                _spawnedCards.Add(card);

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

        private void SetPreview(BrawlerDefinition def)
        {
            _previewed = def;
            if (_detailPanel != null) _detailPanel.Bind(def);
            SeedSelectedLoadout(def);
            RefreshLoadoutUI();
            UpdateConfirmButtonInteractable();
        }

        private void OnConfirm()
        {
            if (_previewed != null) Commit(_previewed);
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
            if (_confirmButton != null)
                _confirmButton.interactable = _previewed != null && IsCurrentLoadoutValid();
        }

        private void OnBack() => SceneFlow.Instance?.LoadScene(SceneId.MainMenu);

        private void EnsureRuntimeLoadoutPanel()
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
            panelRect.anchorMin = new Vector2(0.56f, 0.08f);
            panelRect.anchorMax = new Vector2(0.96f, 0.46f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

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

                LayoutElement statusLayout = _loadoutStatusText.gameObject.AddComponent<LayoutElement>();
                statusLayout.preferredHeight = 34f;
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
                _runtimeConfirmButton = CreateButton(
                    panel.transform,
                    "RuntimeConfirmButton",
                    "PLAY",
                    new Color(0.16f, 0.64f, 0.86f, 1f),
                    OnConfirm);
                _confirmButton = _runtimeConfirmButton;
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
                GameObject row = CreateLoadoutRow("Default loadout", null);
                _loadoutRows.Add(row);
                UpdateLoadoutStatus();
                return;
            }

            for (int i = 0; i < _previewSlots.Count; i++)
            {
                BrawlerBuildSlotDefinition slot = _previewSlots[i];
                _selectedOptions.TryGetValue(slot.SlotId, out BrawlerBuildOptionDefinition selected);
                string label = $"{ResolveSlotDisplayName(slot)}: {ResolveOptionDisplayName(selected)}";

                Button button = CreateButton(
                    _loadoutContainer,
                    $"LoadoutSlot_{slot.SlotId}",
                    label,
                    ResolveSlotColor(slot.SlotType),
                    () => CycleSlot(slot));

                LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 42f;
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
                _loadoutStatusText.text = "Choose brawler";
                return;
            }

            BrawlerBuildValidationResult validation = ValidateCurrentLoadout();
            _loadoutStatusText.text = validation.IsValid
                ? ResolveReadyStatusText(_previewed, _previewSlots.Count)
                : validation.Message;
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
            build.name = def != null
                ? $"{def.name}_RuntimeSelectedBuild"
                : "RuntimeSelectedBuild";
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

            if (!keepAlive)
                return build;

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
                UnityEngine.Object.Destroy(build);
            else
                UnityEngine.Object.DestroyImmediate(build);
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

            if (onClick != null)
                button.onClick.AddListener(onClick);

            TMP_Text text = CreateText(
                go.transform,
                "Label",
                label,
                16,
                TextAlignmentOptions.Center,
                Color.white);

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

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

            return label;
        }

        private static string ResolveSlotDisplayName(BrawlerBuildSlotDefinition slot)
        {
            if (!string.IsNullOrWhiteSpace(slot.DisplayName))
                return slot.DisplayName;

            return slot.SlotType.ToString();
        }

        private static string ResolveOptionDisplayName(BrawlerBuildOptionDefinition option)
        {
            if (option == null)
                return "None";

            if (!string.IsNullOrWhiteSpace(option.OptionName))
                return option.OptionName;

            return option.name;
        }

        private static string ResolveBrawlerName(BrawlerDefinition brawler)
        {
            if (brawler == null)
                return "Brawler";

            return !string.IsNullOrWhiteSpace(brawler.BrawlerName)
                ? brawler.BrawlerName
                : brawler.name;
        }

        private static string ResolveReadyStatusText(BrawlerDefinition brawler, int slotCount)
        {
            string name = ResolveBrawlerName(brawler);
            return slotCount > 0
                ? $"{name} loadout ready"
                : $"{name} default loadout";
        }

        private static Color ResolveSlotColor(BrawlerBuildSlotType slotType)
        {
            switch (slotType)
            {
                case BrawlerBuildSlotType.Gadget:
                    return new Color(0.16f, 0.54f, 0.38f, 0.96f);
                case BrawlerBuildSlotType.StarPower:
                    return new Color(0.74f, 0.50f, 0.12f, 0.96f);
                case BrawlerBuildSlotType.Hypercharge:
                    return new Color(0.50f, 0.18f, 0.74f, 0.96f);
                case BrawlerBuildSlotType.Gear:
                    return new Color(0.20f, 0.34f, 0.62f, 0.96f);
                default:
                    return new Color(0.22f, 0.24f, 0.30f, 0.96f);
            }
        }
    }
}
