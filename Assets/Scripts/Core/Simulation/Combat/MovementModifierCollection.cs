using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public sealed class MovementModifierCollection
    {
        private readonly List<MovementModifier> _modifiers = new List<MovementModifier>(8);

        public void Add(MovementModifier modifier)
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

        public float Apply(float baseSpeed)
        {
            float result = baseSpeed;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                var mod = _modifiers[i];
                if (mod.Type == MovementModifierType.SpeedMultiplier)
                {
                    result *= mod.Value;
                }
            }

            return result;
        }

        public void Clear()
        {
            _modifiers.Clear();
        }
    }
}