using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BombLeapAbility", menuName = "MOBA/Abilities/Bomb Leap")]
    public sealed class BombLeapAbilityDefinition : AbilityDefinition
    {
        [Header("Leap")]
        public float Range = 8f;
        public float LandingSearchRadius = 1.25f;
        public float TravelDurationSeconds = 0.75f;
        public float TravelDurationPerUnit = 0.035f;
        public float MaxTravelDurationSeconds = 1.1f;
        public float JumpHeight = 3.7f;
        public float DistanceHeightBonus = 0.2f;
        public float MaxJumpHeight = 5.2f;
        [Range(0.35f, 1.5f)]
        public float ApexHangPower = 0.82f;

        [Header("Takeoff Bombs")]
        [Min(1)] public int BombCount = 4;
        public float BombDelaySeconds = 0.5f;
        public float BombRadius = 1.25f;
        public float BombDamage = 720f;
        public float BombSpreadRadius = 1.35f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.Super;
            TargetingType = AbilityTargetingType.Area;
            DeliveryType = AbilityDeliveryType.Area;
            PreviewMode = AimPreviewMode.Placement;
            Intent = AbilityIntentType.Escape;

            Range = Mathf.Max(0.1f, Range);
            LandingSearchRadius = Mathf.Max(0f, LandingSearchRadius);
            TravelDurationSeconds = Mathf.Max(0f, TravelDurationSeconds);
            TravelDurationPerUnit = Mathf.Max(0f, TravelDurationPerUnit);
            MaxTravelDurationSeconds = Mathf.Max(0f, MaxTravelDurationSeconds);
            JumpHeight = Mathf.Max(0f, JumpHeight);
            DistanceHeightBonus = Mathf.Max(0f, DistanceHeightBonus);
            MaxJumpHeight = Mathf.Max(0f, MaxJumpHeight);
            ApexHangPower = Mathf.Clamp(ApexHangPower, 0.35f, 1.5f);
            BombCount = Mathf.Max(1, BombCount);
            BombDelaySeconds = Mathf.Max(0f, BombDelaySeconds);
            BombRadius = Mathf.Max(0.05f, BombRadius);
            BombDamage = Mathf.Max(0f, BombDamage);
            BombSpreadRadius = Mathf.Max(0f, BombSpreadRadius);
        }

        public override IAbilityLogic CreateLogic()
        {
            return new BombLeapAbilityLogic(this);
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
