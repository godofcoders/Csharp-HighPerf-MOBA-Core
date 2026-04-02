using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BurstSequenceProjectileAbility", menuName = "MOBA/Abilities/Burst Sequence Projectile")]
    public class BurstSequenceProjectileAbilityDefinition : AbilityDefinition
    {
        [Header("Projectile")]
        public float Damage = 120f;
        public float Range = 10f;
        public float Speed = 30f;

        [Header("Burst")]
        [Min(1)] public int ProjectileCount = 6;

        [Tooltip("Spacing between bullets along the forward firing lane.")]
        [Min(0f)] public float ForwardSpacing = 0.25f;

        [Tooltip("Optional slight random spread in degrees. Keep very low for Colt.")]
        [Min(0f)] public float RandomSpreadAngle = 0.25f;

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.BurstSequenceProjectileLogic(this);
        }
    }
}