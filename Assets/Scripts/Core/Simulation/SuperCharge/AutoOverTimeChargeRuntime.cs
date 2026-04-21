using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Runtime half of <see cref="AutoOverTimeChargeSource"/>. Adds a flat
    /// per-second grant to the owner's super meter every tick while the
    /// owner is alive (dead brawlers never charge).
    /// </summary>
    public sealed class AutoOverTimeChargeRuntime : SuperChargeSourceRuntime
    {
        private readonly AutoOverTimeChargeSource _definition;

        public AutoOverTimeChargeRuntime(AutoOverTimeChargeSource definition)
        {
            _definition = definition;
        }

        public override void Tick(BrawlerState owner, float deltaTime, uint currentTick)
        {
            if (_definition == null || !_definition.Enabled)
                return;

            if (owner == null || owner.IsDead)
                return;

            if (deltaTime <= 0f || _definition.ChargePerSecond <= 0f)
                return;

            // PauseInCombat is intentionally unimplemented for now — the
            // "recently took/dealt damage" signal needs a combat-tracker
            // field on BrawlerState that doesn't exist yet. When that lands,
            // this method gets one additional gate; the source's tuning
            // knob on the definition is already in place for designers.

            float grant = _definition.ChargePerSecond * deltaTime;
            if (grant <= 0f)
                return;

            owner.AddSuperCharge(grant);
        }
    }
}
