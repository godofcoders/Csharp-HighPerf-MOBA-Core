namespace MOBA.Core.Simulation
{
    public struct DamageModifier
    {
        public DamageModifierType Type;
        public float Value;
        public object Source;

        public DamageModifier(DamageModifierType type, float value, object source = null)
        {
            Type = type;
            Value = value;
            Source = source;
        }
    }
}