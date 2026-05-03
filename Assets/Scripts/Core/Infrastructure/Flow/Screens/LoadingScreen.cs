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
        [Min(0f)]
        [SerializeField] private float _holdSeconds = 2f;
        [SerializeField] private string _statusLabel = "Loading...";

        private void Start()
        {
            SetStatus(_statusLabel);
            StartCoroutine(LoadFlow());
        }

        private IEnumerator LoadFlow()
        {
            float t = 0f;
            while (t < _holdSeconds)
            {
                t += Time.deltaTime;
                if (_progressFill != null) _progressFill.fillAmount = Mathf.Clamp01(t / _holdSeconds);
                yield return null;
            }
            SceneFlow.Instance?.LoadScene(SceneId.MainMenu);
        }

        private void SetStatus(string s)
        {
            if (_statusTextTmp != null) _statusTextTmp.text = s;
            else if (_statusTextLegacy != null) _statusTextLegacy.text = s;
        }
    }
}
