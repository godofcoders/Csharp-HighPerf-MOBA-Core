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

        [Min(0.1f)]
        [SerializeField] private float _autoDiscoverIntervalSeconds = 0.5f;

        [Header("Ammo")]
        [Tooltip("One Image per ammo slot. Sized to max ammo on the equipped weapon. Use Image Type = Filled, Vertical or Horizontal — fillAmount is driven for the slot currently reloading.")]
        [SerializeField] private Image[] _ammoSlots;
        [Tooltip("Optional root per ammo slot, hidden when a brawler has fewer max-ammo slots than the fallback HUD can display.")]
        [SerializeField] private GameObject[] _ammoSlotRoots;
        [SerializeField] private TMP_Text _ammoCountTmp;
        [SerializeField] private Text _ammoCountLegacy;
        [SerializeField] private Color _ammoReadyColor = new Color(1f, 0.85f, 0.20f);
        [SerializeField] private Color _ammoEmptyColor = new Color(0.35f, 0.35f, 0.35f, 0.55f);

        [Header("Super")]
        [Tooltip("Filled Image (Radial360 reads as a ring; Horizontal reads as a bar).")]
        [SerializeField] private Image _superFill;
        [SerializeField] private TMP_Text _superChargeTmp;
        [SerializeField] private Text _superChargeLegacy;
        [Tooltip("Optional GameObject shown when super is ready (full meter).")]
        [SerializeField] private GameObject _superReadyVisual;

        [Header("Hypercharge")]
        [SerializeField] private GameObject _hyperchargeRoot;
        [SerializeField] private Image _hyperchargeFill;
        [SerializeField] private TMP_Text _hyperchargeChargeTmp;
        [SerializeField] private Text _hyperchargeChargeLegacy;
        [Tooltip("Optional GameObject shown while hypercharge is currently active.")]
        [SerializeField] private GameObject _hyperchargeActiveVisual;

        [Header("Gadget")]
        [SerializeField] private GameObject _gadgetRoot;
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

        private float _nextAutoDiscoverTime;

        private void Awake()
        {
            if (_controlledBrawler == null)
                AutoDiscoverBrawler();
        }

        public void SetControlledBrawler(BrawlerController brawler) => _controlledBrawler = brawler;

        public void BindAmmoWidgets(
            Image[] ammoSlots,
            GameObject[] ammoSlotRoots,
            TMP_Text ammoCountTmp,
            Text ammoCountLegacy)
        {
            _ammoSlots = ammoSlots;
            _ammoSlotRoots = ammoSlotRoots;
            _ammoCountTmp = ammoCountTmp;
            _ammoCountLegacy = ammoCountLegacy;
        }

        public void BindSuperWidgets(
            Image superFill,
            GameObject superReadyVisual,
            TMP_Text superChargeTmp,
            Text superChargeLegacy)
        {
            _superFill = superFill;
            _superReadyVisual = superReadyVisual;
            _superChargeTmp = superChargeTmp;
            _superChargeLegacy = superChargeLegacy;
        }

        public void BindHyperchargeWidgets(
            GameObject hyperchargeRoot,
            Image hyperchargeFill,
            GameObject hyperchargeActiveVisual,
            TMP_Text hyperchargeChargeTmp,
            Text hyperchargeChargeLegacy)
        {
            _hyperchargeRoot = hyperchargeRoot;
            _hyperchargeFill = hyperchargeFill;
            _hyperchargeActiveVisual = hyperchargeActiveVisual;
            _hyperchargeChargeTmp = hyperchargeChargeTmp;
            _hyperchargeChargeLegacy = hyperchargeChargeLegacy;
        }

        public void BindGadgetWidgets(
            GameObject gadgetRoot,
            Image cooldownOverlay,
            TMP_Text chargesTmp,
            Text chargesLegacy,
            GameObject readyVisual)
        {
            _gadgetRoot = gadgetRoot;
            _gadgetCooldownOverlay = cooldownOverlay;
            _gadgetChargesTmp = chargesTmp;
            _gadgetChargesLegacy = chargesLegacy;
            _gadgetReadyVisual = readyVisual;
        }

        public void BindCarrierWidgets(
            GameObject carrierBadgeRoot,
            TMP_Text carriedGemCountTmp,
            Text carriedGemCountLegacy)
        {
            _carrierBadgeRoot = carrierBadgeRoot;
            _carriedGemCountTmp = carriedGemCountTmp;
            _carriedGemCountLegacy = carriedGemCountLegacy;
        }

        private bool AutoDiscoverBrawler()
        {
            // Prefer a brawler that has a PlayerCommandSource attached —
            // that's the locally controlled one. Fall back to first brawler
            // in the scene if no PlayerCommandSource exists yet.
            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            if (sources.Length > 0)
            {
                BrawlerController b = sources[0].GetComponent<BrawlerController>();
                if (b != null)
                {
                    _controlledBrawler = b;
                    return true;
                }
            }

            BrawlerController[] all = FindObjectsOfType<BrawlerController>();
            if (all.Length > 0)
            {
                _controlledBrawler = all[0];
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (_controlledBrawler == null || _controlledBrawler.State == null)
            {
                TryAutoDiscoverBrawlerIfDue();
                return;
            }

            BrawlerState state = _controlledBrawler.State;

            UpdateAmmo(state);
            UpdateSuper(state);
            UpdateHypercharge(state);
            UpdateGadget(state);
            UpdateCarrierBadge(state);
        }

        private void TryAutoDiscoverBrawlerIfDue()
        {
            float now = Time.unscaledTime;
            if (now < _nextAutoDiscoverTime)
                return;

            _nextAutoDiscoverTime = now + Mathf.Max(0.1f, _autoDiscoverIntervalSeconds);
            AutoDiscoverBrawler();
        }

        private void UpdateAmmo(BrawlerState state)
        {
            ResourceStorage ammo = state.Ammo;
            if (ammo == null)
                return;

            // CurrentAmmo is a float (e.g. 1.7 = "one full bar + 70% into the
            // next reload"). AvailableBars is the integer floor.
            float current = ammo.CurrentAmmo;
            int available = ammo.AvailableBars;
            int maxBars = Mathf.Max(1, ammo.MaxAmmo);

            SetText(_ammoCountTmp, _ammoCountLegacy, $"{Mathf.Clamp(available, 0, maxBars)}/{maxBars}");

            if (_ammoSlots == null || _ammoSlots.Length == 0)
                return;

            for (int i = 0; i < _ammoSlots.Length; i++)
            {
                Image slot = _ammoSlots[i];
                GameObject slotRoot = GetAmmoSlotRoot(i);

                if (i >= maxBars)
                {
                    // Slot is beyond the brawler's max ammo (e.g. equipped a
                    // 3-bar weapon, HUD has 4 dots). Hide the surplus.
                    SetActive(slotRoot, false);
                    if (slot != null)
                        slot.enabled = false;
                    continue;
                }

                SetActive(slotRoot, true);
                if (slot == null)
                    continue;

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

            float chargePercent = Mathf.Clamp01(sc.ChargePercent);
            if (_superFill != null)
                _superFill.fillAmount = chargePercent;

            SetText(
                _superChargeTmp,
                _superChargeLegacy,
                sc.IsReady ? "READY" : $"{Mathf.RoundToInt(chargePercent * 100f)}%");

            if (_superReadyVisual != null && _superReadyVisual.activeSelf != sc.IsReady)
                _superReadyVisual.SetActive(sc.IsReady);
        }

        private void UpdateHypercharge(BrawlerState state)
        {
            bool hasHypercharge = state.GetCurrentHyperchargeDefinition() != null;
            SetActive(_hyperchargeRoot, true);
            if (!hasHypercharge)
            {
                if (_hyperchargeFill != null)
                    _hyperchargeFill.fillAmount = 0f;

                SetText(
                    _hyperchargeChargeTmp,
                    _hyperchargeChargeLegacy,
                    "0%");

                if (_hyperchargeActiveVisual != null && _hyperchargeActiveVisual.activeSelf)
                    _hyperchargeActiveVisual.SetActive(false);

                return;
            }

            HyperchargeTracker hc = state.Hypercharge;
            if (hc == null) return;

            uint currentTick = 0;
            if (ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock) && clock != null)
                currentTick = clock.CurrentTick;

            float chargePercent = hc.IsActive
                ? Mathf.Clamp01(hc.GetRemainingPercent(currentTick))
                : Mathf.Clamp01(hc.ChargePercent);

            if (_hyperchargeFill != null)
                _hyperchargeFill.fillAmount = chargePercent;

            string displayText = hc.IsActive
                ? $"{hc.GetRemainingSeconds(currentTick):0.0}s"
                : hc.ChargePercent >= 0.999f
                    ? "READY"
                    : $"{Mathf.RoundToInt(chargePercent * 100f)}%";

            SetText(
                _hyperchargeChargeTmp,
                _hyperchargeChargeLegacy,
                displayText);

            if (_hyperchargeActiveVisual != null)
            {
                bool highlighted = hc.IsActive || (!hc.IsActive && hc.ChargePercent >= 0.999f);
                if (_hyperchargeActiveVisual.activeSelf != highlighted)
                    _hyperchargeActiveVisual.SetActive(highlighted);
            }
        }

        private void UpdateGadget(BrawlerState state)
        {
            bool hasGadget = state.GetCurrentGadgetDefinition() != null;
            SetActive(_gadgetRoot, hasGadget);
            if (!hasGadget)
                return;

            int charges = state.RemainingGadgets;

            // True radial sweep: GetProgress returns 1 at cooldown start,
            // decaying linearly to 0 when the ability is ready.
            uint currentTick = 0;
            if (ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock) && clock != null)
                currentTick = clock.CurrentTick;

            float cdRatio = state.GadgetCooldown.GetProgress(currentTick);
            bool onCooldown = cdRatio > 0.001f;

            if (_gadgetCooldownOverlay != null)
                _gadgetCooldownOverlay.fillAmount = cdRatio;

            string countStr = charges.ToString();
            SetText(_gadgetChargesTmp, _gadgetChargesLegacy, countStr);

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
            SetText(_carriedGemCountTmp, _carriedGemCountLegacy, text);
        }

        private GameObject GetAmmoSlotRoot(int index)
        {
            return _ammoSlotRoots != null &&
                   index >= 0 &&
                   index < _ammoSlotRoots.Length
                ? _ammoSlotRoots[index]
                : null;
        }

        private static void SetActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }

        private static void SetText(TMP_Text tmp, Text legacy, string text)
        {
            if (tmp != null)
                tmp.text = text;
            else if (legacy != null)
                legacy.text = text;
        }
    }
}
