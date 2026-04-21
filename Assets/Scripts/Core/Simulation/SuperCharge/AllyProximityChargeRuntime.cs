using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Runtime half of <see cref="AllyProximityChargeSource"/>. Scaffold
    /// only — the actual ally scan is delegated to <see cref="AllyScanner"/>
    /// which is null today. Once a team/brawler registry service lands,
    /// the coordinator can set <see cref="AllyScanner"/> during install
    /// and this runtime starts granting on its own.
    ///
    /// Keeping the scan helper as an injected function (rather than a
    /// hard service lookup) means this file has no new dependencies to
    /// resolve today — the class compiles and the hook is a safe no-op.
    /// </summary>
    public sealed class AllyProximityChargeRuntime : SuperChargeSourceRuntime
    {
        /// <summary>
        /// Injected callback that returns the number of living allies within
        /// the supplied radius of the given owner, capped at
        /// <paramref name="maxAllies"/> and (optionally) counting self.
        /// Runtime leaves the grant at zero if this is null.
        /// </summary>
        public System.Func<BrawlerState, float, int, bool, int> AllyScanner;

        private readonly AllyProximityChargeSource _definition;

        public AllyProximityChargeRuntime(AllyProximityChargeSource definition)
        {
            _definition = definition;
        }

        public override void Tick(BrawlerState owner, float deltaTime, uint currentTick)
        {
            if (_definition == null || !_definition.Enabled)
                return;

            if (owner == null || owner.IsDead)
                return;

            if (deltaTime <= 0f || _definition.ChargePerSecondPerAlly <= 0f)
                return;

            if (AllyScanner == null)
                return;

            int allies = AllyScanner(
                owner,
                _definition.Radius,
                _definition.MaxAlliesCounted,
                _definition.IncludeSelf);

            if (allies <= 0)
                return;

            float grant = allies * _definition.ChargePerSecondPerAlly * deltaTime;
            if (grant <= 0f)
                return;

            owner.AddSuperCharge(grant);
        }
    }
}
