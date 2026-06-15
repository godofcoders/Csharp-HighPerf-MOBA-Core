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

                if (brawler.GetComponentInChildren<BrawlerHealthBarView>(true) != null)
                    continue;

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
            rootRect.sizeDelta = new Vector2(168f, 22f);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = CanvasSortingOrder;
            canvas.overrideSorting = true;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 16f;

            Image frameImage = CreateImage(
                root.transform,
                "Frame",
                Vector2.zero,
                new Vector2(168f, 22f),
                new Color(0f, 0f, 0f, 0.78f));

            Image backgroundImage = CreateImage(
                root.transform,
                "Background",
                Vector2.zero,
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

            BrawlerHealthBarView view = root.GetComponent<BrawlerHealthBarView>();
            view.Bind(brawler, fillImage, backgroundImage, frameImage, canvas);

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
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
