using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Runtime half of <see cref="HealingDoneChargeSource"/>. Listens for
    /// heal-applied pushes from the loadout and adds a proportional amount
    /// to the owner's super meter.
    ///
    /// Scaffolded for future use — no call site pushes
    /// <c>NotifyHealApplied</c> yet. Once the heal pipeline is updated to
    /// fan out like <c>DamageService</c> does for damage, this runtime will
    /// start granting without any further change on this side.
    /// </summary>
    public sealed class HealingDoneChargeRuntime : SuperChargeSourceRuntime
    {
        private readonly HealingDoneChargeSource _definition;

        public HealingDoneChargeRuntime(HealingDoneChargeSource definition)
        {
            _definition = definition;
        }

        public override void OnHealApplied(BrawlerState owner, float healAmount, BrawlerState recipient)
        {
            if (_definition == null || !_definition.Enabled)
                return;

            if (healAmount <= 0f)
                return;

            if (owner == null || owner.IsDead)
                return;

            bool isSelfHeal = ReferenceEquals(owner, recipient);
            if (isSelfHeal && !_definition.IncludeSelfHeals)
                return;

            float grant = healAmount * _definition.ChargePerHeal;
            if (grant <= 0f)
                return;

            owner.AddSuperCharge(grant);
        }
    }
}
