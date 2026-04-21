using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// Charges the super meter from outgoing damage. The runtime adds
    /// <c>damageAmount * ChargePerDamage</c> to the owner's super charge
    /// every time <c>DamageService</c> pushes a finalised damage event.
    ///
    /// Tuning note: <see cref="ChargePerDamage"/> is expressed in
    /// "fraction of the meter per 1 point of damage". With Barley dealing
    /// 500 damage per bottle, a value of <c>0.00029</c> means each hit
    /// grants roughly <c>0.145</c> of the meter, i.e. about 7 landed
    /// bottles to fill.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageDealtChargeSource", menuName = "MOBA/Super Charge/Damage Dealt")]
    public class DamageDealtChargeSource : SuperChargeSourceDefinition
    {
        [Header("Tuning")]
        [Tooltip("Fraction of the super meter granted per 1 point of damage dealt.")]
        [Min(0f)]
        public float ChargePerDamage = 0.0005f;

        [Tooltip("If true, only direct-weapon main-attack damage feeds this source. If false, any outgoing damage counts (DoTs, reflects, etc.).")]
        public bool MainAttackOnly = false;

        public override SuperChargeSourceRuntime CreateRuntime()
        {
            return new DamageDealtChargeRuntime(this);
        }
    }
}
