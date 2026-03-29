namespace MOBA.Core.Simulation
{
    public sealed class SilenceStatusEffect : TimedControlStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Silence;

        public SilenceStatusEffect(uint startTick, uint endTick, object sourceToken)
            : base(startTick, endTick, sourceToken)
        {
        }
    }
}