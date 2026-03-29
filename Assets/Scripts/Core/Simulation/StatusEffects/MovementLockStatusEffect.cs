namespace MOBA.Core.Simulation
{
    public sealed class MovementLockStatusEffect : TimedControlStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.MovementLock;

        public MovementLockStatusEffect(uint startTick, uint endTick, object sourceToken)
            : base(startTick, endTick, sourceToken)
        {
        }
    }
}