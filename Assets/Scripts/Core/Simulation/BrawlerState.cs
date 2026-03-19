using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public class BrawlerState
    {
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

        public BrawlerState(BrawlerDefinition definition, TeamType team)
        {
            Definition = definition;
            Team = team;

            MaxHealth = new ModifiableStat(definition.BaseHealth);
            MoveSpeed = new ModifiableStat(definition.BaseMoveSpeed);
            Damage = new ModifiableStat(definition.BaseDamage);

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

            RemainingGadgets = definition.Gadget != null ? definition.Gadget.MaxCharges : 0;
            CurrentHealth = MaxHealth.Value;
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

            if (IsDead)
            {
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
        }

        public void UpdateResources(float deltaTime)
        {
            Ammo.Tick(deltaTime);
        }

        public void Reset()
        {
            CurrentHealth = MaxHealth.Value;
            Ammo.Refill();

            RemainingGadgets = Definition.Gadget != null ? Definition.Gadget.MaxCharges : 0;
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

            OnHealthChanged?.Invoke(CurrentHealth);
        }

        public void TickEffects(uint currentTick)
        {
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = ActiveStatusEffects[i];
                effect.Tick(this, currentTick);

                if (effect.IsExpired(currentTick))
                {
                    var context = new StatusEffectResult
                    {
                        Context = default,
                        Applied = false,
                        Refreshed = false
                    };

                    effect.Remove(this, currentTick);
                    ActiveStatusEffects.RemoveAt(i);
                    StatusEffectEventBus.RaiseRemoved(context);
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
            if (amount <= 0f) return;
            ShieldHealth += amount;
        }

        public void ClearShield()
        {
            ShieldHealth = 0f;
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
    }
}