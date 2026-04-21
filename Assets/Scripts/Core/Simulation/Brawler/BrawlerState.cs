using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class BrawlerState : IStatusTarget
    {
        public BrawlerDefinition Definition { get; private set; }
        public TeamType Team { get; private set; }
        public MOBA.Core.Simulation.AI.ThreatTracker ThreatTracker { get; private set; }
        public MOBA.Core.Simulation.AI.AssistTracker AssistTracker { get; private set; }

        // Session 3 refactor: all numeric state (max/move/damage stats, current
        // health, shield, and the three modifier collections) now lives inside
        // this POCO substate. The original fields are kept as pass-through
        // properties below so callers don't have to change a thing.
        public BrawlerStats Stats { get; private set; }

        public DamageModifierCollection IncomingDamageModifiers => Stats.IncomingDamageModifiers;
        public DamageModifierCollection OutgoingDamageModifiers => Stats.OutgoingDamageModifiers;
        public float ShieldHealth => Stats.ShieldHealth;
        public ModifiableStat MaxHealth => Stats.MaxHealth;
        public ModifiableStat MoveSpeed => Stats.MoveSpeed;
        public ModifiableStat Damage => Stats.Damage;

        // Session 3 refactor: ammo + super charge + hypercharge meter + gadget
        // charges live in this substate. Pass-through getters keep the public
        // API identical — external code doing State.Hypercharge.Tick(...) etc.
        // still works unchanged (those calls hit methods on the class-typed
        // subsystems, which are references even through a getter).
        public BrawlerResources Resources { get; private set; }

        public int RemainingGadgets => Resources.RemainingGadgets;
        public HyperchargeTracker Hypercharge => Resources.Hypercharge;
        public SuperChargeTracker SuperCharge => Resources.SuperCharge;

        public float CurrentHealth => Stats.CurrentHealth;
        public bool IsDead => Stats.IsDead;

        public Action OnDeath;
        public Action<float> OnHealthChanged;

        public ResourceStorage Ammo => Resources.Ammo;
        public bool IsStunned;

        // Session 3 refactor: bush / reveal / last-attack-tick live in the
        // Stealth substate. These are pass-through get/set properties because
        // external systems (VisibilitySystem, RevealEffect, BrawlerController)
        // write these fields directly — the existing API surface stays intact.
        public BrawlerStealth Stealth { get; private set; }

        public bool IsInBush
        {
            get => Stealth.IsInBush;
            set => Stealth.IsInBush = value;
        }

        public uint LastAttackTick
        {
            get => Stealth.LastAttackTick;
            set => Stealth.LastAttackTick = value;
        }

        public bool IsRevealed
        {
            get => Stealth.IsRevealed;
            set => Stealth.IsRevealed = value;
        }

        public BrawlerController Owner { get; set; }
        public MovementModifierCollection IncomingMovementModifiers => Stats.IncomingMovementModifiers;

        // Session 3 refactor: the active status-effect list and its pure
        // queries (HasStatus, HasSilence, HasAttackLock, etc.) live in this
        // substate. ActiveStatusEffects below is a pass-through to the
        // substate's list — this is load-bearing because IStatusTarget
        // requires a List getter and StatusEffectService mutates that list
        // directly when applying new effects.
        public BrawlerStatusEffects StatusEffects { get; private set; }

        public List<IStatusEffectInstance> ActiveStatusEffects => StatusEffects.Active;

        // Reused buffer for TickEffects so the event-bus side effects can run
        // outside the removal loop without allocating a new list every tick.
        // See BrawlerStatusEffects.TickAndCollectExpired for why the buffer
        // is coordinator-owned.
        private readonly List<IStatusEffectInstance> _tickRemovedBuffer =
            new List<IStatusEffectInstance>(4);

        // Session 3 refactor: action-state transitions (enter/clear/expire/
        // interrupt) live in this substate. The ActionState property below is
        // a pass-through getter — external code reading state.ActionState.* to
        // inspect the current state works unchanged.
        public BrawlerActionStateMachine ActionStateMachine { get; private set; }
        public BrawlerActionStateData ActionState => ActionStateMachine.Current;

        // Session 3 refactor: the three AbilityCooldownState timers now live
        // inside this substate. Fields (not properties) inside the POCO so the
        // struct mutations persist correctly — see BrawlerCooldowns for the
        // detailed explanation.
        public BrawlerCooldowns Cooldowns { get; private set; }

        // Pass-through getters preserve the public API. External code only
        // ever *reads* these (confirmed by grep), so returning a copy is fine.
        public AbilityCooldownState MainAttackCooldown => Cooldowns.MainAttack;
        public AbilityCooldownState SuperCooldown => Cooldowns.Super;
        public AbilityCooldownState GadgetCooldown => Cooldowns.Gadget;

        // Session 3 refactor: power level, equipped passives + their
        // installed runtimes, RuntimeBuild, RuntimeKit, EquippedHypercharge,
        // and the hypercharge-modifier token all live in this substate. Every
        // external read goes through a pass-through getter below — external
        // callers doing State.RuntimeBuild.SetEquippedGadget(...) or
        // State.RuntimeKit.SetMainAttack(...) still work unchanged because
        // the pass-throughs return the same reference instances the POCO
        // owns.
        public BrawlerLoadout Loadout { get; private set; }

        public int CurrentPowerLevel => Loadout.CurrentPowerLevel;
        public IReadOnlyList<PassiveDefinition> EquippedPassives => Loadout.EquippedPassives;
        public HyperchargeDefinition EquippedHypercharge => Loadout.EquippedHypercharge;
        public object HyperchargeModifierSource => Loadout.HyperchargeModifierSource;
        public BrawlerRuntimeBuildState RuntimeBuild => Loadout.RuntimeBuild;
        public BrawlerRuntimeKit RuntimeKit => Loadout.RuntimeKit;

        public int EntityID => Owner != null ? Owner.EntityID : 0;

        public BrawlerState(BrawlerDefinition definition, TeamType team)
        {
            Definition = definition;
            Team = team;

            // Stats = all numeric state. Creating it here wires up the three
            // ModifiableStats, the two damage modifier collections, the
            // movement modifier collection, and the shield pool in one shot.
            Stats = new BrawlerStats();
            Cooldowns = new BrawlerCooldowns();
            // The state machine's ctor already calls Clear(), so by the time
            // we reach ClearActionState() below it's a second (harmless) reset.
            ActionStateMachine = new BrawlerActionStateMachine();
            Resources = new BrawlerResources();
            Stealth = new BrawlerStealth();

            ThreatTracker = new MOBA.Core.Simulation.AI.ThreatTracker();
            AssistTracker = new MOBA.Core.Simulation.AI.AssistTracker();
            StatusEffects = new BrawlerStatusEffects();

            // Loadout's ctor wires up RuntimeBuild, RuntimeKit, sets
            // CurrentPowerLevel = 1, and mints the HyperchargeModifierSource
            // token — one line replaces the four separate initializations
            // that used to live here.
            Loadout = new BrawlerLoadout();

            RefreshRuntimeBuildUnlockState();
            RefreshGadgetChargesFromRuntimeKit();
            RebuildProgressionStats(false);
            Stats.ResetHealthToMax();

            ClearActionState();
            ResetAbilityCooldowns();

            // Super meter starts empty — it's earned in-match through the
            // configured SuperChargeSources (damage dealt, heal done, auto-
            // over-time, ally proximity, etc.). The "start full" test line
            // that used to live here is gone; re-enable via a test-only
            // hook if you need it for manual smoke tests.
            Resources.ResetSuperCharge(false);

            // Install the brawler's data-driven super-charge sources. Empty
            // array or null definition is a silent no-op — a brawler with
            // no sources configured simply never charges, which is the same
            // behaviour we had before damage-based charging existed.
            Loadout.InstallSuperChargeSources(this, Definition);
        }

        public void SetEquippedHypercharge(HyperchargeDefinition definition)
        {
            Loadout.SetEquippedHypercharge(definition);
        }

        public AbilityDefinition GetCurrentSuperDefinition()
        {
            // Composition that spans two substates — Loadout (RuntimeKit +
            // EquippedHypercharge) and Resources (Hypercharge.IsActive) —
            // stays on the coordinator. The "base case" lookup lives on
            // Loadout; the hypercharge override is layered on top here.
            AbilityDefinition baseSuper = Loadout.GetBaseSuperDefinition(Definition);

            if (Hypercharge.IsActive &&
                EquippedHypercharge != null &&
                EquippedHypercharge.EnhancedSuper != null)
            {
                return EquippedHypercharge.EnhancedSuper;
            }

            return baseSuper;
        }

        public void ClearHyperchargeRuntimeModifiers()
        {
            MoveSpeed.RemoveModifiersFromSource(HyperchargeModifierSource);
            Damage.RemoveModifiersFromSource(HyperchargeModifierSource);
            RemoveIncomingDamageModifiersFromSource(HyperchargeModifierSource);
        }

        public void SetPowerLevel(int powerLevel, bool preserveHealthRatio = true)
        {
            // Loadout does the clamp + field update; coordinator then cascades
            // the three knock-on effects: slot-unlock refresh (Loadout),
            // progression stat rebuild (Stats), and passive reinstall (Loadout
            // + Stats again via health-restore).
            Loadout.SetPowerLevel(powerLevel);
            RefreshRuntimeBuildUnlockState();
            RebuildProgressionStats(preserveHealthRatio);
            RefreshPassiveLoadout(preserveHealthRatio);
        }

        public void SetPassiveLoadout(IEnumerable<PassiveDefinition> definitions, bool preserveHealthRatio = true)
        {
            // Coordinator brackets the swap with health measurement so the
            // preserve-ratio math still works across passives that modify
            // MaxHealth. The three middle steps are pure loadout bookkeeping.
            float oldMaxHealth = MaxHealth.Value;
            float oldHealth = CurrentHealth;

            Loadout.UninstallAll(this);
            Loadout.SetEquippedPassives(definitions);
            Loadout.InstallAll(this, Owner);

            RestoreHealthAfterStatRefresh(oldMaxHealth, oldHealth, preserveHealthRatio);
        }

        public void RefreshPassiveLoadout(bool preserveHealthRatio = true)
        {
            if (Loadout.EquippedPassives.Count == 0)
                return;

            float oldMaxHealth = MaxHealth.Value;
            float oldHealth = CurrentHealth;

            Loadout.UninstallAll(this);
            Loadout.InstallAll(this, Owner);

            RestoreHealthAfterStatRefresh(oldMaxHealth, oldHealth, preserveHealthRatio);
        }

        public void SetStarPowerLoadout(IEnumerable<PassiveDefinition> definitions, bool preserveHealthRatio = true)
        {
            SetPassiveLoadout(definitions, preserveHealthRatio);
        }

        public void RefreshStarPowerLoadout(bool preserveHealthRatio = true)
        {
            RefreshPassiveLoadout(preserveHealthRatio);
        }

        public void TickPassives(uint currentTick)
        {
            Loadout.TickPassives(this, currentTick);
        }

        private void RebuildProgressionStats(bool preserveHealthRatio)
        {
            float oldMaxHealth = MaxHealth.Value;
            float oldHealth = CurrentHealth;

            // Definition stays on BrawlerState (it's the whole brawler's
            // identity), current power level now lives on Loadout.
            var progression = Definition.GetProgressionBonus(Loadout.CurrentPowerLevel);

            MaxHealth.SetBaseValue(Definition.BaseHealth + progression.BonusHealth);
            MoveSpeed.SetBaseValue(Definition.BaseMoveSpeed + progression.BonusMoveSpeed);
            Damage.SetBaseValue(Definition.BaseDamage + progression.BonusDamage);

            RestoreHealthAfterStatRefresh(oldMaxHealth, oldHealth, preserveHealthRatio);
        }

        private void RestoreHealthAfterStatRefresh(float oldMaxHealth, float oldHealth, bool preserveHealthRatio)
        {
            float newMaxHealth = MaxHealth.Value;

            // We no longer poke CurrentHealth directly — Stats.SetCurrentHealth
            // enforces the [0, MaxHealth.Value] clamp. Computing the raw target
            // here and letting Stats do the clamping is simpler than the old
            // three-branch assignment, and behavior stays identical.
            float target = preserveHealthRatio && oldMaxHealth > 0f
                ? newMaxHealth * (oldHealth / oldMaxHealth)
                : CurrentHealth;

            Stats.SetCurrentHealth(target);

            OnHealthChanged?.Invoke(CurrentHealth);
        }

        public void RemoveAllStatModifiersFromSource(object source)
        {
            Stats.RemoveAllStatModifiersFromSource(source);
        }

        public void AddIncomingMovementModifier(MovementModifier modifier)
        {
            Stats.AddIncomingMovementModifier(modifier);
        }

        public void RemoveIncomingMovementModifiersFromSource(object source)
        {
            Stats.RemoveIncomingMovementModifiersFromSource(source);
        }

        public void UseGadgetCharge()
        {
            Resources.UseGadgetCharge();
        }

        public void AddSuperCharge(float amount)
        {
            Resources.AddSuperCharge(amount);
        }

        public bool TryConsumeSuper()
        {
            return Resources.TryConsumeSuper();
        }

        /// <summary>Per-tick update for every installed super-charge source runtime (auto-over-time, ally proximity, etc.).</summary>
        public void TickSuperChargeSources(float deltaTime, uint currentTick)
        {
            Loadout.TickSuperChargeSources(this, deltaTime, currentTick);
        }

        /// <summary>
        /// Fan a finalised damage-dealt event to every installed super-charge
        /// source runtime (DamageDealt runtime, future MainAttackOnly filter,
        /// etc.). Called once from <c>DamageService</c> after the damage hit
        /// has been fully resolved (post shields, post modifiers).
        /// </summary>
        public void NotifyDamageDealt(float damageAmount, BrawlerState victim)
        {
            Loadout.NotifyDamageDealt(this, damageAmount, victim);
        }

        /// <summary>
        /// Fan a finalised heal-applied event to every installed super-charge
        /// source runtime. Not wired into the heal pipeline yet — safe to
        /// call when that integration lands.
        /// </summary>
        public void NotifyHealApplied(float healAmount, BrawlerState recipient)
        {
            Loadout.NotifyHealApplied(this, healAmount, recipient);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead)
                return;

            float beforeHealth = CurrentHealth;

            // Stats.ApplyDamage does the math + clamp and returns true if THIS
            // call caused the transition alive -> dead. We keep the side
            // effects (debug log, events, action-state transition) here in
            // BrawlerState, the coordinator, so the POCO stays pure.
            bool justDied = Stats.ApplyDamage(amount);

            Debug.Log($"[DAMAGE] Target={Owner?.name ?? "Unknown"} Team={Team} Damage={amount} Health: {beforeHealth} -> {CurrentHealth}");

            OnHealthChanged?.Invoke(CurrentHealth);

            if (Owner != null)
            {
                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                {
                    EventType = BrawlerPresentationEventType.DamageTaken,
                    Source = Owner,
                    AbilityDefinition = null,
                    Position = Owner.transform.position,
                    Direction = Owner.transform.forward,
                    Value = amount,
                    Tick = ServiceProvider.Get<ISimulationClock>().CurrentTick
                });
            }

            if (justDied)
            {
                uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
                EnterActionState(
                    BrawlerActionStateType.Dead,
                    currentTick,
                    uint.MaxValue / 4,
                    false,
                    false,
                    false);

                if (Owner != null)
                {
                    BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                    {
                        EventType = BrawlerPresentationEventType.Died,
                        Source = Owner,
                        AbilityDefinition = null,
                        Position = Owner.transform.position,
                        Direction = Owner.transform.forward,
                        Value = 0f,
                        Tick = currentTick
                    });
                }

                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead)
                return;

            float beforeHealth = CurrentHealth;

            // Clamped heal is done inside Stats; we still own the logging and
            // event bus side effects so presentation hooks stay in the
            // coordinator layer.
            Stats.ApplyHeal(amount);

            Debug.Log($"[HEAL] Target={Owner?.name ?? "Unknown"} Team={Team} Heal={amount} Health: {beforeHealth} -> {CurrentHealth}");

            OnHealthChanged?.Invoke(CurrentHealth);

            if (Owner != null)
            {
                BrawlerPresentationEventBus.Raise(new BrawlerPresentationEvent
                {
                    EventType = BrawlerPresentationEventType.Healed,
                    Source = Owner,
                    AbilityDefinition = null,
                    Position = Owner.transform.position,
                    Direction = Owner.transform.forward,
                    Value = amount,
                    Tick = ServiceProvider.Get<ISimulationClock>().CurrentTick
                });
            }
        }

        public void UpdateResources(float deltaTime)
        {
            Resources.Tick(deltaTime);
        }

        /// <summary>
        /// Respawn-time reset. Called by BrawlerController.Respawn only.
        ///
        /// Semantics in plain English: "bring this brawler back to a fresh
        /// fighting state, keeping identity (Definition, Team, equipped
        /// passive *definitions*) but clearing everything transient." The
        /// steps are grouped below by concern; ordering matters where noted.
        ///
        /// This method deliberately clears RuntimeBuild and RuntimeKit via
        /// Loadout.ResetRuntimeState — installed ability logics are transient
        /// per-life instances, so a fresh respawn deserves fresh instances.
        /// BrawlerController.Respawn re-resolves and re-applies the current
        /// default build (ResolveAndApplyCurrentBuild) immediately after this
        /// returns, so the gadget / star power / gears are restored before
        /// the brawler is reactivated. See Session 4 of SESSIONS.md for the
        /// gap-close history (the previous Respawn implementation didn't
        /// re-apply the build, leaving brawlers without a gadget post-death).
        /// </summary>
        public void Reset()
        {
            // --- 1. Rebuild progression-driven base stats and refresh
            //        passive modifiers. Uninstall+reinstall also resets any
            //        transient runtime state that passives track internally
            //        (stacks, timers, etc.). Passive modifiers on
            //        MoveSpeed/Damage/MaxHealth survive through ClearAllModifiers
            //        below, because that method only wipes the damage,
            //        movement, and shield collections — not the three
            //        primary ModifiableStats.
            RebuildProgressionStats(false);
            RefreshPassiveLoadout(false);

            // --- 2. Restore pooled resources to full / fresh.
            Stats.ResetHealthToMax();
            Resources.RefillAmmo();
            RefreshGadgetChargesFromRuntimeKit();
            Resources.ResetHypercharge();     // in-place reset; references stay valid
            Resources.ResetSuperCharge(false); // respawn with empty meter — earned in-match via SuperChargeSources

            // Rebuild the super-charge source runtimes from the brawler's
            // definition. Reset() is the respawn path, and we want any
            // transient runtime state on a source (e.g. a hypothetical
            // combat-lull timer on AutoOverTimeChargeSource) to reset too.
            Loadout.UninstallAllSuperChargeSources(this);
            Loadout.InstallSuperChargeSources(this, Definition);

            // --- 3. Clear transient combat state.
            IsStunned = false;
            Stealth.Reset();
            ThreatTracker.Clear();
            AssistTracker.Clear();
            Stats.ClearAllModifiers();        // incoming/outgoing damage mods, movement mods, shield
            StatusEffects.Clear();
            ClearActionState();
            ResetAbilityCooldowns();

            // --- 4. Clear the runtime loadout slots (see KNOWN GAP above).
            //        Encapsulated in one POCO call so the "what gets wiped
            //        on respawn" decision has one home.
            Loadout.ResetRuntimeState(Definition);

            // --- 5. Clear hypercharge-tagged stat modifiers. Must come last:
            //        earlier steps (RebuildProgressionStats, RefreshPassiveLoadout)
            //        operate on MoveSpeed/Damage/IncomingDamage, and we want
            //        to leave those collections with only the contributions
            //        that survive respawn. This call strips anything tagged
            //        with the HyperchargeModifierSource token from those
            //        three collections.
            ClearHyperchargeRuntimeModifiers();

            OnHealthChanged?.Invoke(CurrentHealth);
        }

        public bool DoesActionRequireResource(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                case BrawlerActionRequestType.Gadget:
                case BrawlerActionRequestType.Super:
                case BrawlerActionRequestType.Hypercharge:
                    return true;

                default:
                    return false;
            }
        }

        public bool CanPayActionCost(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    return Ammo != null && Ammo.AvailableBars >= 1;

                case BrawlerActionRequestType.Gadget:
                    return RemainingGadgets > 0;

                case BrawlerActionRequestType.Super:
                    return SuperCharge != null && SuperCharge.IsReady;

                case BrawlerActionRequestType.Hypercharge:
                    return Hypercharge != null && Hypercharge.ChargePercent >= 1f;

                default:
                    return false;
            }
        }

        public bool TryConsumeActionCost(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    return Ammo != null && Ammo.Consume(1);

                case BrawlerActionRequestType.Gadget:
                    if (RemainingGadgets <= 0)
                        return false;

                    UseGadgetCharge();
                    return true;

                case BrawlerActionRequestType.Super:
                    return TryConsumeSuper();

                case BrawlerActionRequestType.Hypercharge:
                    return Hypercharge != null && Hypercharge.ChargePercent >= 1f;

                default:
                    return false;
            }
        }

        public string GetActionResourceName(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack: return "Ammo";
                case BrawlerActionRequestType.Gadget: return "Gadget Charges";
                case BrawlerActionRequestType.Super: return "Super Charge";
                case BrawlerActionRequestType.Hypercharge: return "Hypercharge Charge";
                default: return "None";
            }
        }

        public void TickEffects(uint currentTick)
        {
            // Delegate the pure bookkeeping (tick each effect, remove expired
            // ones, collect which were removed) to the POCO, which knows
            // nothing about event buses or combat logs. The reused buffer
            // must be cleared before each call — BrawlerStatusEffects
            // intentionally appends without clearing.
            _tickRemovedBuffer.Clear();
            StatusEffects.TickAndCollectExpired(this, currentTick, _tickRemovedBuffer);

            if (_tickRemovedBuffer.Count == 0)
                return;

            // Side effects for anything that expired this tick: raise the
            // status-effect event and write a combat-log entry. Kept in the
            // coordinator because touching global services is not the POCO's
            // job.
            var combatLog = ServiceProvider.Get<ICombatLogService>();
            for (int i = 0; i < _tickRemovedBuffer.Count; i++)
            {
                IStatusEffectInstance effect = _tickRemovedBuffer[i];

                var result = new StatusEffectResult
                {
                    Context = new StatusEffectContext
                    {
                        Source = null,
                        Target = this,
                        Type = effect.Type,
                        Duration = 0f,
                        Magnitude = 0f,
                        Origin = default,
                        SourceToken = null
                    },
                    Applied = false,
                    Refreshed = false
                };

                StatusEffectEventBus.RaiseRemoved(result);
                combatLog.AddEntry(CombatLogEntry.CreateStatusRemoved(currentTick, result));
            }
        }

        public bool IsHiddenTo(TeamType observerTeam)
        {
            // Allies always see allies.
            if (observerTeam == Team)
                return false;

            // The pure "am I hidden right now" question belongs to the
            // stealth substate; we just ask it with the current sim tick.
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            return Stealth.IsHidden(currentTick);
        }

        public void AddShield(float amount)
        {
            Stats.AddShield(amount);
        }

        public void ClearShield()
        {
            Stats.ClearShield();
        }

        public void EnterActionState(
            BrawlerActionStateType stateType,
            uint currentTick,
            uint durationTicks,
            bool allowMovement,
            bool allowActionInput,
            bool isInterruptible)
        {
            ActionStateMachine.Enter(stateType, currentTick, durationTicks, allowMovement, allowActionInput, isInterruptible);
        }

        public void ClearActionState()
        {
            ActionStateMachine.Clear();
        }

        public void UpdateActionState(uint currentTick)
        {
            ActionStateMachine.UpdateExpiry(currentTick);
        }

        public bool HasActiveActionState(uint currentTick)
        {
            return ActionStateMachine.IsActive(currentTick);
        }

        public bool IsAbilityReady(AbilityRuntimeSlot slot, uint currentTick)
        {
            return Cooldowns.IsReady(slot, currentTick);
        }

        public void StartAbilityCooldown(AbilityRuntimeSlot slot, uint currentTick, float cooldownSeconds)
        {
            Cooldowns.StartCooldown(slot, currentTick, cooldownSeconds);
        }

        public void ResetAbilityCooldowns()
        {
            Cooldowns.ResetAll();
        }
        public bool CanMove(uint currentTick)
        {
            if (IsMovementLocked())
                return false;

            if (!HasActiveActionState(currentTick))
                return !IsDead;

            return ActionState.AllowMovement && !IsDead;
        }

        public bool CanUseActionInput(uint currentTick)
        {
            if (!HasActiveActionState(currentTick))
                return !IsDead && !IsStunned;

            return ActionState.AllowActionInput && !IsDead && !IsStunned;
        }

        public bool TryInterruptActionState()
        {
            return ActionStateMachine.TryInterrupt();
        }

        public void AddIncomingDamageModifier(DamageModifier modifier)
        {
            Stats.AddIncomingDamageModifier(modifier);
        }

        public void AddOutgoingDamageModifier(DamageModifier modifier)
        {
            Stats.AddOutgoingDamageModifier(modifier);
        }

        public void RemoveIncomingDamageModifiersFromSource(object source)
        {
            Stats.RemoveIncomingDamageModifiersFromSource(source);
        }

        public void RemoveOutgoingDamageModifiersFromSource(object source)
        {
            Stats.RemoveOutgoingDamageModifiersFromSource(source);
        }

        public bool HasStatus(StatusEffectType type)
        {
            return StatusEffects.HasStatus(type);
        }

        public bool IsInActionState(BrawlerActionStateType type, uint currentTick)
        {
            return ActionStateMachine.IsInState(type, currentTick);
        }

        public void RefreshRuntimeBuildUnlockState()
        {
            Loadout.RefreshRuntimeBuildUnlockState(Definition);
        }

        public bool HasUnlockedGadgetSlot() => Loadout.HasUnlockedGadgetSlot();
        public bool HasUnlockedStarPowerSlot() => Loadout.HasUnlockedStarPowerSlot();
        public bool HasUnlockedHyperchargeSlot() => Loadout.HasUnlockedHyperchargeSlot();
        public bool HasAnyUnlockedGearSlot() => Loadout.HasAnyUnlockedGearSlot();

        public AbilityDefinition GetCurrentMainAttackDefinition()
        {
            return Loadout.GetCurrentMainAttackDefinition(Definition);
        }

        public AbilityDefinition GetBaseSuperDefinition()
        {
            return Loadout.GetBaseSuperDefinition(Definition);
        }

        public GadgetDefinition GetCurrentGadgetDefinition()
        {
            return Loadout.GetCurrentGadgetDefinition();
        }

        public HyperchargeDefinition GetCurrentHyperchargeDefinition()
        {
            return Loadout.GetCurrentHyperchargeDefinition();
        }

        public BrawlerActionBlockReason GetMainAttackBlockReason(uint currentTick)
        {
            if (IsDead)
                return BrawlerActionBlockReason.Dead;

            if (HasSilence())
                return BrawlerActionBlockReason.Silenced;

            if (IsMainAttackLocked())
                return BrawlerActionBlockReason.AttackLocked;

            if (!CanUseActionInput(currentTick))
                return BrawlerActionBlockReason.ActionLocked;

            if (!IsAbilityReady(AbilityRuntimeSlot.MainAttack, currentTick))
                return BrawlerActionBlockReason.AbilityCooldown;

            if (Ammo == null || Ammo.AvailableBars < 1)
                return BrawlerActionBlockReason.NoAmmo;

            return BrawlerActionBlockReason.None;
        }

        public BrawlerActionBlockReason GetGadgetBlockReason(uint currentTick)
        {
            if (IsDead)
                return BrawlerActionBlockReason.Dead;

            if (HasSilence())
                return BrawlerActionBlockReason.Silenced;

            if (IsGadgetLocked())
                return BrawlerActionBlockReason.GadgetLocked;

            if (!CanUseActionInput(currentTick))
                return BrawlerActionBlockReason.ActionLocked;

            if (!IsAbilityReady(AbilityRuntimeSlot.Gadget, currentTick))
                return BrawlerActionBlockReason.AbilityCooldown;

            if (RemainingGadgets <= 0)
                return BrawlerActionBlockReason.NoGadgetCharges;

            return BrawlerActionBlockReason.None;
        }

        public BrawlerActionBlockReason GetSuperBlockReason(uint currentTick)
        {
            if (IsDead)
                return BrawlerActionBlockReason.Dead;

            if (HasSilence())
                return BrawlerActionBlockReason.Silenced;

            if (IsSuperLocked())
                return BrawlerActionBlockReason.SuperLocked;

            if (!CanUseActionInput(currentTick))
                return BrawlerActionBlockReason.ActionLocked;

            if (!IsAbilityReady(AbilityRuntimeSlot.Super, currentTick))
                return BrawlerActionBlockReason.AbilityCooldown;

            if (SuperCharge == null || !SuperCharge.IsReady)
                return BrawlerActionBlockReason.SuperNotReady;

            return BrawlerActionBlockReason.None;
        }

        public BrawlerActionBlockReason GetBlockReasonForAction(BrawlerActionRequestType actionType, uint currentTick)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    return GetMainAttackBlockReason(currentTick);

                case BrawlerActionRequestType.Gadget:
                    return GetGadgetBlockReason(currentTick);

                case BrawlerActionRequestType.Super:
                    return GetSuperBlockReason(currentTick);

                case BrawlerActionRequestType.Hypercharge:
                    return GetHyperchargeBlockReason(currentTick);

                default:
                    return BrawlerActionBlockReason.MissingDefinition;
            }
        }

        public bool CanUseActionNow(BrawlerActionRequestType actionType, out BrawlerActionBlockReason blockReason)
        {
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            blockReason = GetBlockReasonForAction(actionType, currentTick);
            return blockReason == BrawlerActionBlockReason.None;
        }

        public bool CanUseAction(BrawlerActionRequestType actionType, uint currentTick)
        {
            return GetBlockReasonForAction(actionType, currentTick) == BrawlerActionBlockReason.None;
        }

        public BrawlerActionBlockReason GetHyperchargeBlockReason(uint currentTick)
        {
            if (IsDead)
                return BrawlerActionBlockReason.Dead;

            if (HasSilence())
                return BrawlerActionBlockReason.Silenced;

            if (!CanUseActionInput(currentTick))
                return BrawlerActionBlockReason.ActionLocked;

            if (Hypercharge == null || Hypercharge.ChargePercent < 1f)
                return BrawlerActionBlockReason.HyperchargeNotReady;

            return BrawlerActionBlockReason.None;
        }

        public bool CanUseHypercharge(uint currentTick)
        {
            return GetHyperchargeBlockReason(currentTick) == BrawlerActionBlockReason.None;
        }

        public bool CanUseMainAttack(uint currentTick)
        {
            return GetMainAttackBlockReason(currentTick) == BrawlerActionBlockReason.None;
        }

        public bool CanUseGadget(uint currentTick)
        {
            return GetGadgetBlockReason(currentTick) == BrawlerActionBlockReason.None;
        }

        public bool CanUseSuper(uint currentTick)
        {
            return GetSuperBlockReason(currentTick) == BrawlerActionBlockReason.None;
        }

        public string GetActionBlockReasonText(BrawlerActionBlockReason reason)
        {
            switch (reason)
            {
                case BrawlerActionBlockReason.None: return "Ready";
                case BrawlerActionBlockReason.MissingDefinition: return "Missing Definition";
                case BrawlerActionBlockReason.Dead: return "Dead";
                case BrawlerActionBlockReason.ActionLocked: return "Action Locked";
                case BrawlerActionBlockReason.AbilityCooldown: return "Cooldown Active";
                case BrawlerActionBlockReason.NoAmmo: return "No Ammo";
                case BrawlerActionBlockReason.NoGadgetCharges: return "No Gadget Charges";
                case BrawlerActionBlockReason.SuperNotReady: return "Super Not Ready";
                case BrawlerActionBlockReason.HyperchargeNotReady: return "Hypercharge Not Ready";
                case BrawlerActionBlockReason.Silenced: return "Silenced";
                case BrawlerActionBlockReason.AttackLocked: return "Attack Locked";
                case BrawlerActionBlockReason.GadgetLocked: return "Gadget Locked";
                case BrawlerActionBlockReason.SuperLocked: return "Super Locked";
                case BrawlerActionBlockReason.MovementLocked: return "Movement Locked";
                default: return "Unknown";
            }
        }

        public BrawlerActionBlockReason GetMovementBlockReason(uint currentTick)
        {
            if (IsDead)
                return BrawlerActionBlockReason.Dead;

            if (IsMovementLocked())
                return BrawlerActionBlockReason.MovementLocked;

            if (HasActiveActionState(currentTick) && !ActionState.AllowMovement)
                return BrawlerActionBlockReason.ActionLocked;

            return BrawlerActionBlockReason.None;
        }

        public void RefreshGadgetChargesFromRuntimeKit()
        {
            // Loadout lookup (RuntimeKit + Definition) stays here in the
            // coordinator; the resulting count gets pushed into Resources.
            // This is the one seam between Loadout and Resources — kept small
            // and one-directional on purpose.
            GadgetDefinition gadget = RuntimeKit?.GadgetDefinition ?? Definition?.Gadget;
            int charges = gadget != null ? gadget.MaxCharges : 0;
            Resources.SetGadgetCharges(charges);
        }

        public bool DoesActionConsumePrimaryAmmo(BrawlerActionRequestType actionType)
        {
            return actionType == BrawlerActionRequestType.MainAttack;
        }

        public AbilityRuntimeSlot GetCooldownSlotForAction(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    return AbilityRuntimeSlot.MainAttack;

                case BrawlerActionRequestType.Gadget:
                    return AbilityRuntimeSlot.Gadget;

                case BrawlerActionRequestType.Super:
                    return AbilityRuntimeSlot.Super;

                default:
                    return AbilityRuntimeSlot.MainAttack;
            }
        }

        public float GetCooldownSecondsForAction(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                    return GetCurrentMainAttackDefinition()?.Cooldown ?? 0f;

                case BrawlerActionRequestType.Gadget:
                    return GetCurrentGadgetDefinition()?.Cooldown ?? 0f;

                case BrawlerActionRequestType.Super:
                    return GetCurrentSuperDefinition()?.Cooldown ?? 0f;

                case BrawlerActionRequestType.Hypercharge:
                    return 0f;

                default:
                    return 0f;
            }
        }

        public bool DoesActionUseCooldown(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack:
                case BrawlerActionRequestType.Gadget:
                case BrawlerActionRequestType.Super:
                    return true;

                default:
                    return false;
            }
        }

        public void StartCooldownForAction(BrawlerActionRequestType actionType, uint currentTick)
        {
            if (!DoesActionUseCooldown(actionType))
                return;

            float cooldownSeconds = GetCooldownSecondsForAction(actionType);
            if (cooldownSeconds <= 0f)
                return;

            AbilityRuntimeSlot slot = GetCooldownSlotForAction(actionType);
            StartAbilityCooldown(slot, currentTick, cooldownSeconds);
        }

        public bool IsActionOnCooldown(BrawlerActionRequestType actionType, uint currentTick)
        {
            if (!DoesActionUseCooldown(actionType))
                return false;

            AbilityRuntimeSlot slot = GetCooldownSlotForAction(actionType);
            return !IsAbilityReady(slot, currentTick);
        }
        public string GetCooldownSlotName(BrawlerActionRequestType actionType)
        {
            switch (actionType)
            {
                case BrawlerActionRequestType.MainAttack: return "MainAttack";
                case BrawlerActionRequestType.Gadget: return "Gadget";
                case BrawlerActionRequestType.Super: return "Super";
                default: return "None";
            }
        }
        public bool HasSilence()
        {
            return StatusEffects.HasSilence();
        }

        public bool IsMainAttackLocked()
        {
            return StatusEffects.HasAttackLock();
        }

        public bool IsGadgetLocked()
        {
            return StatusEffects.HasGadgetLock();
        }

        public bool IsSuperLocked()
        {
            return StatusEffects.HasSuperLock();
        }

        // Composite: the pure status half lives on the POCO; IsStunned is a
        // separate coordinator-owned flag (set by stun status-effect instances
        // and by other systems), so the OR stays here.
        public bool IsMovementLocked()
        {
            return StatusEffects.HasMovementLockStatus() || IsStunned;
        }
        public bool CanReceiveStatusEffects()
        {
            return !IsDead;
        }
    }
}