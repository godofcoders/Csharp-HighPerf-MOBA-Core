using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Loading screen. Auto-advances to MainMenu after a configurable hold
    /// time. Drives an optional progress bar (0 → 1 over the hold) so the
    /// player sees motion. If you wire actual async asset loading later,
    /// swap the timer for AsyncOperation.progress.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const string RuntimePresentationName = "RuntimeLoadingPresentation";

        [Header("References")]
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _statusTextTmp;
        [SerializeField] private Text _statusTextLegacy;

        [Header("Tuning")]
        [Tooltip("Minimum time the loading screen stays visible, even if the actual load is faster. Prevents a sub-100ms flash on fast machines / empty target scenes.")]
        [Min(0f)]
        [SerializeField] private float _minDisplaySeconds = 0.8f;
        [Tooltip("Seconds to hold at 100% before activating the next scene. Lets the player register that loading completed.")]
        [Min(0f)]
        [SerializeField] private float _completionHoldSeconds = 0.2f;
        [SerializeField] private string _statusLabel = "Loading...";
        [Tooltip("Status text format when reporting progress. {0} = integer percent.")]
        [SerializeField] private string _progressFormat = "Loading... {0}%";
        [SerializeField] private string _readyLabel = "Ready";

        private void Start()
        {
            BuildRuntimePresentation();
            SetStatus(_statusLabel);
            if (_progressFill != null) _progressFill.fillAmount = 0f;
            StartCoroutine(LoadFlow());
        }

        private void BuildRuntimePresentation()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform;

            Transform existing = root.Find(RuntimePresentationName);
            if (existing != null)
            {
                existing.SetAsLastSibling();
                return;
            }

            GameObject presentation = new GameObject(RuntimePresentationName, typeof(RectTransform), typeof(CanvasGroup));
            presentation.transform.SetParent(root, false);
            RectTransform presentationRect = presentation.GetComponent<RectTransform>();
            MenuUITheme.Anchor(presentationRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Sprite backgroundSprite = BrawlerGeneratedArtLibrary.LoadLoadingHomeBackground();
            GameObject background = MenuUITheme.CreatePanel("Background", presentation.transform, Color.white);
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.sprite = backgroundSprite != null ? backgroundSprite : RuntimeUISpriteUtility.GetSolidWhiteSprite();
            backgroundImage.color = backgroundSprite != null ? Color.white : MenuUITheme.ScreenBackground;
            backgroundImage.raycastTarget = false;
            MenuUITheme.Anchor(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject veil = MenuUITheme.CreatePanel("Vignette", presentation.transform, new Color(0.005f, 0.011f, 0.030f, 0.36f));
            veil.GetComponent<Image>().raycastTarget = false;
            MenuUITheme.Anchor(veil.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            TMP_Text title = MenuUITheme.CreateText(
                presentation.transform,
                "Title",
                "MOBA CORE",
                60f,
                TextAlignmentOptions.Center,
                Color.white);
            title.fontStyle = FontStyles.Bold;
            MenuUITheme.EnsureShadow(title.gameObject);
            MenuUITheme.Anchor(title.rectTransform, new Vector2(0.20f, 0.62f), new Vector2(0.80f, 0.76f), Vector2.zero, Vector2.zero);

            TMP_Text subtitle = MenuUITheme.CreateText(
                presentation.transform,
                "Subtitle",
                "Advanced Game AI Arena",
                24f,
                TextAlignmentOptions.Center,
                MenuUITheme.TextMuted);
            subtitle.fontStyle = FontStyles.Bold;
            MenuUITheme.EnsureShadow(subtitle.gameObject);
            MenuUITheme.Anchor(subtitle.rectTransform, new Vector2(0.24f, 0.56f), new Vector2(0.76f, 0.63f), Vector2.zero, Vector2.zero);

            GameObject barFrame = MenuUITheme.CreatePanel("ProgressFrame", presentation.transform, new Color(0.010f, 0.020f, 0.048f, 0.92f));
            MenuUITheme.Anchor(barFrame.GetComponent<RectTransform>(), new Vector2(0.26f, 0.115f), new Vector2(0.74f, 0.155f), Vector2.zero, Vector2.zero);

            GameObject fillObject = MenuUITheme.CreatePanel("ProgressFill", barFrame.transform, MenuUITheme.Gold);
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            MenuUITheme.Anchor(fillObject.GetComponent<RectTransform>(), new Vector2(0.015f, 0.18f), new Vector2(0.985f, 0.82f), Vector2.zero, Vector2.zero);
            _progressFill = fill;

            TMP_Text status = MenuUITheme.CreateText(
                presentation.transform,
                "Status",
                _statusLabel,
                19f,
                TextAlignmentOptions.Center,
                MenuUITheme.TextSoft);
            status.fontStyle = FontStyles.Bold;
            MenuUITheme.Anchor(status.rectTransform, new Vector2(0.26f, 0.075f), new Vector2(0.74f, 0.112f), Vector2.zero, Vector2.zero);
            _statusTextTmp = status;

            presentation.transform.SetAsLastSibling();
        }

        private IEnumerator LoadFlow()
        {
            float startTime = Time.time;

            // Real async load. allowSceneActivation = false so the bar can
            // finish visually before the scene swaps in.
            AsyncOperation op = SceneFlow.Instance != null
                ? SceneFlow.Instance.LoadSceneAsync(SceneId.MainMenu, allowActivation: false)
                : null;

            if (op == null)
            {
                // No SceneFlow instance — synchronous fallback so we don't
                // strand the player. Animate the bar fake-style so it's
                // not silent; player still sees motion.
                Debug.LogError("[LoadingScreen] SceneFlow.Instance is null. Make sure SceneFlow GameObject is in the Loading scene. Falling back to synchronous load with fake bar.");

                float t = 0f;
                while (t < _minDisplaySeconds)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / _minDisplaySeconds);
                    if (_progressFill != null) _progressFill.fillAmount = p;
                    SetStatus(string.Format(_progressFormat, Mathf.FloorToInt(p * 100f)));
                    yield return null;
                }
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                yield break;
            }

            // Poll real progress. Unity caps op.progress at 0.9f while
            // allowSceneActivation is false; scale to 0..1 for the UI.
            // Also smooth-step the visible value so empty/tiny scenes don't
            // jump from 0 to 100% in one frame.
            float visibleProgress = 0f;
            while (op.progress < 0.9f || visibleProgress < 0.99f)
            {
                float targetProgress = Mathf.Clamp01(op.progress / 0.9f);
                visibleProgress = Mathf.MoveTowards(visibleProgress, targetProgress, Time.deltaTime * 1.5f);
                if (_progressFill != null) _progressFill.fillAmount = visibleProgress;
                SetStatus(string.Format(_progressFormat, Mathf.FloorToInt(visibleProgress * 100f)));

                // Once op.progress is past 0.9 AND we've caught up visually, exit.
                if (op.progress >= 0.9f && visibleProgress >= 0.99f) break;
                yield return null;
            }

            // Load is ready. Honor minimum display time so we don't flash.
            float elapsed = Time.time - startTime;
            float remainingMin = _minDisplaySeconds - elapsed;
            if (remainingMin > 0f)
                yield return new WaitForSeconds(remainingMin);

            // Show "Ready" briefly at 100% so the player sees the bar fill.
            if (_progressFill != null) _progressFill.fillAmount = 1f;
            SetStatus(_readyLabel);
            yield return new WaitForSeconds(_completionHoldSeconds);

            op.allowSceneActivation = true;
        }

        private void SetStatus(string s)
        {
            if (_statusTextTmp != null) _statusTextTmp.text = s;
            else if (_statusTextLegacy != null) _statusTextLegacy.text = s;
        }
    }
}
