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
            SetStatus(_statusLabel);
            if (_progressFill != null) _progressFill.fillAmount = 0f;
            StartCoroutine(LoadFlow());
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
                // strand the player on the loading screen forever.
                Debug.LogError("[LoadingScreen] SceneFlow.Instance is null. Make sure SceneFlow GameObject is in the Loading scene. Falling back to synchronous load.");
                yield return new WaitForSeconds(_minDisplaySeconds);
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                yield break;
            }

            // Poll real progress. Unity caps op.progress at 0.9f while
            // allowSceneActivation is false; scale to 0..1 for the UI.
            while (op.progress < 0.9f)
            {
                float displayProgress = Mathf.Clamp01(op.progress / 0.9f);
                if (_progressFill != null) _progressFill.fillAmount = displayProgress;
                SetStatus(string.Format(_progressFormat, Mathf.FloorToInt(displayProgress * 100f)));
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
