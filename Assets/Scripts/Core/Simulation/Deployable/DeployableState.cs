using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class DeployableState
    {
        public DeployableDefinition Definition { get; private set; }
        public BrawlerController Owner { get; private set; }
        public TeamType Team { get; private set; }

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float ShieldHealth { get; private set; }

        public uint SpawnTick { get; private set; }
        public uint ExpiryTick { get; private set; }

        public bool IsDead => CurrentHealth <= 0f;

        public List<IStatusEffectInstance> ActiveStatusEffects { get; private set; }
        public DamageModifierCollection IncomingDamageModifiers { get; private set; }

        public DeployableState(
            DeployableDefinition definition,
            BrawlerController owner,
            TeamType team,
            uint spawnTick)
        {
            Definition = definition;
            Owner = owner;
            Team = team;
            SpawnTick = spawnTick;

            MaxHealth = definition != null ? definition.MaxHealth : 0f;
            CurrentHealth = MaxHealth;
            ShieldHealth = 0f;

            float lifetimeSeconds = definition != null ? definition.LifetimeSeconds : 0f;
            ExpiryTick = spawnTick + (uint)(lifetimeSeconds * 30f);

            ActiveStatusEffects = new List<IStatusEffectInstance>(8);
            IncomingDamageModifiers = new DamageModifierCollection();
        }

        public bool IsExpired(uint currentTick)
        {
            return currentTick >= ExpiryTick;
        }

        public bool CanReceiveHealing()
        {
            return Definition != null && Definition.CanReceiveHealing && !IsDead;
        }

        public bool CanReceiveShield()
        {
            return Definition != null && Definition.CanReceiveShield && !IsDead;
        }

        public bool CanReceiveStatusEffects()
        {
            return Definition != null && Definition.CanReceiveStatusEffects && !IsDead;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            amount = IncomingDamageModifiers.Apply(amount);

            if (ShieldHealth > 0f)
            {
                float absorbed = amount <= ShieldHealth ? amount : ShieldHealth;
                ShieldHealth -= absorbed;
                amount -= absorbed;
            }

            if (amount <= 0f)
                return;

            CurrentHealth -= amount;
            if (CurrentHealth < 0f)
                CurrentHealth = 0f;
        }

        public void Heal(float amount)
        {
            if (!CanReceiveHealing() || amount <= 0f)
                return;

            CurrentHealth += amount;
            if (CurrentHealth > MaxHealth)
                CurrentHealth = MaxHealth;
        }

        public void AddShield(float amount)
        {
            if (!CanReceiveShield() || amount <= 0f)
                return;

            ShieldHealth += amount;
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

        public void TickStatusEffects(uint currentTick)
        {
            for (int i = ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                IStatusEffectInstance effect = ActiveStatusEffects[i];
                effect.Tick(null, currentTick);

                if (effect.IsExpired(currentTick))
                {
                    effect.Remove(null, currentTick);
                    ActiveStatusEffects.RemoveAt(i);
                }
            }
        }

        public void AddIncomingDamageModifier(DamageModifier modifier)
        {
            IncomingDamageModifiers.Add(modifier);
        }

        public void RemoveIncomingDamageModifiersFromSource(object source)
        {
            IncomingDamageModifiers.RemoveBySource(source);
        }
    }
}