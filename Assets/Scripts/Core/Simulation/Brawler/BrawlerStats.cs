using System;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// The brawler's numeric state: modifiable stats, current health, shield,
    /// and the damage/movement modifier pipelines that feed into combat.
    ///
    /// This class is POCO (plain old C# object) — no Unity dependencies, no
    /// event firing, no presentation hooks. That makes it trivially unit-testable:
    /// new up a BrawlerStats, poke at it, assert.
    ///
    /// Side effects (event firing, Debug.Log, death-state transitions) stay in
    /// BrawlerState. This class only answers questions about numbers and
    /// performs the math that mutates them.
    /// </summary>
    public class BrawlerStats
    {
        // The three primary stats. Exposed as shared references so callers like
        // "hypercharge buff applies a multiplicative damage mod" can do
        // stats.Damage.AddModifier(...) directly, matching the pre-refactor API.
        public ModifiableStat MaxHealth { get; }
        public ModifiableStat MoveSpeed { get; }
        public ModifiableStat Damage { get; }

        // Current health and shield pool. Setters are private — all mutations go
        // through the methods below, which enforce invariants like
        // "CurrentHealth is clamped to [0, MaxHealth.Value]".
        public float CurrentHealth { get; private set; }
        public float ShieldHealth { get; private set; }

        public bool IsDead => CurrentHealth <= 0f;

        // Per-direction modifier pipelines. Incoming ones affect damage the
        // brawler receives; outgoing ones affect damage they deal.
        public DamageModifierCollection IncomingDamageModifiers { get; }
        public DamageModifierCollection OutgoingDamageModifiers { get; }
        public MovementModifierCollection IncomingMovementModifiers { get; }

        public BrawlerStats()
        {
            MaxHealth = new ModifiableStat(0f);
            MoveSpeed = new ModifiableStat(0f);
            Damage = new ModifiableStat(0f);

            IncomingDamageModifiers = new DamageModifierCollection();
            OutgoingDamageModifiers = new DamageModifierCollection();
            IncomingMovementModifiers = new MovementModifierCollection();

            CurrentHealth = 0f;
            ShieldHealth = 0f;
        }

        // ---------- Health mutation ----------

        /// <summary>
        /// Applies damage to CurrentHealth, clamping at zero. Returns true if
        /// this damage caused the brawler to transition from alive to dead on
        /// THIS call (useful for the caller to fire death side effects exactly
        /// once).
        /// </summary>
        public bool ApplyDamage(float amount)
        {
            if (IsDead)
                return false;

            CurrentHealth -= amount;
            if (CurrentHealth < 0f)
                CurrentHealth = 0f;

            return IsDead;
        }

        /// <summary>
        /// Heals by the given amount, clamped at MaxHealth. No-op if dead —
        /// reviving the dead is a lifecycle concern that belongs elsewhere.
        /// </summary>
        public void ApplyHeal(float amount)
        {
            if (IsDead)
                return;

            CurrentHealth += amount;
            float max = MaxHealth.Value;
            if (CurrentHealth > max)
                CurrentHealth = max;
        }

        /// <summary>
        /// Directly sets current health, clamped to [0, MaxHealth.Value].
        /// Used by stat-refresh flows (e.g. power level changes that recalculate
        /// MaxHealth and want to preserve the health ratio).
        /// </summary>
        public void SetCurrentHealth(float value)
        {
            float max = MaxHealth.Value;
            if (value < 0f)
                value = 0f;
            else if (value > max)
                value = max;

            CurrentHealth = value;
        }

        /// <summary>Refills current health to the current MaxHealth.Value.</summary>
        public void ResetHealthToMax()
        {
            CurrentHealth = MaxHealth.Value;
        }

        // ---------- Shield ----------

        public void AddShield(float amount)
        {
            if (amount <= 0f)
                return;

            ShieldHealth += amount;
        }

        public void ClearShield()
        {
            ShieldHealth = 0f;
        }

        // ---------- Incoming damage modifiers ----------

        public void AddIncomingDamageModifier(DamageModifier modifier)
        {
            IncomingDamageModifiers.Add(modifier);
        }

        public void RemoveIncomingDamageModifiersFromSource(object source)
        {
            IncomingDamageModifiers.RemoveBySource(source);
        }

        // ---------- Outgoing damage modifiers ----------

        public void AddOutgoingDamageModifier(DamageModifier modifier)
        {
            OutgoingDamageModifiers.Add(modifier);
        }

        public void RemoveOutgoingDamageModifiersFromSource(object source)
        {
            OutgoingDamageModifiers.RemoveBySource(source);
        }

        // ---------- Movement modifiers ----------

        public void AddIncomingMovementModifier(MovementModifier modifier)
        {
            IncomingMovementModifiers.Add(modifier);
        }

        public void RemoveIncomingMovementModifiersFromSource(object source)
        {
            IncomingMovementModifiers.RemoveBySource(source);
        }

        // ---------- Stat-modifier housekeeping ----------

        /// <summary>
        /// Removes every stat modifier from the given source across all three
        /// primary stats. Useful when a buff-source (e.g. Hypercharge) ends and
        /// wants to cleanly retract every modifier it contributed.
        /// </summary>
        public void RemoveAllStatModifiersFromSource(object source)
        {
            MaxHealth.RemoveModifiersFromSource(source);
            MoveSpeed.RemoveModifiersFromSource(source);
            Damage.RemoveModifiersFromSource(source);
        }

        /// <summary>
        /// Full modifier wipe — damage (both directions), movement, and shield.
        /// Called on brawler Reset (respawn). Does NOT touch the primary stat
        /// modifiers themselves, because those are driven by passive install/
        /// uninstall which happens elsewhere in the reset flow.
        /// </summary>
        public void ClearAllModifiers()
        {
            IncomingDamageModifiers.Clear();
            OutgoingDamageModifiers.Clear();
            IncomingMovementModifiers.Clear();
            ShieldHealth = 0f;
        }
    }
}
