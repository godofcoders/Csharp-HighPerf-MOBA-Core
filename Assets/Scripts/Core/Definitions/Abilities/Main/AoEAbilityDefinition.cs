using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewAoEAbility", menuName = "MOBA/Abilities/AoE")]
    public class AoEAbilityDefinition : AbilityDefinition
    {
        [Header("Area")]
        public float Damage = 1500f;
        public float Radius = 5f;

        private void OnValidate()
        {
            DeliveryType = AbilityDeliveryType.Area;

            if (SlotType != AbilitySlotType.Super && SlotType != AbilitySlotType.Gadget)
            {
                SlotType = AbilitySlotType.MainAttack;
            }

            if (TargetingType == AbilityTargetingType.Self)
            {
                TargetingType = AbilityTargetingType.Area;
            }
        }

        public override IAbilityLogic CreateLogic()
        {
            return new AoEAbilityLogic(Damage, Radius);
        }

        public override float GetAIIdealRange()
        {
            return Mathf.Max(2f, Radius * 0.9f);
        }

        public override float GetAIMaxRange()
        {
            return Mathf.Max(2f, Radius * 1.1f);
        }
    }
}