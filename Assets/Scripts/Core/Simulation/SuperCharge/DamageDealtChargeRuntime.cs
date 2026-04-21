using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Runtime half of <see cref="DamageDealtChargeSource"/>. Listens for
    /// damage-dealt pushes from the loadout and adds a proportional amount
    /// to the owner's super meter.
    ///
    /// The runtime captures its definition by reference so designer tweaks
    /// during play-mode iteration are picked up immediately without a
    /// reinstall — matches the behaviour of the passive runtime classes.
    /// </summary>
    public sealed class DamageDealtChargeRuntime : SuperChargeSourceRuntime
    {
        private readonly DamageDealtChargeSource _definition;

        public DamageDealtChargeRuntime(DamageDealtChargeSource definition)
        {
            _definition = definition;
        }

        public override void OnDamageDealt(BrawlerState owner, float damageAmount, BrawlerState victim)
        {
            if (_definition == null || !_definition.Enabled)
                return;

            if (damageAmount <= 0f)
                return;

            if (owner == null || owner.IsDead)
                return;

            // Dead (or self-damage) victims still count toward the meter —
            // killing blows should still charge. The MainAttackOnly gate
            // is left for the caller to enforce once damage-tagging is in
            // (there's no flag on DamageContext today), so for now the
            // runtime simply accepts every push it receives.

            float grant = damageAmount * _definition.ChargePerDamage;
            if (grant <= 0f)
                return;

            owner.AddSuperCharge(grant);
        }
    }
}
