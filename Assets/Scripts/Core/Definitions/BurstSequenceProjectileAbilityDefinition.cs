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
        [Min(0f)] public float DelayBetweenShots = 0.05f;
        [Min(0f)] public float RandomSpreadAngle = 0.15f;
        public bool AlternateMuzzles = true;

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.BurstSequenceProjectileLogic(this);
        }
    }
}