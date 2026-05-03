using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Full-screen "You died" overlay shown while the local brawler is
    /// dead. Drives a respawn countdown synced to SpawnManager's
    /// RespawnDelaySeconds.
    ///
    /// Detection: polls IsDead on the local brawler each frame. Captures
    /// Time.time on the false→true transition (death moment) and clears
    /// it on the true→false transition (respawn moment). The countdown is
    /// computed from `Time.time - deathTime` against `RespawnDelaySeconds`.
    ///
    /// Future "killed by X" line is wired but stays empty until kill-credit
    /// tracking lands (would need a LastDamageSource field on BrawlerState
    /// + DamageService writing to it). The killer-text widget gracefully
    /// hides if no killer string is set.
    ///
    /// Setup:
    ///   1. Full-screen Canvas (Screen Space - Overlay).
    ///   2. Optional dimmer Image (semi-transparent black) covering screen.
    ///   3. Centred big "You died" Text + smaller "Respawning in 5" Text.
    ///   4. Drop this on a root GameObject under the Canvas; assign
    ///        _overlayRoot, _titleText (optional, defaults to "You died"),
    ///        _countdownText, optional _killerText.
    /// </summary>
    public class DeathOverlay : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The brawler whose death triggers this overlay. If null, auto-discovers via PlayerCommandSource.")]
        [SerializeField] private BrawlerController _localBrawler;

        [Header("References")]
        [Tooltip("Root GameObject toggled on/off. Holds the dimmer + texts.")]
        [SerializeField] private GameObject _overlayRoot;

        [SerializeField] private TMP_Text _titleTextTmp;
        [SerializeField] private Text _titleTextLegacy;
        [SerializeField] private TMP_Text _countdownTextTmp;
        [SerializeField] private Text _countdownTextLegacy;
        [SerializeField] private TMP_Text _killerTextTmp;
        [SerializeField] private Text _killerTextLegacy;

        [Header("Tuning")]
        [SerializeField] private string _titleLabel = "You died";
        [Tooltip("Format string for the countdown. {0} = remaining seconds (integer).")]
        [SerializeField] private string _countdownFormat = "Respawning in {0}";

        private float _deathTime = -1f;
        private bool _wasDead;

        private void Awake()
        {
            if (_localBrawler == null)
                AutoDiscoverBrawler();

            // Title is static — set once at Awake.
            SetTitle(_titleLabel);

            // Killer line stays empty until kill-credit tracking exists.
            SetKiller(string.Empty);

            Show(false);
        }

        private void AutoDiscoverBrawler()
        {
            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            if (sources.Length > 0)
            {
                BrawlerController b = sources[0].GetComponent<BrawlerController>();
                if (b != null) { _localBrawler = b; return; }
            }
            BrawlerController[] all = FindObjectsOfType<BrawlerController>();
            if (all.Length > 0) _localBrawler = all[0];
        }

        public void SetLocalBrawler(BrawlerController brawler) => _localBrawler = brawler;

        /// <summary>Optional: set "killed by X" string (e.g. "Killed by Colt").
        /// Pass empty / null to hide the line.</summary>
        public void SetKilledBy(string killerName)
        {
            SetKiller(string.IsNullOrWhiteSpace(killerName) ? string.Empty : "Killed by " + killerName);
        }

        private void Update()
        {
            if (_localBrawler == null || _localBrawler.State == null)
            {
                Show(false);
                return;
            }

            bool dead = _localBrawler.State.IsDead;

            // Edge transitions: capture death time on false→true; clear on
            // true→false. Cheap state machine.
            if (dead && !_wasDead) _deathTime = Time.time;
            else if (!dead && _wasDead)
            {
                _deathTime = -1f;
                SetKiller(string.Empty); // wipe killer line on respawn
            }
            _wasDead = dead;

            if (!dead)
            {
                Show(false);
                return;
            }

            // Compute remaining respawn time.
            float respawnDelay = SpawnManager.Instance != null
                ? SpawnManager.Instance.RespawnDelaySeconds
                : 5f;
            float elapsed = Time.time - _deathTime;
            float remaining = respawnDelay - elapsed;
            if (remaining < 0f) remaining = 0f;

            string countdownStr = string.Format(_countdownFormat, Mathf.CeilToInt(remaining).ToString());
            SetCountdown(countdownStr);

            Show(true);
        }

        private void Show(bool visible)
        {
            if (_overlayRoot != null && _overlayRoot.activeSelf != visible)
                _overlayRoot.SetActive(visible);
        }

        private void SetTitle(string s)
        {
            if (_titleTextTmp != null) _titleTextTmp.text = s;
            else if (_titleTextLegacy != null) _titleTextLegacy.text = s;
        }

        private void SetCountdown(string s)
        {
            if (_countdownTextTmp != null) _countdownTextTmp.text = s;
            else if (_countdownTextLegacy != null) _countdownTextLegacy.text = s;
        }

        private void SetKiller(string s)
        {
            if (_killerTextTmp != null)
            {
                _killerTextTmp.text = s;
                _killerTextTmp.gameObject.SetActive(!string.IsNullOrEmpty(s));
            }
            else if (_killerTextLegacy != null)
            {
                _killerTextLegacy.text = s;
                _killerTextLegacy.gameObject.SetActive(!string.IsNullOrEmpty(s));
            }
        }
    }
}
