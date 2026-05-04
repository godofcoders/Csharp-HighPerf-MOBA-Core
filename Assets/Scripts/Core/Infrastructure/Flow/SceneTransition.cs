using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Singleton fade-to-black overlay that drives every scene transition.
    /// Auto-creates its own Canvas + Image at runtime so no scene wiring
    /// is required — drop a SceneTransition GameObject in the Loading
    /// scene (or let SceneFlow auto-create one) and every
    /// <see cref="SceneFlow.LoadScene"/> call routes through this for a
    /// fade-out → load → fade-in sequence.
    ///
    /// The overlay Canvas has a very high sortingOrder (9999) so it draws
    /// above every gameplay/HUD canvas. Renderer is Screen-Space-Overlay
    /// so it's resolution-independent and doesn't need a camera reference.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        public static SceneTransition Instance { get; private set; }

        [Header("Tuning")]
        [Tooltip("Seconds for the fade-OUT (visible scene → black) before the new scene loads.")]
        [Min(0f)]
        [SerializeField] private float _fadeOutSeconds = 0.30f;

        [Tooltip("Seconds for the fade-IN (black → visible new scene) after load completes.")]
        [Min(0f)]
        [SerializeField] private float _fadeInSeconds = 0.30f;

        [Tooltip("Color of the fade veil. Black is most universal; some games use white for snappier feel.")]
        [SerializeField] private Color _fadeColor = Color.black;

        [Tooltip("Sort order of the overlay canvas. Must be higher than every gameplay canvas (typically 0–10) so the veil draws on top.")]
        [SerializeField] private int _canvasSortOrder = 9999;

        private Canvas _overlayCanvas;
        private CanvasGroup _overlayGroup;
        private Image _overlayImage;
        private bool _isTransitioning;

        public bool IsTransitioning => _isTransitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildOverlay();
            // Start fully transparent so the first scene loads visibly.
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildOverlay()
        {
            // Canvas (child of this GameObject so it's covered by DontDestroyOnLoad).
            GameObject canvasGo = new GameObject("FadeCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            _overlayCanvas = canvasGo.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = _canvasSortOrder;

            _overlayGroup = canvasGo.GetComponent<CanvasGroup>();
            _overlayGroup.blocksRaycasts = false; // never block input when not transitioning
            _overlayGroup.interactable = false;

            // Block-Image child filling the screen.
            GameObject imgGo = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _overlayImage = imgGo.GetComponent<Image>();
            _overlayImage.color = _fadeColor;
            _overlayImage.raycastTarget = false;
        }

        /// <summary>
        /// Fade out to <see cref="_fadeColor"/>, async-load <paramref name="sceneName"/>,
        /// then fade back in. Public entry point used by SceneFlow.
        /// </summary>
        public IEnumerator TransitionTo(string sceneName)
        {
            if (_isTransitioning)
                yield break;
            _isTransitioning = true;
            _overlayGroup.blocksRaycasts = true; // block input during fade

            // 1. Fade out.
            yield return Fade(0f, 1f, _fadeOutSeconds);

            // 2. Async load with deferred activation so the new scene
            //    swaps in WHILE we're still black — no flash.
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneTransition] Could not start LoadSceneAsync for '{sceneName}'.");
                _overlayGroup.blocksRaycasts = false;
                _isTransitioning = false;
                yield break;
            }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
                yield return null;

            op.allowSceneActivation = true;
            // Wait one frame for the new scene's Awakes/Starts to run on
            // initial layout before fading in (otherwise first-frame
            // garbage flashes).
            yield return null;
            yield return null;

            // 3. Fade in.
            yield return Fade(1f, 0f, _fadeInSeconds);

            _overlayGroup.blocksRaycasts = false;
            _isTransitioning = false;
        }

        private IEnumerator Fade(float fromAlpha, float toAlpha, float seconds)
        {
            if (seconds <= 0f)
            {
                SetAlpha(toAlpha);
                yield break;
            }
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime; // unscaled so pause-menu fades work
                SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, t / seconds));
                yield return null;
            }
            SetAlpha(toAlpha);
        }

        private void SetAlpha(float a)
        {
            if (_overlayGroup != null) _overlayGroup.alpha = Mathf.Clamp01(a);
        }
    }
}
