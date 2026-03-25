using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "HealBurstGadget", menuName = "MOBA/Gadgets/Heal Burst Gadget")]
    public class HealBurstGadgetDefinition : GadgetDefinition
    {
        [Header("Heal Burst Gadget")]
        public float HealAmount = 800f;

        public override IAbilityLogic CreateLogic()
        {
            return new HealBurstGadgetLogic(HealAmount);
        }
    }
}