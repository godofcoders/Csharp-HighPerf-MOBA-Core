using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "AllyHealPulseGadget", menuName = "MOBA/Gadgets/Ally Heal Pulse Gadget")]
    public class AllyHealPulseGadgetDefinition : GadgetDefinition
    {
        [Header("Ally Heal Pulse Gadget")]
        [Tooltip("Amount healed per ally in range (single instant pulse).")]
        public float HealAmount = 500f;

        [Tooltip("Effect radius in world units. Self included when the caster is inside the radius.")]
        public float Radius = 4f;

        public override IAbilityLogic CreateLogic()
        {
            return new AllyHealPulseGadgetLogic(HealAmount, Radius);
        }
    }
}
