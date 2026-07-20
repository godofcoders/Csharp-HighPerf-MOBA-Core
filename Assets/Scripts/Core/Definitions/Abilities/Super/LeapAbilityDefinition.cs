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
        public float Range = 7.6f;
        public float LandingRadius = 1.8f;
        public float Damage = 1600f;
        [Tooltip("How far around a blocked landing point the leap may search for the nearest clear landing.")]
        public float LandingSearchRadius = 1.25f;
        [Tooltip("Visual and damage delay for the jump. Damage resolves when the brawler lands.")]
        public float TravelDurationSeconds = 0.66f;
        [Tooltip("Additional visual travel time per horizontal unit. Keeps long leaps from looking like short hops.")]
        public float TravelDurationPerUnit = 0f;
        [Tooltip("Upper clamp for distance-scaled leap travel time. Set to 0 to leave unclamped.")]
        public float MaxTravelDurationSeconds = 0f;
        [Tooltip("Presentation-only height used by the leap arc.")]
        public float JumpHeight = 3.15f;
        [Tooltip("Extra presentation height added per horizontal unit traveled.")]
        public float DistanceHeightBonus = 0f;
        [Tooltip("Upper clamp for distance-scaled presentation height. Set to 0 to leave unclamped.")]
        public float MaxJumpHeight = 0f;
        [Tooltip("Values below 1 keep the brawler readable near the top of the arc for longer.")]
        [Range(0.35f, 1.5f)]
        public float ApexHangPower = 1f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.Super;
            TargetingType = AbilityTargetingType.Area;
            DeliveryType = AbilityDeliveryType.Area;
            PreviewMode = AimPreviewMode.Placement;

            Range = Mathf.Max(0.1f, Range);
            LandingRadius = Mathf.Max(0f, LandingRadius);
            Damage = Mathf.Max(0f, Damage);
            LandingSearchRadius = Mathf.Max(0f, LandingSearchRadius);
            TravelDurationSeconds = Mathf.Max(0f, TravelDurationSeconds);
            TravelDurationPerUnit = Mathf.Max(0f, TravelDurationPerUnit);
            MaxTravelDurationSeconds = Mathf.Max(0f, MaxTravelDurationSeconds);
            JumpHeight = Mathf.Max(0f, JumpHeight);
            DistanceHeightBonus = Mathf.Max(0f, DistanceHeightBonus);
            MaxJumpHeight = Mathf.Max(0f, MaxJumpHeight);
            ApexHangPower = Mathf.Clamp(ApexHangPower, 0.35f, 1.5f);
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
