namespace MOBA.Core.Simulation
{
    public struct MovementModifier
    {
        public MovementModifierType Type;
        public float Value;
        public object Source;

        public MovementModifier(MovementModifierType type, float value, object source = null)
        {
            Type = type;
            Value = value;
            Source = source;
        }
    }
}