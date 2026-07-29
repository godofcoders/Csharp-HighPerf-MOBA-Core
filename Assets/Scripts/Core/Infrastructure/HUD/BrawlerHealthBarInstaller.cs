using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Match-scene fallback installer for world-space brawler health bars.
    /// Authored bars win: if a brawler already has a BrawlerHealthBarView in
    /// its children, this installer leaves it alone.
    /// </summary>
    public sealed class BrawlerHealthBarInstaller : MonoBehaviour
    {
        private const string MatchSceneName = "Match";
        private const float ScanIntervalSeconds = 0.35f;
        private const float CanvasScale = 0.01f;
        private const float BarHeightWorld = 1.42f;
        private const int CanvasSortingOrder = 30;
        private const int RuntimeAmmoSlotCount = 5;

        private static Font _runtimeFont;
        private float _nextScanTime;

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

            if (FindObjectOfType<BrawlerHealthBarInstaller>() != null)
                return;

            GameObject host = new GameObject("BrawlerHealthBarInstaller");
            host.AddComponent<BrawlerHealthBarInstaller>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
                return;

            _nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
            AttachMissingBars();
        }

        private static void AttachMissingBars()
        {
            BrawlerController[] brawlers = FindObjectsOfType<BrawlerController>();
            for (int i = 0; i < brawlers.Length; i++)
            {
                BrawlerController brawler = brawlers[i];
                if (brawler == null || brawler.State == null)
                    continue;

                BrawlerHealthBarView existingBar =
                    brawler.GetComponentInChildren<BrawlerHealthBarView>(true);
                if (existingBar != null)
                {
                    CreateCarrierBadge(
                        existingBar.transform,
                        brawler,
                        existingBar.GetComponent<Canvas>() ??
                        existingBar.GetComponentInChildren<Canvas>(true));
                    CreatePowerCubeBadge(
                        existingBar.transform,
                        brawler,
                        existingBar.GetComponent<Canvas>() ??
                        existingBar.GetComponentInChildren<Canvas>(true));
                    continue;
                }

                CreateHealthBar(brawler);
            }
        }

        private static void CreateHealthBar(BrawlerController brawler)
        {
            Transform anchor = brawler.PresentationFollowTarget != null
                ? brawler.PresentationFollowTarget
                : brawler.transform;

            GameObject root = new GameObject(
                "HealthBar",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(BrawlerHealthBarView));

            root.transform.SetParent(anchor, false);
            root.transform.localPosition = new Vector3(0f, BarHeightWorld, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * CanvasScale;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(176f, 66f);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.overrideSorting = true;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 16f;

            Image frameImage = CreateImage(
                root.transform,
                "Frame",
                new Vector2(0f, -7f),
                new Vector2(168f, 22f),
                new Color(0f, 0f, 0f, 0.78f));

            Image backgroundImage = CreateImage(
                root.transform,
                "Background",
                new Vector2(0f, -7f),
                new Vector2(156f, 12f),
                new Color(0.05f, 0.05f, 0.06f, 0.88f));

            Image fillImage = CreateImage(
                backgroundImage.transform,
                "Fill",
                Vector2.zero,
                new Vector2(156f, 12f),
                Color.white);

            RectTransform fillRect = fillImage.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fillImage.type = Image.Type.Simple;
            fillImage.fillAmount = 1f;

            Image ammoFrame = CreateImage(
                root.transform,
                "AmmoFrame",
                new Vector2(0f, -25f),
                new Vector2(156f, 14f),
                new Color(0f, 0f, 0f, 0.62f));

            Image[] ammoSlots = new Image[RuntimeAmmoSlotCount];
            for (int i = 0; i < RuntimeAmmoSlotCount; i++)
            {
                Image ammoSlot = CreateImage(
                    ammoFrame.transform,
                    $"AmmoSlot{i + 1}",
                    new Vector2(-58f + (i * 23f), 0f),
                    new Vector2(18f, 6f),
                    new Color(1f, 0.84f, 0.18f, 0.96f));

                ammoSlot.type = Image.Type.Filled;
                ammoSlot.fillMethod = Image.FillMethod.Horizontal;
                ammoSlot.fillOrigin = (int)Image.OriginHorizontal.Left;
                ammoSlot.fillAmount = 1f;
                ammoSlots[i] = ammoSlot;
            }

            Text ammoCountText = CreateText(
                ammoFrame.transform,
                "AmmoCount",
                "0/0",
                new Vector2(57f, 0f),
                new Vector2(40f, 12f),
                10,
                TextAnchor.MiddleRight,
                new Color(1f, 0.88f, 0.34f, 0.98f),
                FontStyle.Bold);

            BrawlerHealthBarView view = root.GetComponent<BrawlerHealthBarView>();
            view.Bind(brawler, fillImage, backgroundImage, frameImage, canvas);
            view.BindAmmoWidgets(ammoSlots, ammoCountText);
            CreateCarrierBadge(root.transform, brawler, canvas);
            CreatePowerCubeBadge(root.transform, brawler, canvas);

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        }

        private static void CreateCarrierBadge(
            Transform parent,
            BrawlerController brawler,
            Canvas canvas)
        {
            if (parent == null ||
                parent.GetComponentInChildren<BrawlerCarrierBadgeView>(true) != null)
            {
                return;
            }

            if (canvas == null)
                canvas = parent.GetComponent<Canvas>() ??
                         parent.GetComponentInChildren<Canvas>(true);

            GameObject badgeRoot = new GameObject(
                "CarrierGemBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            badgeRoot.transform.SetParent(parent, false);

            RectTransform badgeRect = badgeRoot.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(0f, 15f);
            badgeRect.sizeDelta = new Vector2(62f, 22f);

            Image badgeBackground = badgeRoot.GetComponent<Image>();
            badgeBackground.color = new Color(0f, 0f, 0f, 0.64f);
            badgeBackground.sprite = ResolveUISprite();
            badgeBackground.raycastTarget = false;

            Image gemIcon = CreateImage(
                badgeRoot.transform,
                "GemIcon",
                new Vector2(-16f, 0f),
                new Vector2(15f, 15f),
                Color.white);
            gemIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Text countText = CreateText(
                badgeRoot.transform,
                "GemCount",
                "0",
                new Vector2(8f, 0f),
                new Vector2(34f, 20f),
                16,
                TextAnchor.MiddleLeft,
                Color.white,
                FontStyle.Bold);

            BrawlerCarrierBadgeView badgeView =
                parent.GetComponent<BrawlerCarrierBadgeView>();
            if (badgeView == null)
                badgeView = parent.gameObject.AddComponent<BrawlerCarrierBadgeView>();

            badgeView.Bind(
                brawler,
                badgeRoot,
                gemIcon,
                null,
                countText,
                canvas);

            SetLayerRecursively(badgeRoot, LayerMask.NameToLayer("UI"));
        }

        private static void CreatePowerCubeBadge(
            Transform parent,
            BrawlerController brawler,
            Canvas canvas)
        {
            if (parent == null ||
                parent.GetComponentInChildren<BrawlerPowerCubeBadgeView>(true) != null)
            {
                return;
            }

            if (canvas == null)
                canvas = parent.GetComponent<Canvas>() ??
                         parent.GetComponentInChildren<Canvas>(true);

            GameObject badgeRoot = new GameObject(
                "PowerCubeBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            badgeRoot.transform.SetParent(parent, false);

            RectTransform badgeRect = badgeRoot.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(0f, 39f);
            badgeRect.sizeDelta = new Vector2(58f, 22f);

            Image badgeBackground = badgeRoot.GetComponent<Image>();
            badgeBackground.color = new Color(0f, 0f, 0f, 0.66f);
            badgeBackground.sprite = ResolveUISprite();
            badgeBackground.raycastTarget = false;

            Image cubeIcon = CreateImage(
                badgeRoot.transform,
                "PowerCubeIcon",
                new Vector2(-15f, 0f),
                new Vector2(15f, 15f),
                new Color(0.58f, 1f, 0.16f));
            cubeIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Text countText = CreateText(
                badgeRoot.transform,
                "PowerCubeCount",
                "0",
                new Vector2(8f, 0f),
                new Vector2(30f, 20f),
                16,
                TextAnchor.MiddleLeft,
                Color.white,
                FontStyle.Bold);

            BrawlerPowerCubeBadgeView badgeView =
                parent.GetComponent<BrawlerPowerCubeBadgeView>();
            if (badgeView == null)
                badgeView = parent.gameObject.AddComponent<BrawlerPowerCubeBadgeView>();

            badgeView.Bind(
                brawler,
                badgeRoot,
                cubeIcon,
                null,
                countText,
                canvas);

            SetLayerRecursively(badgeRoot, LayerMask.NameToLayer("UI"));
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = ResolveUISprite();
            image.raycastTarget = false;

            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string text,
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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
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
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.84f);
            outline.effectDistance = new Vector2(1.1f, -1.1f);
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

        private static Sprite ResolveUISprite()
        {
            return RuntimeUISpriteUtility.GetSolidWhiteSprite();
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
