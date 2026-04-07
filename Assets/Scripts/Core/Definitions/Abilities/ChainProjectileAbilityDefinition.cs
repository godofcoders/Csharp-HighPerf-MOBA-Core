using UnityEngine;
using MOBA.Core.Simulation.Abilities;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "ChainProjectileAbility", menuName = "MOBA/Abilities/Chain Projectile")]
    public class ChainProjectileAbilityDefinition : AbilityDefinition
    {
        [Header("Projectile")]
        public float Damage = 320f;
        public float Range = 9f;
        public float Speed = 18f;

        [Header("Chain")]
        [Min(0)] public int BounceCount = 3;
        public float BounceRadius = 5f;

        [Header("Presentation")]
        public ProjectilePresentationProfile PresentationProfile;

        private void OnValidate()
        {
            DeliveryType = AbilityDeliveryType.Projectile;

            if (SlotType != AbilitySlotType.Super && SlotType != AbilitySlotType.Gadget)
            {
                SlotType = AbilitySlotType.MainAttack;
            }

            if (TargetingType == AbilityTargetingType.Self)
            {
                TargetingType = AbilityTargetingType.Directional;
            }
        }

        public override IAbilityLogic CreateLogic()
        {
            return new ChainProjectileLogic(
                Damage,
                Range,
                Speed,
                BounceCount,
                BounceRadius,
                PresentationProfile
            );
        }

        public override float GetAIIdealRange()
        {
            return Range * 0.85f;
        }

        public override float GetAIMaxRange()
        {
            return Range;
        }
    }
}