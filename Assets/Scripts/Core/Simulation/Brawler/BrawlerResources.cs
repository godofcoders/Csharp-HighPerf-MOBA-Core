namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the brawler's depletable/rechargeable resources:
    ///   - Ammo (primary attack charges that regenerate over time)
    ///   - Hypercharge (build-up meter that activates a temporary empowered state)
    ///   - SuperCharge (super ability build-up meter)
    ///   - RemainingGadgets (integer count of uses left)
    ///
    /// Unlike BrawlerStats (which absorbed raw fields), this substate uses the
    /// **aggregation** pattern — it holds references to existing subsystem
    /// classes (ResourceStorage, HyperchargeTracker, SuperChargeTracker) and
    /// coordinates them. Each subsystem already knows its own rules; this
    /// class is just one-stop shopping for "all the resource-y things."
    ///
    /// POCO: no Unity types, no events, no Debug.Log. RemainingGadgets gets
    /// set from outside (by RefreshGadgetChargesFromRuntimeKit on the
    /// coordinator) because the cap depends on the equipped gadget definition
    /// — that's loadout knowledge and doesn't belong here.
    /// </summary>
    public class BrawlerResources
    {
        public ResourceStorage Ammo { get; }
        public HyperchargeTracker Hypercharge { get; }
        public SuperChargeTracker SuperCharge { get; }
        public int RemainingGadgets { get; private set; }

        public BrawlerResources()
        {
            Ammo = new ResourceStorage(3, 0.5f);
            Hypercharge = new HyperchargeTracker();
            SuperCharge = new SuperChargeTracker();
            RemainingGadgets = 0;
        }

        // ---------- Tick ----------

        /// <summary>Advances real-time resources (currently just ammo regen).</summary>
        public void Tick(float deltaTime)
        {
            Ammo.Tick(deltaTime);
        }

        // ---------- Gadget charges ----------

        /// <summary>
        /// Sets the current gadget charge count (used when loadout refreshes
        /// tell us "the equipped gadget gives N max charges"). Clamps to zero
        /// to avoid ever going negative.
        /// </summary>
        public void SetGadgetCharges(int count)
        {
            if (count < 0)
                count = 0;

            RemainingGadgets = count;
        }

        /// <summary>Decrements gadget charges by one. No-op if already at zero.</summary>
        public void UseGadgetCharge()
        {
            if (RemainingGadgets > 0)
                RemainingGadgets--;
        }

        // ---------- Super charge ----------

        public void AddSuperCharge(float amount)
        {
            SuperCharge.AddCharge(amount);
        }

        public bool TryConsumeSuper()
        {
            return SuperCharge.TryConsume();
        }

        // ---------- Respawn / reset helpers ----------

        /// <summary>Refills ammo to full (used on respawn).</summary>
        public void RefillAmmo()
        {
            Ammo.Refill();
        }

        /// <summary>Clears the hypercharge meter and any active hyper state in place.</summary>
        public void ResetHypercharge()
        {
            Hypercharge.Reset();
        }

        /// <summary>Resets the super charge meter. `startFull` grants immediate super readiness.</summary>
        public void ResetSuperCharge(bool startFull)
        {
            SuperCharge.Reset(startFull);
        }
    }
}
