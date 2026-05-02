using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Simulation;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Screen-space HUD for the locally controlled brawler. Drives a fixed
    /// set of UI widgets each frame from the controlled brawler's state.
    ///
    /// Widgets (all optional — leave any field unwired and that widget is
    /// just skipped):
    ///   - Ammo dots: array of Image components, one per max-ammo slot.
    ///     Filled colour for "ready", unfilled colour for "reloading", and
    ///     the in-progress slot has its fillAmount lerped by the partial
    ///     reload progress (Brawl Stars-style).
    ///   - Super-charge bar/ring: single Filled Image, fillAmount = 0..1.
    ///   - Super-ready glow: GameObject toggled on while super is full.
    ///   - Hypercharge bar: Filled Image fillAmount = 0..1; an "active"
    ///     visual is also toggled while hypercharge is firing.
    ///   - Gadget UI: count text + cooldown overlay Image (Filled, Radial360
    ///     looks best). When count > 0 and cooldown done, ready visual on.
    ///   - Carrier badge: gem icon GameObject + count text, shown when
    ///     CarriedGemCount > 0.
    ///
    /// The brawler reference is set via <see cref="SetControlledBrawler"/>
    /// or auto-found via the singleton-like search in <see cref="Awake"/>.
    /// Both TMP and legacy Text are supported (whichever is wired wins) so
    /// projects without TMP can still use the HUD.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The brawler this HUD reflects. If null, the first BrawlerController in the scene that has a PlayerCommandSource component is used.")]
        [SerializeField] private BrawlerController _controlledBrawler;

        [Header("Ammo")]
        [Tooltip("One Image per ammo slot. Sized to max ammo on the equipped weapon. Use Image Type = Filled, Vertical or Horizontal — fillAmount is driven for the slot currently reloading.")]
        [SerializeField] private Image[] _ammoSlots;
        [SerializeField] private Color _ammoReadyColor = new Color(1f, 0.85f, 0.20f);
        [SerializeField] private Color _ammoEmptyColor = new Color(0.35f, 0.35f, 0.35f, 0.55f);

        [Header("Super")]
        [Tooltip("Filled Image (Radial360 reads as a ring; Horizontal reads as a bar).")]
        [SerializeField] private Image _superFill;
        [Tooltip("Optional GameObject shown when super is ready (full meter).")]
        [SerializeField] private GameObject _superReadyVisual;

        [Header("Hypercharge")]
        [SerializeField] private Image _hyperchargeFill;
        [Tooltip("Optional GameObject shown while hypercharge is currently active.")]
        [SerializeField] private GameObject _hyperchargeActiveVisual;

        [Header("Gadget")]
        [Tooltip("Image overlay used as a cooldown sweep. Image Type = Filled, Radial360. fillAmount=0 → ready, 1 → just used.")]
        [SerializeField] private Image _gadgetCooldownOverlay;
        [Tooltip("Optional text showing remaining gadget charges. Either TMP or legacy.")]
        [SerializeField] private TMP_Text _gadgetChargesTmp;
        [SerializeField] private Text _gadgetChargesLegacy;
        [Tooltip("Optional GameObject shown when at least one gadget charge is available and not on cooldown.")]
        [SerializeField] private GameObject _gadgetReadyVisual;

        [Header("Gem Carrier")]
        [Tooltip("Root GameObject for the carrier badge. Toggled on when CarriedGemCount > 0.")]
        [SerializeField] private GameObject _carrierBadgeRoot;
        [Tooltip("Optional text showing carried-gem count.")]
        [SerializeField] private TMP_Text _carriedGemCountTmp;
        [SerializeField] private Text _carriedGemCountLegacy;

        private void Awake()
        {
            if (_controlledBrawler == null)
                AutoDiscoverBrawler();
        }

        public void SetControlledBrawler(BrawlerController brawler) => _controlledBrawler = brawler;

        private void AutoDiscoverBrawler()
        {
            // Prefer a brawler that has a PlayerCommandSource attached —
            // that's the locally controlled one. Fall back to first brawler
            // in the scene if no PlayerCommandSource exists yet.
            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            if (sources.Length > 0)
            {
                BrawlerController b = sources[0].GetComponent<BrawlerController>();
                if (b != null) { _controlledBrawler = b; return; }
            }

            BrawlerController[] all = FindObjectsOfType<BrawlerController>();
            if (all.Length > 0) _controlledBrawler = all[0];
        }

        private void Update()
        {
            if (_controlledBrawler == null || _controlledBrawler.State == null)
                return;

            BrawlerState state = _controlledBrawler.State;

            UpdateAmmo(state);
            UpdateSuper(state);
            UpdateHypercharge(state);
            UpdateGadget(state);
            UpdateCarrierBadge(state);
        }

        private void UpdateAmmo(BrawlerState state)
        {
            if (_ammoSlots == null || _ammoSlots.Length == 0) return;

            ResourceStorage ammo = state.Ammo;
            if (ammo == null) return;

            // CurrentAmmo is a float (e.g. 1.7 = "one full bar + 70% into the
            // next reload"). AvailableBars is the integer floor.
            float current = ammo.CurrentAmmo;
            int available = ammo.AvailableBars;
            int maxBars = Mathf.Max(1, ammo.MaxAmmo);

            for (int i = 0; i < _ammoSlots.Length; i++)
            {
                Image slot = _ammoSlots[i];
                if (slot == null) continue;

                if (i >= maxBars)
                {
                    // Slot is beyond the brawler's max ammo (e.g. equipped a
                    // 3-bar weapon, HUD has 4 dots). Hide the surplus.
                    slot.enabled = false;
                    continue;
                }

                slot.enabled = true;

                if (i < available)
                {
                    // Fully ready bar.
                    slot.color = _ammoReadyColor;
                    slot.fillAmount = 1f;
                }
                else if (i == available)
                {
                    // The bar currently reloading — partial fill.
                    float partial = Mathf.Clamp01(current - available);
                    slot.color = Color.Lerp(_ammoEmptyColor, _ammoReadyColor, partial);
                    slot.fillAmount = partial;
                }
                else
                {
                    // Future-bar, fully empty.
                    slot.color = _ammoEmptyColor;
                    slot.fillAmount = 0f;
                }
            }
        }

        private void UpdateSuper(BrawlerState state)
        {
            SuperChargeTracker sc = state.SuperCharge;
            if (sc == null) return;

            if (_superFill != null)
                _superFill.fillAmount = Mathf.Clamp01(sc.ChargePercent);

            if (_superReadyVisual != null && _superReadyVisual.activeSelf != sc.IsReady)
                _superReadyVisual.SetActive(sc.IsReady);
        }

        private void UpdateHypercharge(BrawlerState state)
        {
            HyperchargeTracker hc = state.Hypercharge;
            if (hc == null) return;

            if (_hyperchargeFill != null)
                _hyperchargeFill.fillAmount = Mathf.Clamp01(hc.ChargePercent);

            if (_hyperchargeActiveVisual != null && _hyperchargeActiveVisual.activeSelf != hc.IsActive)
                _hyperchargeActiveVisual.SetActive(hc.IsActive);
        }

        private void UpdateGadget(BrawlerState state)
        {
            int charges = state.RemainingGadgets;

            // AbilityCooldownState only stores ReadyAtTick (no original
            // duration), so we can't compute a true sweep ratio without
            // knowing when the cooldown started. Binary fallback: overlay
            // is full while on cooldown, empty when ready. Upgrade path:
            // add DurationTicks to AbilityCooldownState and divide here.
            uint currentTick = 0;
            ISimulationClock clock = ServiceProvider.Get<ISimulationClock>();
            if (clock != null) currentTick = clock.CurrentTick;

            bool onCooldown = !state.GadgetCooldown.IsReady(currentTick);
            float cdRatio = onCooldown ? 1f : 0f;

            if (_gadgetCooldownOverlay != null)
                _gadgetCooldownOverlay.fillAmount = cdRatio;

            string countStr = charges.ToString();
            if (_gadgetChargesTmp != null) _gadgetChargesTmp.text = countStr;
            else if (_gadgetChargesLegacy != null) _gadgetChargesLegacy.text = countStr;

            if (_gadgetReadyVisual != null)
            {
                bool ready = charges > 0 && !onCooldown;
                if (_gadgetReadyVisual.activeSelf != ready)
                    _gadgetReadyVisual.SetActive(ready);
            }
        }

        private void UpdateCarrierBadge(BrawlerState state)
        {
            int count = state.CarriedGemCount;
            bool show = count > 0;

            if (_carrierBadgeRoot != null && _carrierBadgeRoot.activeSelf != show)
                _carrierBadgeRoot.SetActive(show);

            if (!show) return;

            string text = count.ToString();
            if (_carriedGemCountTmp != null) _carriedGemCountTmp.text = text;
            else if (_carriedGemCountLegacy != null) _carriedGemCountLegacy.text = text;
        }
    }
}
