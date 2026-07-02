using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public sealed class GemGrabCountdownOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _overlayRoot;
        [SerializeField] private TMP_Text _bigTextTmp;
        [SerializeField] private Text _bigTextLegacy;

        [Header("Presentation")]
        [SerializeField] private Color _blueColor = new Color(0.34f, 0.62f, 1f, 1f);
        [SerializeField] private Color _redColor = new Color(1f, 0.32f, 0.36f, 1f);

        public void BindOverlay(GameObject overlayRoot, TMP_Text bigTextTmp, Text bigTextLegacy)
        {
            _overlayRoot = overlayRoot;
            _bigTextTmp = bigTextTmp;
            _bigTextLegacy = bigTextLegacy;

            SetText(string.Empty);
            Show(false);
        }

        private void Update()
        {
            MatchManager matchManager = MatchManager.Instance;
            GemGrabMode gemGrab = GemGrabMode.Instance;
            if (matchManager == null ||
                matchManager.CurrentState != MatchState.Active ||
                gemGrab == null ||
                !gemGrab.HasLeader ||
                gemGrab.WinTimerRemainingSeconds <= 0f)
            {
                Show(false);
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(gemGrab.WinTimerRemainingSeconds));
            string teamLabel = gemGrab.LeadingTeam == TeamType.Blue ? "BLUE" : "RED";
            SetColor(gemGrab.LeadingTeam == TeamType.Blue ? _blueColor : _redColor);
            SetText($"{teamLabel} COUNTDOWN: {seconds}");
            Show(true);
        }

        private void Show(bool visible)
        {
            if (_overlayRoot != null && _overlayRoot.activeSelf != visible)
                _overlayRoot.SetActive(visible);
        }

        private void SetText(string text)
        {
            if (_bigTextTmp != null)
                _bigTextTmp.text = text;
            else if (_bigTextLegacy != null)
                _bigTextLegacy.text = text;
        }

        private void SetColor(Color color)
        {
            if (_bigTextTmp != null)
                _bigTextTmp.color = color;
            else if (_bigTextLegacy != null)
                _bigTextLegacy.color = color;
        }
    }
}
