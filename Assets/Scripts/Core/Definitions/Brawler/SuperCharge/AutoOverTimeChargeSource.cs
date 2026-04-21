using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Charges the super meter continuously while the brawler is alive.
    /// The runtime adds <c>ChargePerSecond * deltaTime</c> to the owner's
    /// super charge every simulation tick.
    ///
    /// Scaffolded ahead of need — no brawler currently ships this source.
    /// A future "Surge" / "Bibi"-style brawler could drop one in to model
    /// passive meter gain.
    /// </summary>
    [CreateAssetMenu(fileName = "AutoOverTimeChargeSource", menuName = "MOBA/Super Charge/Auto Over Time")]
    public class AutoOverTimeChargeSource : SuperChargeSourceDefinition
    {
        [Header("Tuning")]
        [Tooltip("Fraction of the super meter granted per real-time second.")]
        [Min(0f)]
        public float ChargePerSecond = 0.02f;

        [Tooltip("If true, auto-charge pauses while the owner is in combat (took or dealt damage recently). Reserved for future implementation.")]
        public bool PauseInCombat = false;

        public override SuperChargeSourceRuntime CreateRuntime()
        {
            return new AutoOverTimeChargeRuntime(this);
        }
    }
}
