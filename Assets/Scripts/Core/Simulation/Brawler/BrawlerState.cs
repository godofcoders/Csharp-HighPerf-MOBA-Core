using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public class BrawlerState
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

        public DamageModifierCollection IncomingDamageModifiers { get; private set; }
        public DamageModifierCollection OutgoingDamageModifiers { get; private set; }
        public float ShieldHealth { get; private set; }
        public ModifiableStat MaxHealth { get; private set; }
        public ModifiableStat MoveSpeed { get; private set; }
        public ModifiableStat Damage { get; private set; }

        public int RemainingGadgets { get; private set; }
        public HyperchargeTracker Hypercharge { get; private set; }
        public SuperChargeTracker SuperCharge { get; private set; }

        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        public Action OnDeath;
        public Action<float> OnHealthChanged;

        public ResourceStorage Ammo { get; private set; }
        public bool IsStunned;

        public bool IsInBush { get; set; }
        public uint LastAttackTick { get; set; }
        public bool IsRevealed { get; set; }

        public BrawlerController Owner { get; set; }
        public MovementModifierCollection IncomingMovementModifiers { get; private set; }
        public List<IStatusEffectInstance> ActiveStatusEffects { get; private set; }
        public BrawlerActionStateData ActionState { get; private set; }

        public AbilityCooldownState MainAttackCooldown { get; private set; }
        public AbilityCooldownState SuperCooldown { get; private set; }
        public AbilityCooldownState GadgetCooldown { get; private set; }

        public int CurrentPowerLevel { get; private set; }

        private readonly List<PassiveDefinition> _equippedPassives = new List<PassiveDefinition>(4);
        private readonly List<InstalledPassive> _installedPassives = new List<InstalledPassive>(4);

        public IReadOnlyList<PassiveDefinition> EquippedPassives => _equippedPassives;

        public HyperchargeDefinition EquippedHypercharge { get; private set; }
        public object HyperchargeModifierSource { get; } = new object();

        public BrawlerRuntimeBuildState RuntimeBuild { get; private set; }
        public BrawlerRuntimeKit RuntimeKit { get; private set; }

        public BrawlerState(BrawlerDefinition definition, TeamType team)
        {
            Definition = definition;
            Team = team;
            CurrentPowerLevel = 1;

            MaxHealth = new ModifiableStat(0f);
            MoveSpeed = new ModifiableStat(0f);
            Damage = new ModifiableStat(0f);

            Ammo = new ResourceStorage(3, 0.5f);
            Hypercharge = new HyperchargeTracker();
            SuperCharge = new SuperChargeTracker();
            ThreatTracker = new MOBA.Core.Simulation.AI.ThreatTracker();
            AssistTracker = new MOBA.Core.Simulation.AI.AssistTracker();
            IncomingDamageModifiers = new DamageModifierCollection();
            OutgoingDamageModifiers = new DamageModifierCollection();
            ShieldHealth = 0f;
            IncomingMovementModifiers = new MovementModifierCollection();
            ActiveStatusEffects = new List<IStatusEffectInstance>(8);
            RuntimeBuild = new BrawlerRuntimeBuildState();
            RuntimeKit = new BrawlerRuntimeKit();

            RefreshRuntimeBuildUnlockState();
            RefreshGadgetChargesFromRuntimeKit();
            RebuildProgressionStats(false);
            CurrentHealth = MaxHealth.Value;

            ClearActionState();
            ResetAbilityCooldowns();
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

            if (preserveHealthRatio && oldMaxHealth > 0f)
            {
                float healthRatio = oldHealth / oldMaxHealth;
                CurrentHealth = newMaxHealth * healthRatio;
            }
            else
            {
                if (CurrentHealth > newMaxHealth)
                    CurrentHealth = newMaxHealth;
            }

            if (CurrentHealth < 0f)
                CurrentHealth = 0f;

            OnHealthChanged?.Invoke(CurrentHealth);
        }

        public void RemoveAllStatModifiersFromSource(object source)
        {
            MaxHealth.RemoveModifiersFromSource(source);
            MoveSpeed.RemoveModifiersFromSource(source);
            Damage.RemoveModifiersFromSource(source);
        }

        public void AddIncomingMovementModifier(MovementModifier modifier)
        {
            IncomingMovementModifiers.Add(modifier);
        }

        public void RemoveIncomingMovementModifiersFromSource(object source)
        {
            IncomingMovementModifiers.RemoveBySource(source);
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

            CurrentHealth -= amount;
            CurrentHealth = Math.Max(0, CurrentHealth);

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

            if (IsDead)
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

            CurrentHealth += amount;
            CurrentHealth = Math.Min(CurrentHealth, MaxHealth.Value);

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

            CurrentHealth = MaxHealth.Value;
            Ammo.Refill();

            RefreshGadgetChargesFromRuntimeKit();
            Hypercharge = new HyperchargeTracker();
            SuperCharge.Reset(false);

            IsStunned = false;
            IsInBush = false;
            IsRevealed = false;
            LastAttackTick = 0;

            ThreatTracker.Clear();
            AssistTracker.Clear();
            IncomingDamageModifiers.Clear();
            OutgoingDamageModifiers.Clear();
            ShieldHealth = 0f;
            IncomingMovementModifiers.Clear();
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
                            Target = Owner,
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
            if (amount <= 0f)
                return;

            ShieldHealth += amount;
        }

        public void ClearShield()
        {
            ShieldHealth = 0f;
        }

        public void EnterActionState(
            BrawlerActionStateType stateType,
            uint currentTick,
            uint durationTicks,
            bool allowMovement,
            bool allowActionInput,
            bool isInterruptible)
        {
            ActionState = new BrawlerActionStateData
            {
                StateType = stateType,
                StartTick = currentTick,
                LockUntilTick = currentTick + durationTicks,
                AllowMovement = allowMovement,
                AllowActionInput = allowActionInput,
                IsInterruptible = isInterruptible
            };
        }

        public void ClearActionState()
        {
            ActionState = new BrawlerActionStateData
            {
                StateType = BrawlerActionStateType.None,
                StartTick = 0,
                LockUntilTick = 0,
                AllowMovement = true,
                AllowActionInput = true,
                IsInterruptible = true
            };
        }

        public void UpdateActionState(uint currentTick)
        {
            if (ActionState.StateType != BrawlerActionStateType.None &&
                !ActionState.IsActive(currentTick))
            {
                ClearActionState();
            }
        }

        public bool HasActiveActionState(uint currentTick)
        {
            return ActionState.StateType != BrawlerActionStateType.None &&
                   ActionState.IsActive(currentTick);
        }

        public bool IsAbilityReady(AbilityRuntimeSlot slot, uint currentTick)
        {
            switch (slot)
            {
                case AbilityRuntimeSlot.MainAttack:
                    return MainAttackCooldown.IsReady(currentTick);

                case AbilityRuntimeSlot.Super:
                    return SuperCooldown.IsReady(currentTick);

                case AbilityRuntimeSlot.Gadget:
                    return GadgetCooldown.IsReady(currentTick);

                default:
                    return false;
            }
        }

        public void StartAbilityCooldown(AbilityRuntimeSlot slot, uint currentTick, float cooldownSeconds)
        {
            uint cooldownTicks = (uint)(cooldownSeconds * 30f);

            switch (slot)
            {
                case AbilityRuntimeSlot.MainAttack:
                    MainAttackCooldown.StartCooldown(currentTick, cooldownTicks);
                    break;

                case AbilityRuntimeSlot.Super:
                    SuperCooldown.StartCooldown(currentTick, cooldownTicks);
                    break;

                case AbilityRuntimeSlot.Gadget:
                    GadgetCooldown.StartCooldown(currentTick, cooldownTicks);
                    break;
            }
        }

        public void ResetAbilityCooldowns()
        {
            MainAttackCooldown.Reset();
            SuperCooldown.Reset();
            GadgetCooldown.Reset();
        }

        public bool CanMove(uint currentTick)
        {
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
            if (ActionState.StateType == BrawlerActionStateType.None)
                return true;

            if (!ActionState.IsInterruptible)
                return false;

            ClearActionState();
            return true;
        }

        public void AddIncomingDamageModifier(DamageModifier modifier)
        {
            IncomingDamageModifiers.Add(modifier);
        }

        public void AddOutgoingDamageModifier(DamageModifier modifier)
        {
            OutgoingDamageModifiers.Add(modifier);
        }

        public void RemoveIncomingDamageModifiersFromSource(object source)
        {
            IncomingDamageModifiers.RemoveBySource(source);
        }

        public void RemoveOutgoingDamageModifiersFromSource(object source)
        {
            OutgoingDamageModifiers.RemoveBySource(source);
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
            return ActionState.StateType == type && ActionState.IsActive(currentTick);
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
                default: return "Unknown";
            }
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
    }
}