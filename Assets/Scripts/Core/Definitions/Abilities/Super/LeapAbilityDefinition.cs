using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "LeapAbility", menuName = "MOBA/Abilities/Leap")]
    public class LeapAbilityDefinition : AbilityDefinition
    {
        [Header("Leap")]
        public float Range = 6.5f;
        public float LandingRadius = 1.8f;
        public float Damage = 1600f;
        [Tooltip("How far around a blocked landing point the leap may search for the nearest clear landing.")]
        public float LandingSearchRadius = 1.25f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.Super;
            TargetingType = AbilityTargetingType.Area;
            DeliveryType = AbilityDeliveryType.Area;
            PreviewMode = AimPreviewMode.Placement;
        }

        public override IAbilityLogic CreateLogic()
        {
            return new LeapAbilityLogic(this);
        }

        public override float GetAIIdealRange()
        {
            return Mathf.Max(2f, Range * 0.82f);
        }

        public override float GetAIMaxRange()
        {
            return Mathf.Max(2f, Range);
        }
    }
}
