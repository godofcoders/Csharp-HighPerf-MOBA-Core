using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "SuperChargeGadget", menuName = "MOBA/Gadgets/Super Charge Gadget")]
    public class SuperChargeGadgetDefinition : GadgetDefinition
    {
        [Header("Super Charge Gadget")]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the super meter granted on activation. 0.25 = +25% super.")]
        public float ChargeFraction = 0.25f;

        public override IAbilityLogic CreateLogic()
        {
            return new SuperChargeGadgetLogic(ChargeFraction);
        }
    }
}
