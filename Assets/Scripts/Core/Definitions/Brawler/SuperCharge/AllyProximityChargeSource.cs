using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Charges the super meter based on nearby allies. The runtime scans
    /// each tick for living allies within <see cref="Radius"/> and grants
    /// <c>count * ChargePerSecondPerAlly * deltaTime</c> to the owner.
    ///
    /// Scaffolded ahead of need — no brawler currently ships this source.
    /// The ally-registry lookup is wired through a small helper so that
    /// the actual scan source (team registry, BrawlerManager, etc.) can
    /// be swapped in a later session without touching this file.
    /// </summary>
    [CreateAssetMenu(fileName = "AllyProximityChargeSource", menuName = "MOBA/Super Charge/Ally Proximity")]
    public class AllyProximityChargeSource : SuperChargeSourceDefinition
    {
        [Header("Tuning")]
        [Tooltip("Radius (world units) within which allies count as 'nearby'.")]
        [Min(0f)]
        public float Radius = 3f;

        [Tooltip("Fraction of the super meter granted per ally, per real-time second.")]
        [Min(0f)]
        public float ChargePerSecondPerAlly = 0.01f;

        [Tooltip("If true, the owner counts as their own ally (always grants). Usually false.")]
        public bool IncludeSelf = false;

        [Tooltip("Maximum number of allies that contribute per tick (prevents team-stacking runaway).")]
        [Min(0)]
        public int MaxAlliesCounted = 3;

        public override SuperChargeSourceRuntime CreateRuntime()
        {
            return new AllyProximityChargeRuntime(this);
        }
    }
}
