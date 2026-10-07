using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public sealed class PowerCubeCrateHealthBarView : MonoBehaviour
    {
        public const string RootName = "ChestHealthBar";

        private const float CanvasScale = 0.0044f;
        private const float BarHeightWorld = 0.90f;
        private const float FillWidth = 148f;
        private const int CanvasSortingOrder = 28;

        private static readonly Color HealthyColor = new Color(0.34f, 0.90f, 0.24f, 1f);
        private static readonly Color DamagedColor = new Color(1f, 0.72f, 0.12f, 1f);
        private static readonly Color CriticalColor = new Color(1f, 0.22f, 0.14f, 1f);

        [SerializeField] private PowerCubeCrateController _crate;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _frameImage;

        private Camera _camera;
        private RectTransform _fillRect;

        public float DisplayedHealthRatio { get; private set; } = 1f;

        public static PowerCubeCrateHealthBarView EnsureFor(
            PowerCubeCrateController crate)
        {
            if (crate == null)
                return null;

            Transform existing = crate.transform.Find(RootName);
            PowerCubeCrateHealthBarView view = existing != null
                ? existing.GetComponent<PowerCubeCrateHealthBarView>()
                : null;

            if (view == null)
                view = Create(crate);

            view.Bind(crate);
            return view;
        }

        public void Bind(PowerCubeCrateController crate)
        {
            _crate = crate;
            AutoBindReferences();
            ApplyStaticColors();
            float ratio = crate != null
                ? crate.CurrentHealth / Mathf.Max(1f, crate.MaxHealth)
                : 0f;
            SetHealthRatio(ratio);
        }

        public void SetHealthRatio(float ratio)
        {
            AutoBindReferences();
            DisplayedHealthRatio = Mathf.Clamp01(ratio);

            if (_fillRect != null)
            {
                _fillRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    FillWidth * DisplayedHealthRatio);
            }

            if (_fillImage != null)
                _fillImage.color = ResolveFillColor(DisplayedHealthRatio);

            SetVisible(_crate != null && !_crate.IsDestroyed);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private static PowerCubeCrateHealthBarView Create(
            PowerCubeCrateController crate)
        {
            GameObject root = new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(PowerCubeCrateHealthBarView));

            root.transform.SetParent(crate.transform, false);
            root.transform.localPosition = new Vector3(0f, BarHeightWorld, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * CanvasScale;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(164f, 26f);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 16f;

            Image frame = CreateImage(
                root.transform,
                "Frame",
                new Vector2(158f, 18f),
                new Color(0f, 0f, 0f, 0.82f));
            Image background = CreateImage(
                root.transform,
                "Background",
                new Vector2(FillWidth, 10f),
                new Color(0.055f, 0.045f, 0.035f, 0.94f));
            Image fill = CreateImage(
                background.transform,
                "Fill",
                new Vector2(FillWidth, 10f),
                HealthyColor);

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(-FillWidth * 0.5f, 0f);

            int uiLayer = LayerMask.NameToLayer("UI");
            SetLayerRecursively(root, uiLayer >= 0 ? uiLayer : crate.gameObject.layer);

            PowerCubeCrateHealthBarView view =
                root.GetComponent<PowerCubeCrateHealthBarView>();
            view._crate = crate;
            view._canvas = canvas;
            view._fillImage = fill;
            view._backgroundImage = background;
            view._frameImage = frame;
            view._fillRect = fillRect;
            view.ApplyStaticColors();
            return view;
        }

        private static Image CreateImage(
            Transform parent,
            string objectName,
            Vector2 size,
            Color color)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = RuntimeUISpriteUtility.GetSolidWhiteSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void Awake()
        {
            AutoBindReferences();
            ApplyStaticColors();
        }

        private void LateUpdate()
        {
            if (_crate == null || _crate.IsDestroyed)
            {
                SetVisible(false);
                return;
            }

            BillboardToCamera();
        }

        private void AutoBindReferences()
        {
            if (_crate == null)
                _crate = GetComponentInParent<PowerCubeCrateController>();

            if (_canvas == null)
                _canvas = GetComponent<Canvas>();

            if (_fillImage == null)
                _fillImage = FindImage("Fill");

            if (_backgroundImage == null)
                _backgroundImage = FindImage("Background");

            if (_frameImage == null)
                _frameImage = FindImage("Frame");

            if (_fillRect == null && _fillImage != null)
                _fillRect = _fillImage.rectTransform;
        }

        private Image FindImage(string objectName)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.gameObject.name == objectName)
                    return image;
            }

            return null;
        }

        private void ApplyStaticColors()
        {
            if (_backgroundImage != null)
                _backgroundImage.color = new Color(0.055f, 0.045f, 0.035f, 0.94f);

            if (_frameImage != null)
                _frameImage.color = new Color(0f, 0f, 0f, 0.82f);
        }

        private static Color ResolveFillColor(float ratio)
        {
            if (ratio <= 0.30f)
                return CriticalColor;

            if (ratio <= 0.60f)
                return DamagedColor;

            return HealthyColor;
        }

        private void BillboardToCamera()
        {
            if (_canvas == null)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
                return;

            Transform canvasTransform = _canvas.transform;
            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - _camera.transform.position,
                Vector3.up);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0)
                return;

            root.layer = layer;
            Transform rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
                SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }
}
