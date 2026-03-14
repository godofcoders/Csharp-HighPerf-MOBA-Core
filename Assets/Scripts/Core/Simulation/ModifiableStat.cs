using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public class ModifiableStat
    {
        public float BaseValue;
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        public float Value => CalculateValue();

        public ModifiableStat(float baseValue) => BaseValue = baseValue;

        public void AddModifier(StatModifier mod) => _modifiers.Add(mod);
        public void RemoveModifiersFromSource(object source) => _modifiers.RemoveAll(m => m.Source == source);

        private float CalculateValue()
        {
            float totalAdd = 0;
            float totalMult = 0;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Type == ModifierType.Additive) totalAdd += _modifiers[i].Value;
                else totalMult += _modifiers[i].Value;
            }
            return (BaseValue + totalAdd) * (1 + totalMult);
        }
    }
}