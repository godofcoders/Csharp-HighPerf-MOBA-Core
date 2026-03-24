using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "DashGadget", menuName = "MOBA/Gadgets/Dash Gadget")]
    public class DashGadgetDefinition : GadgetDefinition
    {
        [Header("Dash Gadget")]
        public float DashForce = 3.5f;

        public override IAbilityLogic CreateLogic()
        {
            return new DashGadgetLogic(DashForce);
        }
    }
}