namespace MOBA.Core.Simulation
{
    public sealed class GadgetLockStatusEffect : TimedControlStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.GadgetLock;

        public GadgetLockStatusEffect(uint startTick, uint endTick, object sourceToken)
            : base(startTick, endTick, sourceToken)
        {
        }
    }
}