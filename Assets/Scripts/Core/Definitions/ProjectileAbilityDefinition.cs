using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewProjectileAbility", menuName = "MOBA/Abilities/Projectile")]
    public class ProjectileAbilityDefinition : AbilityDefinition
    {
        public float Damage = 500f;
        public float Range = 10f;
        public float Speed = 15f;
        public int ProjectileCount = 1;

        public override IAbilityLogic CreateLogic()
        {
            // The Factory creates the POCO with the SO's data
            return new StraightProjectileLogic(Damage, Range, Speed, ProjectileCount);
        }
    }
}