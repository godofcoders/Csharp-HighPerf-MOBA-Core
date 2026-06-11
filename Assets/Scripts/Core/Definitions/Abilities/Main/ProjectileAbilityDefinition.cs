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
        [Tooltip("Total spread angle in degrees across all projectiles. 0 = single line. Used by aim preview to render a fan boundary; gameplay logic decides actual per-projectile spread.")]
        [Min(0f)]
        public float SpreadAngle = 0f;
        [Tooltip("Side-to-side spacing for multi-projectile straight attacks. This keeps Colt-style shots readable as lanes instead of stacked duplicates.")]
        [Min(0f)]
        public float ParallelLaneSpacing = 0.86f;
        [Tooltip("Forward spawn offset along the aim direction. Keeps projectiles from appearing inside the brawler model while preserving aim-based travel.")]
        [Min(0f)]
        public float ForwardSpawnOffset = 0.65f;
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
                SpreadAngle,
                ParallelLaneSpacing,
                ForwardSpawnOffset,
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
