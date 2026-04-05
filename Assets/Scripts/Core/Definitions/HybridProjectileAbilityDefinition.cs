using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "HybridProjectileAbility", menuName = "MOBA/Abilities/Hybrid Projectile")]
    public class HybridProjectileAbilityDefinition : AbilityDefinition
    {
        [Header("Projectile Travel")]
        public float Range = 10f;
        public float Speed = 20f;

        [Header("Hybrid Payload")]
        public float EnemyDamage = 300f;
        public float AllyHeal = 300f;

        [Header("Presentation")]
        public ProjectilePresentationProfile PresentationProfile;


        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.HybridProjectileLogic(this);
        }
    }
}