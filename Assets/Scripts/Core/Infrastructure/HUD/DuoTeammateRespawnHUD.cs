using MOBA.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace MOBA.Core.Infrastructure
{
    public sealed class DuoTeammateRespawnHUD : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _countdownText;

        private BrawlerController _localPlayer;
        private float _nextPlayerSearchTime;

        private void Awake()
        {
            Show(false);
        }

        public void Bind(GameObject root, Text countdownText)
        {
            _root = root;
            _countdownText = countdownText;
            Show(false);
        }

        private void Update()
        {
            ResolveLocalPlayer();
            SoloShowdownMode mode = SoloShowdownMode.Instance;
            if (_localPlayer == null ||
                _localPlayer.State == null ||
                _localPlayer.State.IsDead ||
                mode == null ||
                !mode.TryGetPendingTeammateRespawn(
                    _localPlayer,
                    out _,
                    out float secondsRemaining))
            {
                Show(false);
                return;
            }

            if (_countdownText != null)
            {
                _countdownText.text =
                    $"TEAMMATE RESPAWNING IN {Mathf.CeilToInt(secondsRemaining)}";
            }

            Show(true);
        }

        private void ResolveLocalPlayer()
        {
            if (_localPlayer != null || Time.unscaledTime < _nextPlayerSearchTime)
                return;

            _nextPlayerSearchTime = Time.unscaledTime + 0.5f;
            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                PlayerCommandSource source = sources[i];
                if (source == null)
                    continue;

                BrawlerController brawler = source.GetComponent<BrawlerController>();
                if (brawler != null)
                {
                    _localPlayer = brawler;
                    return;
                }
            }
        }

        private void Show(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }
    }
}
