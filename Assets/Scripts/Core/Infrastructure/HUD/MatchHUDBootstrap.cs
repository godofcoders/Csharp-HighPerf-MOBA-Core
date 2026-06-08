using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Runtime fallback HUD composer for the Match scene. It only installs
    /// when no authored match HUD exists, so a designer-made prefab can replace
    /// this without creating duplicate score/countdown/feed widgets.
    /// </summary>
    public sealed class MatchHUDBootstrap : MonoBehaviour
    {
        private const string MatchSceneName = "Match";
        private const int CanvasSortingOrder = 80;

        private static Font _runtimeFont;
        private static Sprite _runtimeUISprite;

        private bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene);
        }

        private static void TryInstallForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MatchSceneName)
                return;

            if (HasAuthoredMatchHUD())
                return;

            GameObject host = new GameObject("MatchHUDBootstrap");
            MatchHUDBootstrap bootstrap = host.AddComponent<MatchHUDBootstrap>();
            bootstrap.Install();
        }

        private static bool HasAuthoredMatchHUD()
        {
            return Object.FindObjectOfType<MatchHUDBootstrap>() != null ||
                   Object.FindObjectOfType<MatchHUD>() != null ||
                   Object.FindObjectOfType<MatchCountdownOverlay>() != null ||
                   Object.FindObjectOfType<CombatLogHUD>() != null ||
                   Object.FindObjectOfType<DeathOverlay>() != null;
        }

        private void Start()
        {
            Install();
        }

        public void Install()
        {
            if (_installed)
                return;

            _installed = true;

            GameObject canvasGo = new GameObject(
                "MatchHUDCanvas",
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

            Transform canvasTransform = canvasGo.transform;
            CreateMatchStatus(canvasTransform);
            CreateCombatFeed(canvasTransform);
            CreateCountdownOverlay(canvasTransform);
            CreateDeathOverlay(canvasTransform);

            SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));
        }

        private static void CreateMatchStatus(Transform parent)
        {
            Text statusText = CreateText(
                parent,
                "MatchStatusText",
                string.Empty,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(1180f, 42f),
                24,
                TextAnchor.UpperCenter,
                Color.white,
                FontStyle.Bold);

            MatchHUD hud = statusText.gameObject.AddComponent<MatchHUD>();
            hud.BindTextTargets(null, statusText);
        }

        private static void CreateCombatFeed(Transform parent)
        {
            Text feedText = CreateText(
                parent,
                "CombatFeedText",
                string.Empty,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -92f),
                new Vector2(540f, 180f),
                20,
                TextAnchor.UpperLeft,
                new Color(1f, 1f, 1f, 0.92f),
                FontStyle.Bold);

            feedText.horizontalOverflow = HorizontalWrapMode.Wrap;
            feedText.verticalOverflow = VerticalWrapMode.Truncate;

            CombatLogHUD combatLog = feedText.gameObject.AddComponent<CombatLogHUD>();
            combatLog.BindTextTargets(null, feedText, feedText.gameObject);
        }

        private static void CreateCountdownOverlay(Transform parent)
        {
            GameObject controller = CreateController(parent, "CountdownOverlayController");
            GameObject root = CreatePanel(
                parent,
                "CountdownOverlay",
                new Color(0f, 0f, 0f, 0.16f));

            Text countdownText = CreateText(
                root.transform,
                "CountdownText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(680f, 180f),
                96,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            MatchCountdownOverlay countdown = controller.AddComponent<MatchCountdownOverlay>();
            countdown.BindOverlay(root, null, countdownText);
        }

        private static void CreateDeathOverlay(Transform parent)
        {
            GameObject controller = CreateController(parent, "DeathOverlayController");
            GameObject root = CreatePanel(
                parent,
                "DeathOverlay",
                new Color(0f, 0f, 0f, 0.56f));

            Text titleText = CreateText(
                root.transform,
                "DeathTitleText",
                "You died",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f),
                new Vector2(720f, 80f),
                46,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);

            Text countdownText = CreateText(
                root.transform,
                "RespawnCountdownText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f),
                new Vector2(720f, 54f),
                28,
                TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.92f),
                FontStyle.Bold);

            Text killerText = CreateText(
                root.transform,
                "KilledByText",
                string.Empty,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -44f),
                new Vector2(720f, 44f),
                22,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.82f, 0.92f),
                FontStyle.Bold);

            DeathOverlay deathOverlay = controller.AddComponent<DeathOverlay>();
            deathOverlay.BindOverlay(
                root,
                null,
                titleText,
                null,
                countdownText,
                null,
                killerText);
        }

        private static GameObject CreateController(Transform parent, string name)
        {
            GameObject controller = new GameObject(name, typeof(RectTransform));
            controller.transform.SetParent(parent, false);
            RectTransform rect = controller.GetComponent<RectTransform>();
            Stretch(rect);
            return controller;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Stretch(rect);

            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.sprite = ResolveUISprite();
            image.raycastTarget = false;

            return panel;
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
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));

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
            outline.effectColor = new Color(0f, 0f, 0f, 0.74f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            outline.useGraphicAlpha = true;

            return uiText;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
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

        private static Sprite ResolveUISprite()
        {
            if (_runtimeUISprite != null)
                return _runtimeUISprite;

            _runtimeUISprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            return _runtimeUISprite;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0 || root == null)
                return;

            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
