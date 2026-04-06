using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewProjectileAbility", menuName = "MOBA/Abilities/Projectile")]
    public class ProjectileAbilityDefinition : AbilityDefinition
    {
        [Header("Projectile")]
        public float Damage = 500f;
        public float Range = 10f;
        public float Speed = 15f;
        public int ProjectileCount = 1;
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
            return new StraightProjectileLogic(
                Damage,
                Range,
                Speed,
                ProjectileCount,
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