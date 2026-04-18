namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Owns the three per-ability cooldown timers (MainAttack, Super, Gadget)
    /// and the tiny set of operations that read or mutate them.
    ///
    /// Two design notes worth remembering:
    ///
    /// 1) AbilityCooldownState is a struct. If we exposed these cooldowns as
    ///    auto-properties (`{ get; private set; }`), calling a mutating method
    ///    through the property getter would mutate a *copy* and silently lose
    ///    the change — a classic C# foot-gun. Exposing them as public fields
    ///    on this reference-type class makes `MainAttack.StartCooldown(...)`
    ///    operate on the real memory location (an l-value), so the mutation
    ///    actually sticks.
    ///
    /// 2) POCO: no Unity types, no events, no Debug.Log. BrawlerState stays
    ///    the coordinator; this class just does cooldown math.
    /// </summary>
    public class BrawlerCooldowns
    {
        // Fields, not properties. See (1) above.
        public AbilityCooldownState MainAttack;
        public AbilityCooldownState Super;
        public AbilityCooldownState Gadget;

        /// <summary>True if the given slot's cooldown has elapsed.</summary>
        public bool IsReady(AbilityRuntimeSlot slot, uint currentTick)
        {
            switch (slot)
            {
                case AbilityRuntimeSlot.MainAttack:
                    return MainAttack.IsReady(currentTick);

                case AbilityRuntimeSlot.Super:
                    return Super.IsReady(currentTick);

                case AbilityRuntimeSlot.Gadget:
                    return Gadget.IsReady(currentTick);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Starts a cooldown on the given slot. Converts seconds -> ticks using
        /// the simulation's actual TPS via SimulationClock.TickDeltaTime
        /// (previously hardcoded `* 30f`, which would break silently if the
        /// tick rate ever changed — same fix we made in HyperchargeTracker).
        /// </summary>
        public void StartCooldown(AbilityRuntimeSlot slot, uint currentTick, float cooldownSeconds)
        {
            uint cooldownTicks = (uint)(cooldownSeconds / SimulationClock.TickDeltaTime);

            switch (slot)
            {
                case AbilityRuntimeSlot.MainAttack:
                    MainAttack.StartCooldown(currentTick, cooldownTicks);
                    break;

                case AbilityRuntimeSlot.Super:
                    Super.StartCooldown(currentTick, cooldownTicks);
                    break;

                case AbilityRuntimeSlot.Gadget:
                    Gadget.StartCooldown(currentTick, cooldownTicks);
                    break;
            }
        }

        /// <summary>Clears every cooldown timer back to zero.</summary>
        public void ResetAll()
        {
            MainAttack.Reset();
            Super.Reset();
            Gadget.Reset();
        }
    }
}
