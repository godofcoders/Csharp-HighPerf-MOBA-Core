using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "AmmoRefillGadget", menuName = "MOBA/Gadgets/Ammo Refill Gadget")]
    public class AmmoRefillGadgetDefinition : GadgetDefinition
    {
        [Header("Ammo Refill Gadget")]
        [Tooltip("Rounds to refill. 0 or negative = full refill. " +
                 "(Partial refill requires ResourceStorage.Add API — not yet implemented.)")]
        public int RefillAmount = 0;

        public override IAbilityLogic CreateLogic()
        {
            return new AmmoRefillGadgetLogic(RefillAmount);
        }
    }
}
