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
            return new StraightProjectileLogic(Damage, Range, Speed, ProjectileCount);
        }

        public override float GetAIIdealRange()
        {
            // Slightly under max projectile range usually feels better than max-edge fighting.
            return Range * 0.85f;
        }

        public override float GetAIMaxRange()
        {
            return Range;
        }
    }
}