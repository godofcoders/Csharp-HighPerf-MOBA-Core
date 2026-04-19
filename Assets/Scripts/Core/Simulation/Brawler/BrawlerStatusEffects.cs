using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the brawler's active status-effect list and the pure queries over
    /// it ("am I silenced?", "am I attack-locked?", etc.). This substate is a
    /// POCO — no Unity types, no event bus, no combat log, no services.
    ///
    /// Coordinator split:
    ///   - PURE here:   the list, HasStatus, the five lock convenience checks,
    ///                  ticking each effect and removing expired ones.
    ///   - COORDINATOR: firing StatusEffectEventBus.RaiseRemoved + writing to
    ///                  the combat log when an effect ends. That's a side
    ///                  effect against the outside world, so it stays in
    ///                  BrawlerState.TickEffects(...).
    ///
    /// To keep the side effects out of this class without losing the info
    /// about *which* effects expired this tick, TickAndCollectExpired uses a
    /// **caller-owned out-list**: the coordinator passes in a buffer, we
    /// append every effect we removed to it, and the coordinator walks the
    /// buffer afterwards to fire its events. This avoids both (a) allocating
    /// a fresh list every tick (the buffer is reused) and (b) coupling this
    /// POCO to event/log services it shouldn't know about.
    ///
    /// Note: IsMovementLocked stays on BrawlerState because it composes the
    /// status check with the separate IsStunned flag (which lives on the
    /// coordinator). HasMovementLockStatus below is the pure status-only half
    /// of that question.
    /// </summary>
    public class BrawlerStatusEffects
    {
        /// <summary>
        /// The live list of active effects. Exposed because the IStatusTarget
        /// interface contract requires a List&lt;IStatusEffectInstance&gt;
        /// getter, and StatusEffectService needs to mutate this list when it
        /// applies new effects. BrawlerState pass-through preserves the
        /// existing API surface.
        /// </summary>
        public List<IStatusEffectInstance> Active { get; }

        public BrawlerStatusEffects()
        {
            Active = new List<IStatusEffectInstance>(8);
        }

        /// <summary>True if any active effect has the given type.</summary>
        public bool HasStatus(StatusEffectType type)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].Type == type)
                    return true;
            }

            return false;
        }

        // ---------- Convenience wrappers around HasStatus ----------
        // These exist so call sites read like English ("HasSilence()" instead
        // of "HasStatus(StatusEffectType.Silence)"), and so the magic enum
        // values aren't sprinkled across the codebase.

        public bool HasSilence() => HasStatus(StatusEffectType.Silence);
        public bool HasAttackLock() => HasStatus(StatusEffectType.AttackLock);
        public bool HasGadgetLock() => HasStatus(StatusEffectType.GadgetLock);
        public bool HasSuperLock() => HasStatus(StatusEffectType.SuperLock);

        /// <summary>
        /// Pure status-only movement-lock check. The full "am I movement
        /// locked?" question on BrawlerState also OR's in the IsStunned flag,
        /// which is coordinator-owned, so that composition stays there.
        /// </summary>
        public bool HasMovementLockStatus() => HasStatus(StatusEffectType.MovementLock);

        // ---------- Tick / removal ----------

        /// <summary>
        /// Ticks every active effect and removes any that have expired,
        /// appending each removed instance to <paramref name="removedOut"/>.
        /// The caller (BrawlerState) walks that buffer afterwards to fire
        /// event-bus / combat-log side effects — this method itself only
        /// touches the list and the effects' own Tick/Remove methods.
        ///
        /// The buffer is caller-owned and is NOT cleared here; the coordinator
        /// is expected to clear it before passing it in. That lets the
        /// coordinator reuse the same buffer across ticks (zero alloc) while
        /// keeping ownership explicit.
        /// </summary>
        public void TickAndCollectExpired(
            IStatusTarget target,
            uint currentTick,
            List<IStatusEffectInstance> removedOut)
        {
            // Iterate backwards so RemoveAt(i) is safe within the loop.
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                IStatusEffectInstance effect = Active[i];
                effect.Tick(target, currentTick);

                if (effect.IsExpired(currentTick))
                {
                    effect.Remove(target, currentTick);
                    Active.RemoveAt(i);
                    removedOut?.Add(effect);
                }
            }
        }

        /// <summary>Drops every active effect without firing remove hooks. Used on Reset (respawn).</summary>
        public void Clear()
        {
            Active.Clear();
        }
    }
}
