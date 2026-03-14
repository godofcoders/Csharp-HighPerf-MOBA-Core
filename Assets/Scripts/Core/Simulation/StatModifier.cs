namespace MOBA.Core.Simulation
{
    public enum ModifierType { Additive, Multiplicative }

    public struct StatModifier
    {
        public readonly float Value;
        public readonly ModifierType Type;
        public readonly object Source; // Identifies who gave the buff (e.g., "Hypercharge")

        public StatModifier(float value, ModifierType type, object source = null)
        {
            Value = value;
            Type = type;
            Source = source;
        }
    }
}