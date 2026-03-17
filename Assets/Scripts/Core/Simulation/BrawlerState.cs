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

        private readonly List<StatusEffect> _activeEffects = new List<StatusEffect>();

        public bool IsInBush { get; set; }
        public uint LastAttackTick { get; set; }
        public bool IsRevealed { get; set; }

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

            RemainingGadgets = definition.Gadget != null ? definition.Gadget.MaxCharges : 0;
            CurrentHealth = MaxHealth.Value;
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

            _activeEffects.Clear();
            OnHealthChanged?.Invoke(CurrentHealth);
        }

        public void ApplyEffect(StatusEffect effect, float duration)
        {
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            effect.Initialize(this, duration, currentTick);
            _activeEffects.Add(effect);
        }

        public void TickEffects(uint currentTick)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                effect.OnTick(currentTick);

                if (effect.IsExpired(currentTick))
                {
                    effect.OnRemove();
                    _activeEffects.RemoveAt(i);
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
    }
}