using System.Collections;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public sealed class NanopowerSelectionController : MonoBehaviour
    {
        private const string MatchSceneName = "Match";
        private const int CanvasSortingOrder = 112;
        private const float SelectionDurationSeconds = 5f;
        private const float PlayerSpawnWaitSeconds = 1.25f;

        private static Font _runtimeFont;

        private readonly List<NanopowerDefinition> _optionsBuffer = new List<NanopowerDefinition>(4);
        private readonly List<NanopowerDefinition> _botOptionsBuffer = new List<NanopowerDefinition>(4);
        private readonly NanopowerDefinition[] _offers = new NanopowerDefinition[2];

        private readonly GameObject[] _offerCards = new GameObject[2];
        private readonly Image[] _offerBackgrounds = new Image[2];
        private readonly Image[] _offerAccents = new Image[2];
        private readonly GameObject[] _offerSelectionFrames = new GameObject[2];
        private readonly Text[] _offerNames = new Text[2];
        private readonly Text[] _offerDescriptions = new Text[2];

        private MatchManager _subscribedMatchManager;
        private GameObject _root;
        private Text _timerText;
        private BrawlerController _localPlayer;
        private Coroutine _selectionRoutine;
        private bool _hasHandledMatchStart;
        private bool _selectionActive;
        private int _selectedOfferIndex = -1;
        private float _selectionEndsAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene);
        }

        private static void TryInstallForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MatchSceneName)
                return;

            if (FindObjectOfType<NanopowerSelectionController>() != null)
                return;

            GameObject host = new GameObject("NanopowerSelectionController");
            host.AddComponent<NanopowerSelectionController>();
        }

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            TryBindMatchManager();

            if (_subscribedMatchManager != null &&
                _subscribedMatchManager.CurrentState == MatchState.CountingDown)
            {
                BeginSelectionPhase();
            }
        }

        private void OnDestroy()
        {
            if (_subscribedMatchManager != null)
                _subscribedMatchManager.OnStateChanged -= HandleMatchStateChanged;
        }

        private void Update()
        {
            TryBindMatchManager();

            if (!_hasHandledMatchStart &&
                _subscribedMatchManager != null &&
                _subscribedMatchManager.CurrentState == MatchState.CountingDown)
            {
                BeginSelectionPhase();
            }

            if (!_selectionActive)
                return;

            HandleSelectionKeys();
            RefreshTimer();

            if (Time.time >= _selectionEndsAt)
                FinalizeSelection();
        }

        private void TryBindMatchManager()
        {
            MatchManager current = MatchManager.Instance;
            if (_subscribedMatchManager == current)
                return;

            if (_subscribedMatchManager != null)
                _subscribedMatchManager.OnStateChanged -= HandleMatchStateChanged;

            _subscribedMatchManager = current;

            if (_subscribedMatchManager != null)
                _subscribedMatchManager.OnStateChanged += HandleMatchStateChanged;
        }

        private void HandleMatchStateChanged(MatchState state)
        {
            if (state == MatchState.CountingDown)
            {
                BeginSelectionPhase();
                return;
            }

            if (state == MatchState.Active && _selectionActive)
            {
                FinalizeSelection();
                return;
            }

            if (state == MatchState.Ended)
                HideSelection();
        }

        private void BeginSelectionPhase()
        {
            if (_hasHandledMatchStart)
                return;

            _hasHandledMatchStart = true;

            if (_selectionRoutine != null)
                StopCoroutine(_selectionRoutine);

            _selectionRoutine = StartCoroutine(BeginSelectionWhenReady());
        }

        private IEnumerator BeginSelectionWhenReady()
        {
            float deadline = Time.time + PlayerSpawnWaitSeconds;

            while (_localPlayer == null && Time.time < deadline)
            {
                _localPlayer = FindLocalPlayer();

                if (_localPlayer != null &&
                    _localPlayer.Definition != null &&
                    _localPlayer.State != null)
                {
                    break;
                }

                yield return null;
            }

            ApplyAutomaticNanopowersToBots();

            if (_localPlayer == null ||
                _localPlayer.Definition == null ||
                _localPlayer.State == null ||
                _localPlayer.ActiveNanopower != null)
            {
                yield break;
            }

            NanopowerCatalog.BuildOptions(_localPlayer.Definition, _optionsBuffer);
            if (_optionsBuffer.Count == 0)
                yield break;

            MatchManager matchManager = MatchManager.Instance;
            if (matchManager != null && matchManager.CountdownDuration < SelectionDurationSeconds)
                matchManager.ExtendCurrentCountdownTo(SelectionDurationSeconds);

            PickOffers(_optionsBuffer);
            ShowSelection();
        }

        private static BrawlerController FindLocalPlayer()
        {
            BrawlerController[] brawlers = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < brawlers.Length; i++)
            {
                BrawlerController brawler = brawlers[i];
                if (brawler != null && brawler.GetComponent<PlayerCommandSource>() != null)
                    return brawler;
            }

            return null;
        }

        private void ApplyAutomaticNanopowersToBots()
        {
            BrawlerController[] brawlers = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < brawlers.Length; i++)
            {
                BrawlerController brawler = brawlers[i];
                if (brawler == null ||
                    brawler.State == null ||
                    brawler.Definition == null ||
                    brawler.ActiveNanopower != null ||
                    brawler.GetComponent<PlayerCommandSource>() != null)
                {
                    continue;
                }

                NanopowerCatalog.BuildOptions(brawler.Definition, _botOptionsBuffer);
                if (_botOptionsBuffer.Count == 0)
                    continue;

                int selectedIndex = UnityEngine.Random.Range(0, _botOptionsBuffer.Count);
                brawler.SetActiveNanopower(_botOptionsBuffer[selectedIndex], true);
            }
        }

        private void PickOffers(List<NanopowerDefinition> options)
        {
            _offers[0] = null;
            _offers[1] = null;

            if (options == null || options.Count == 0)
                return;

            int first = UnityEngine.Random.Range(0, options.Count);
            _offers[0] = options[first];

            if (options.Count == 1)
                return;

            int second = first;
            while (second == first)
                second = UnityEngine.Random.Range(0, options.Count);

            _offers[1] = options[second];
        }

        private void ShowSelection()
        {
            for (int i = 0; i < _offers.Length; i++)
            {
                NanopowerDefinition offer = _offers[i];
                bool hasOffer = offer != null;

                if (_offerCards[i] != null)
                    _offerCards[i].SetActive(hasOffer);

                if (!hasOffer)
                    continue;

                Color accent = offer.AccentColor;
                if (_offerAccents[i] != null)
                    _offerAccents[i].color = accent;

                if (_offerBackgrounds[i] != null)
                {
                    _offerBackgrounds[i].color = new Color(
                        Mathf.Lerp(0.04f, accent.r, 0.16f),
                        Mathf.Lerp(0.05f, accent.g, 0.16f),
                        Mathf.Lerp(0.06f, accent.b, 0.16f),
                        0.94f);
                }

                if (_offerNames[i] != null)
                    _offerNames[i].text = offer.DisplayName;

                if (_offerDescriptions[i] != null)
                    _offerDescriptions[i].text = offer.DisplayDescription;
            }

            _selectionEndsAt = Time.time + ResolveSelectionWindow();
            _selectionActive = true;
            _selectedOfferIndex = -1;
            RefreshSelectedOfferFrames();
            RefreshTimer();

            if (_root != null)
                _root.SetActive(true);
        }

        private float ResolveSelectionWindow()
        {
            MatchManager matchManager = MatchManager.Instance;
            if (matchManager != null && matchManager.CurrentState == MatchState.CountingDown)
                return Mathf.Max(0.15f, matchManager.CountdownRemainingSeconds);

            return SelectionDurationSeconds;
        }

        private void SelectOffer(int index)
        {
            if (!_selectionActive)
                return;

            if (index < 0 || index >= _offers.Length || _offers[index] == null)
                index = 0;

            NanopowerDefinition selected = _offers[index] != null ? _offers[index] : _offers[0];
            if (selected != null && _localPlayer != null)
            {
                _selectedOfferIndex = index;
                _localPlayer.SetActiveNanopower(selected, true);
                RefreshSelectedOfferFrames();
            }
        }

        private void FinalizeSelection()
        {
            if (!_selectionActive)
                return;

            if (_selectedOfferIndex < 0)
                SelectOffer(0);

            HideSelection();
        }

        private void HideSelection()
        {
            _selectionActive = false;

            if (_root != null)
                _root.SetActive(false);
        }

        private void RefreshTimer()
        {
            if (_timerText == null)
                return;

            float remaining = Mathf.Max(0f, _selectionEndsAt - Time.time);
            _timerText.text = remaining.ToString("0.0");
        }

        private void RefreshSelectedOfferFrames()
        {
            for (int i = 0; i < _offerSelectionFrames.Length; i++)
            {
                if (_offerSelectionFrames[i] != null)
                    _offerSelectionFrames[i].SetActive(i == _selectedOfferIndex);
            }
        }

        private void HandleSelectionKeys()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                SelectOffer(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SelectOffer(1);
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                SelectOffer(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                SelectOffer(1);
#endif
        }

        private void BuildUI()
        {
            GameObject canvasGo = new GameObject(
                "NanopowerSelectionCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = CreatePanel(
                canvasGo.transform,
                "NanopowerSelectionRoot",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0f, 0f, 0f, 0.34f));

            GameObject panel = CreatePanel(
                _root.transform,
                "NanopowerPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -42f),
                new Vector2(840f, 330f),
                new Color(0.035f, 0.04f, 0.052f, 0.94f));

            CreateText(
                panel.transform,
                "NanopowerTitle",
                "SELECT NANOPOWER",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-46f, -28f),
                new Vector2(420f, 44f),
                28,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            _timerText = CreateText(
                panel.transform,
                "NanopowerTimer",
                "3.0",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -30f),
                new Vector2(90f, 40f),
                28,
                TextAnchor.MiddleRight,
                new Color(1f, 0.88f, 0.32f, 1f),
                FontStyle.Bold);

            CreateOffer(panel.transform, 0, new Vector2(-206f, -34f), "1");
            CreateOffer(panel.transform, 1, new Vector2(206f, -34f), "2");

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                SetLayerRecursively(canvasGo, uiLayer);

            EnsureEventSystem();
            _root.SetActive(false);
        }

        private void CreateOffer(Transform parent, int index, Vector2 anchoredPosition, string keyLabel)
        {
            GameObject card = CreatePanel(
                parent,
                $"NanopowerOffer{index + 1}",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                new Vector2(360f, 196f),
                new Color(0.08f, 0.09f, 0.11f, 0.95f));

            Image background = card.GetComponent<Image>();
            Button button = card.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            int capturedIndex = index;
            button.onClick.AddListener(() => SelectOffer(capturedIndex));

            _offerCards[index] = card;
            _offerBackgrounds[index] = background;

            _offerAccents[index] = CreateImage(
                card.transform,
                "Accent",
                Vector2.zero,
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(10f, 0f),
                new Color(0.15f, 0.75f, 1f, 1f));

            GameObject badge = CreatePanel(
                card.transform,
                "KeyBadge",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(26f, -24f),
                new Vector2(42f, 42f),
                new Color(1f, 1f, 1f, 0.16f));

            CreateText(
                badge.transform,
                "KeyLabel",
                keyLabel,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                22,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            _offerNames[index] = CreateText(
                card.transform,
                "Name",
                string.Empty,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(82f, -18f),
                new Vector2(250f, 48f),
                25,
                TextAnchor.MiddleLeft,
                Color.white,
                FontStyle.Bold);

            _offerDescriptions[index] = CreateText(
                card.transform,
                "Description",
                string.Empty,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -86f),
                new Vector2(304f, 86f),
                18,
                TextAnchor.UpperLeft,
                new Color(1f, 1f, 1f, 0.82f),
                FontStyle.Normal);

            _offerSelectionFrames[index] = CreateSelectionFrame(card.transform);
        }

        private static GameObject CreateSelectionFrame(Transform parent)
        {
            GameObject root = new GameObject("SelectedOutline", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Color outlineColor = new Color(1f, 0.86f, 0.18f, 1f);
            CreateFrameBar(root.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), Vector2.zero, outlineColor);
            CreateFrameBar(root.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 5f), outlineColor);
            CreateFrameBar(root.transform, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f), outlineColor);
            CreateFrameBar(root.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-5f, 0f), Vector2.zero, outlineColor);

            root.SetActive(false);
            return root;
        }

        private static void CreateFrameBar(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            GameObject bar = CreatePanel(
                parent,
                name,
                anchorMin,
                anchorMax,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                color);

            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = bar.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.type = Image.Type.Simple;
            image.color = color;

            return go;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject go = CreatePanel(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size, color);
            return go.GetComponent<Image>();
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle fontStyle)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text uiText = go.GetComponent<Text>();
            uiText.text = text;
            uiText.font = ResolveFont();
            uiText.fontSize = fontSize;
            uiText.fontStyle = fontStyle;
            uiText.alignment = alignment;
            uiText.color = color;
            uiText.raycastTarget = false;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            return uiText;
        }

        private static Font ResolveFont()
        {
            if (_runtimeFont != null)
                return _runtimeFont;

            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_runtimeFont == null)
                _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return _runtimeFont;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystem.AddComponent<InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            Transform rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
                SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }
}
