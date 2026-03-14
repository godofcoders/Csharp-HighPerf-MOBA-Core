using UnityEngine;

namespace MOBA.Core.Simulation
{
    // 1. SLOW: Interacts with ModifiableStat
    public class SlowEffect : StatusEffect
    {
        private float _intensity; // e.g., 0.3 for 30% slow
        private StatModifier _mod;

        public SlowEffect(float intensity) => _intensity = intensity;

        public override void OnApply()
        {
            // Note: We use a negative multiplier for a slow
            _mod = new StatModifier(-_intensity, ModifierType.Multiplicative, this);
            Target.MoveSpeed.AddModifier(_mod);
        }

        public override void OnTick(uint currentTick) { } // Slows are passive

        public override void OnRemove() => Target.MoveSpeed.RemoveModifiersFromSource(this);
    }

    // 2. STUN: Prevents Input Consumption
    public class StunEffect : StatusEffect
    {
        public override void OnApply() => Target.IsStunned = true;
        public override void OnTick(uint currentTick) { }
        public override void OnRemove() => Target.IsStunned = false;
    }

    // 3. POISON (DoT): Applies damage every X ticks
    public class PoisonEffect : StatusEffect
    {
        private float _damagePerTick;
        public PoisonEffect(float totalDmg, float duration)
            => _damagePerTick = totalDmg / (duration * 30);

        public override void OnApply() { }
        public override void OnTick(uint currentTick) => Target.TakeDamage(_damagePerTick);
        public override void OnRemove() { }
    }
}