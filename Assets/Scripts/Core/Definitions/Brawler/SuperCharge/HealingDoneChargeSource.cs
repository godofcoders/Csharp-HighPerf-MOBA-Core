using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Charges the super meter from outgoing heals (allies healed or self-
    /// heal, per designer taste). Pairs with <c>HealingDoneChargeRuntime</c>.
    ///
    /// Tuning note: <see cref="ChargePerHeal"/> is "fraction of meter per
    /// 1 point of healing actually applied" — the pipeline clamps away
    /// over-heal before pushing, so HP-topped-off allies don't grant.
    ///
    /// Currently scaffolded but unwired at runtime (heal pipeline does
    /// not yet push NotifyHealApplied). Leave <see cref="Enabled"/> off
    /// or simply don't attach this source until the heal pipeline is
    /// updated in a later session.
    /// </summary>
    [CreateAssetMenu(fileName = "HealingDoneChargeSource", menuName = "MOBA/Super Charge/Healing Done")]
    public class HealingDoneChargeSource : SuperChargeSourceDefinition
    {
        [Header("Tuning")]
        [Tooltip("Fraction of the super meter granted per 1 point of healing applied to an ally.")]
        [Min(0f)]
        public float ChargePerHeal = 0.0005f;

        [Tooltip("If true, self-heals (owner == recipient) count. If false, only ally heals grant.")]
        public bool IncludeSelfHeals = false;

        public override SuperChargeSourceRuntime CreateRuntime()
        {
            return new HealingDoneChargeRuntime(this);
        }
    }
}
