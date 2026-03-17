using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public sealed class DamageModifierCollection
    {
        private readonly List<DamageModifier> _modifiers = new List<DamageModifier>(8);

        public void Add(DamageModifier modifier)
        {
            _modifiers.Add(modifier);
        }

        public void RemoveBySource(object source)
        {
            if (source == null) return;

            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].Source == source)
                {
                    _modifiers.RemoveAt(i);
                }
            }
        }

        public float ApplyIncoming(float damage, ref float remainingShield)
        {
            float result = damage;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                var mod = _modifiers[i];

                switch (mod.Type)
                {
                    case DamageModifierType.FlatReduction:
                        result -= mod.Value;
                        break;

                    case DamageModifierType.PercentReduction:
                        result *= (1f - mod.Value);
                        break;

                    case DamageModifierType.PercentAmplification:
                        result *= (1f + mod.Value);
                        break;

                    case DamageModifierType.Shield:
                        if (remainingShield > 0f)
                        {
                            float absorbed = result <= remainingShield ? result : remainingShield;
                            remainingShield -= absorbed;
                            result -= absorbed;
                        }
                        break;
                }
            }

            return result < 0f ? 0f : result;
        }

        public float GetLifestealPercent()
        {
            float value = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Type == DamageModifierType.Lifesteal)
                {
                    value += _modifiers[i].Value;
                }
            }

            return value;
        }

        public void Clear()
        {
            _modifiers.Clear();
        }
    }
}