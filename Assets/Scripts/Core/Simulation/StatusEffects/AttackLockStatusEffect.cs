namespace MOBA.Core.Simulation
{
    public sealed class AttackLockStatusEffect : TimedControlStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.AttackLock;

        public AttackLockStatusEffect(uint startTick, uint endTick, object sourceToken)
            : base(startTick, endTick, sourceToken)
        {
        }
    }
}