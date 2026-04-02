using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "VolleyProjectileAbility", menuName = "MOBA/Abilities/Volley Projectile")]
    public class VolleyProjectileAbilityDefinition : AbilityDefinition
    {
        [Header("Projectile")]
        public float Damage = 300f;
        public float Range = 10f;
        public float Speed = 20f;

        [Header("Volley")]
        [Min(1)] public int ProjectileCount = 6;
        [Min(0f)] public float SpreadAngle = 12f;
        [Min(0f)] public float DelayBetweenShots = 0f;

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.VolleyProjectileLogic(this);
        }
    }
}