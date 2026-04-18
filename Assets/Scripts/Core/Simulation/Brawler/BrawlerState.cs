using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public class BrawlerState : IStatusTarget
    {
        private struct InstalledPassive
        {
            public PassiveDefinition Definition;
            public PassiveInstallContext Context;
            public IPassiveRuntime Runtime;
        }

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

        public int RemainingGadgets { get; private set; }
        public HyperchargeTracker Hypercharge { get; private set; }
        public SuperChargeTracker SuperCharge { get; private set; }

        public float CurrentHealth => Stats.CurrentHealth;
        public bool IsDead => Stats.IsDead;

        public Action OnDeath;
        public Action<float> OnHealthChanged;

        public ResourceStorage Ammo { get; private set; }
        public bool IsStunned;

        public bool IsInBush { get; set; }
        public uint LastAttackTick { get; set; }
        public bool IsRevealed { get; set; }

        public BrawlerController Owner { get; set; }
        public MovementModifierCollection IncomingMovementModifiers => Stats.IncomingMovementModifiers;
        public List<IStatusEffectInstance> ActiveStatusEffects { get; private set; }

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

        public int CurrentPowerLevel { get; private set; }

        private readonly List<PassiveDefinition> _equippedPassives = new List<PassiveDefinition>(4);
        private readonly List<InstalledPassive> _installedPassives = new List<InstalledPassive>(4);

        public IReadOnlyList<PassiveDefinition> EquippedPassives => _equippedPassives;

        public HyperchargeDefinition EquippedHypercharge { get; private set; }
        public object HyperchargeModifierSource { get; } = new object();

        public BrawlerRuntimeBuildState RuntimeBuild { get; private set; }
        public BrawlerRuntimeKit RuntimeKit { get; private set; }

        public int EntityID => Owner != null ? Owner.EntityID : 0;

        public BrawlerState(BrawlerDefinition definition, TeamType team)
        {
            Definition = definition;
            Team = team;
            CurrentPowerLevel = 1;

            // Stats = all numeric state. Creating it here wires up the three
            // ModifiableStats, the two damage modifier collections, the
            // movement modifier collection, and the shield pool in one shot.
            Stats = new BrawlerStats();
            Cooldowns = new BrawlerCooldowns();
            // The state machine's ctor already calls Clear(), so by the time
            // we reach ClearActionState() below it's a second (harmless) reset.
            ActionStateMachine = new BrawlerActionStateMachine();

            Ammo = new ResourceStorage(3, 0.5f);
            Hypercharge = new HyperchargeTracker();
            SuperCharge = new SuperChargeTracker();
            ThreatTracker = new MOBA.Core.Simulation.AI.ThreatTracker();
            AssistTracker = new MOBA.Core.Simulation.AI.AssistTracker();
            ActiveStatusEffects = new List<IStatusEffectInstance>(8);
            RuntimeBuild = new BrawlerRuntimeBuildState();
            RuntimeKit = new BrawlerRuntimeKit();

            RefreshRuntimeBuildUnlockState();
            RefreshGadgetChargesFromRuntimeKit();
            RebuildProgressionStats(false);
            Stats.ResetHealthToMax();

            ClearActionState();
            ResetAbilityCooldowns();
            SuperCharge.Reset(true);
        }

        public void SetEquippedHypercharge(HyperchargeDefinition definition)
        {
            EquippedHypercharge = definition;
        }

        public AbilityDefinition GetCurrentSuperDefinition()
        {
            AbilityDefinition baseSuper = RuntimeKit?.SuperDefinition ?? Definition?.SuperAbility;

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
            if (powerLevel < 1)
                powerLevel = 1;

            CurrentPowerLevel = powerLevel;
            RefreshRuntimeBuildUnlockState();
            RebuildProgressionStats(preserveHealthRatio);
            RefreshPassiveLoadout(preserveHealthRatio);
        }

        public void SetPassiveLoadout(IEnumerable<PassiveDefinition> definitions, bool preserveHealthRatio = true)
        {
            float oldMaxHealth = MaxHealth.Value;
            float oldHealth = CurrentHealth;

            UninstallAllPassivesInternal();
            _equippedPassives.Clear();

            if (definitions != null)
            {
                foreach (PassiveDefinition definition in definitions)
                {
                    if (definition == null)
                        continue;

                    if (!_equippedPassives.Contains(definition))
                        _equippedPassives.Add(definition);
                }
            }

            InstallAllPassivesInternal();
            RestoreHealthAfterStatRefresh(oldMaxHealth, oldHealth, preserveHealthRatio);
        }

        public void RefreshPassiveLoadout(bool preserveHealthRatio = true)
        {
            if (_equippedPassives.Count == 0)
                return;

            float oldMaxHealth = MaxHealth.Value;
            float oldHealth = CurrentHealth;

            UninstallAllPassivesInternal();
            InstallAllPassivesInternal();

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

        private void InstallAllPassivesInternal()
        {
            for (int i = 0; i < _equippedPassives.Count; i++)
            {
                PassiveDefinition definition = _equippedPassives[i];
                object sourceToken = new object();

                PassiveInstallContext context = new PassiveInstallContext(this, Owner, sourceToken);
                definition.Install(context);

                IPassiveRuntime runtime = definition.CreateRuntime(context);
                runtime?.OnInstalled(this);

                _installedPassives.Add(new InstalledPassive
                {
                    Definition = definition,
                    Context = context,
                    Runtime = runtime
                });
            }
        }

        private void UninstallAllPassivesInternal()
        {
            for (int i = _installedPassives.Count - 1; i >= 0; i--)
            {
                InstalledPassive installed = _installedPassives[i];
                installed.Runtime?.OnUninstalled(this);
                installed.Definition?.Uninstall(installed.Context);
            }

            _installedPassives.Clear();
        }

        public void TickPassives(uint currentTick)
        {
            for (int i = 0; i < _installedPassives.Count; i++)
            {
                _installedPassives[i].Runtime?.Tick(this, currentTick);
            }
        }

        private void RebuildProgressionStats(bool preserveHealthRatio)
        {
            float oldMaxHealth = MaxHealth.Value;
            float oldHealth = CurrentHealth;

            var progression = Definition.GetProgressionBonus(CurrentPowerLevel);

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
            if (RemainingGadgets > 0)
                RemainingGadgets--;
        }

        public void AddSuperCharge(float amount)
        {
            SuperCharge.AddCharge(amount);
        }

        public bool TryConsumeSuper()
        {
            return SuperCharge.TryConsume();
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
            Ammo.Tick(deltaTime);
        }

        public void Reset()
        {
            RebuildProgressionStats(false);
            RefreshPassiveLoadout(false);

            Stats.ResetHealthToMax();
            Ammo.Refill();

            RefreshGadgetChargesFromRuntimeKit();
            Hypercharge = new HyperchargeTracker();
            SuperCharge.Reset(true); // For testing purposes, start with a full super charge on reset. Adjust as needed.

            IsStunned = false;
            IsInBush = false;
            IsRevealed = false;
            LastAttackTick = 0;

            ThreatTracker.Clear();
            AssistTracker.Clear();
            // One line replaces the four-line reset of damage modifiers
            // (incoming + outgoing), shield pool, and movement modifiers. The
            // POCO now owns every field that line used to touch.
            Stats.ClearAllModifiers();
            ActiveStatusEffects.Clear();
            RuntimeBuild.Clear();
            RuntimeKit.Clear();
            RefreshRuntimeBuildUnlockState();

            ClearActionState();
            ResetAbilityCooldowns();
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
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = ActiveStatusEffects[i];
                effect.Tick(this, currentTick);

                if (effect.IsExpired(currentTick))
                {
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

                    effect.Remove(this, currentTick);
                    ActiveStatusEffects.RemoveAt(i);
                    StatusEffectEventBus.RaiseRemoved(result);

                    var combatLog = ServiceProvider.Get<ICombatLogService>();
                    combatLog.AddEntry(CombatLogEntry.CreateStatusRemoved(currentTick, result));
                }
            }
        }

        public bool IsHiddenTo(TeamType observerTeam)
        {
            if (observerTeam == Team)
                return false;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            bool recentlyAttacked = (currentTick - LastAttackTick) < 60;

            if (!IsInBush || recentlyAttacked || IsRevealed)
                return false;

            return true;
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
            for (int i = 0; i < ActiveStatusEffects.Count; i++)
            {
                if (ActiveStatusEffects[i].Type == type)
                    return true;
            }

            return false;
        }

        public bool IsInActionState(BrawlerActionStateType type, uint currentTick)
        {
            return ActionStateMachine.IsInState(type, currentTick);
        }

        public void RefreshRuntimeBuildUnlockState()
        {
            if (Definition == null || Definition.BuildLayout == null || RuntimeBuild == null)
                return;

            RuntimeBuild.SetUnlockedState(
                Definition.BuildLayout.IsSlotUnlocked("gear_1", CurrentPowerLevel),
                Definition.BuildLayout.IsSlotUnlocked("gear_2", CurrentPowerLevel),
                Definition.BuildLayout.IsSlotUnlocked("gadget_1", CurrentPowerLevel),
                Definition.BuildLayout.IsSlotUnlocked("starpower_1", CurrentPowerLevel),
                Definition.BuildLayout.IsSlotUnlocked("hypercharge_1", CurrentPowerLevel)
            );
        }

        public bool HasUnlockedGadgetSlot()
        {
            return RuntimeBuild != null && RuntimeBuild.IsGadgetSlotUnlocked;
        }

        public bool HasUnlockedStarPowerSlot()
        {
            return RuntimeBuild != null && RuntimeBuild.IsStarPowerSlotUnlocked;
        }

        public bool HasUnlockedHyperchargeSlot()
        {
            return RuntimeBuild != null && RuntimeBuild.IsHyperchargeSlotUnlocked;
        }

        public bool HasAnyUnlockedGearSlot()
        {
            return RuntimeBuild != null && (RuntimeBuild.IsGearSlot1Unlocked || RuntimeBuild.IsGearSlot2Unlocked);
        }

        public AbilityDefinition GetCurrentMainAttackDefinition()
        {
            return RuntimeKit?.MainAttackDefinition ?? Definition?.MainAttack;
        }

        public AbilityDefinition GetBaseSuperDefinition()
        {
            return RuntimeKit?.SuperDefinition ?? Definition?.SuperAbility;
        }

        public GadgetDefinition GetCurrentGadgetDefinition()
        {
            return RuntimeKit?.GadgetDefinition;
        }

        public HyperchargeDefinition GetCurrentHyperchargeDefinition()
        {
            return RuntimeKit?.HyperchargeDefinition ?? EquippedHypercharge;
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
            GadgetDefinition gadget = RuntimeKit?.GadgetDefinition ?? Definition?.Gadget;
            RemainingGadgets = gadget != null ? gadget.MaxCharges : 0;
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
            return HasStatus(StatusEffectType.Silence);
        }

        public bool IsMainAttackLocked()
        {
            return HasStatus(StatusEffectType.AttackLock);
        }

        public bool IsGadgetLocked()
        {
            return HasStatus(StatusEffectType.GadgetLock);
        }

        public bool IsSuperLocked()
        {
            return HasStatus(StatusEffectType.SuperLock);
        }

        public bool IsMovementLocked()
        {
            return HasStatus(StatusEffectType.MovementLock) || IsStunned;
        }
        public bool CanReceiveStatusEffects()
        {
            return !IsDead;
        }
    }
}