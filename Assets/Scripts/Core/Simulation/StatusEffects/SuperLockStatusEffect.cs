namespace MOBA.Core.Simulation
{
    public sealed class SuperLockStatusEffect : TimedControlStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.SuperLock;

        public SuperLockStatusEffect(uint startTick, uint endTick, object sourceToken)
            : base(startTick, endTick, sourceToken)
        {
        }
    }
}