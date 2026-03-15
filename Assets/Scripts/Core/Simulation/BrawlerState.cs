using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public class BrawlerState
    {
        // Reference to the static data
        public BrawlerDefinition Definition { get; private set; }
        public TeamType Team { get; private set; } // Add this property

        // Dynamic, modifiable stats
        public ModifiableStat MaxHealth { get; private set; }
        public ModifiableStat MoveSpeed { get; private set; }
        public ModifiableStat Damage { get; private set; }

        public int RemainingGadgets { get; private set; }
        public HyperchargeTracker Hypercharge { get; private set; }
        // Current mutable values
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        // Events for the View Layer to listen to
        public Action OnDeath;
        public Action<float> OnHealthChanged;
        public ResourceStorage Ammo { get; private set; }
        public bool IsStunned;
        private List<StatusEffect> _activeEffects = new List<StatusEffect>();
        public bool IsInBush { get; set; }
        public uint LastAttackTick { get; set; }
        public bool IsRevealed { get; set; }

        public BrawlerState(BrawlerDefinition definition, TeamType team)
        {
            Definition = definition;
            Team = team;

            // Initialize stats from definition data
            MaxHealth = new ModifiableStat(definition.BaseHealth);
            MoveSpeed = new ModifiableStat(definition.BaseMoveSpeed);
            Damage = new ModifiableStat(definition.BaseDamage);
            Ammo = new ResourceStorage(3, 0.5f);
            Hypercharge = new HyperchargeTracker();
            RemainingGadgets = (definition.Gadget != null) ? definition.Gadget.MaxCharges : 0;
            // Start at full health
            CurrentHealth = MaxHealth.Value;
        }
        public void UseGadgetCharge() => RemainingGadgets--;

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

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
            if (IsDead) return;

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
            Ammo.Consume((int)-Ammo.MaxAmmo); // Hacky way to refill ammo to max
                                              // Note: Better to add a 'Refill()' method to ResourceStorage later!
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
            if (observerTeam == this.Team) return false;

            // Get the current tick via the Service Locator
            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;

            bool recentlyAttacked = (currentTick - LastAttackTick) < 60;

            if (!IsInBush || recentlyAttacked || IsRevealed) return false;

            return true;
        }
    }
}