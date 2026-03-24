using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BasicProjectileAttack", menuName = "MOBA/Abilities/Main Attack/Basic Projectile")]
    public class BasicProjectileAttackDefinition : AbilityDefinition
    {
        public float ProjectileSpeed = 10f;
        public float Range = 8f;
        public float Damage = 500f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.MainAttack;
        }

        public override IAbilityLogic CreateLogic()
        {
            return new BasicProjectileAttackLogic(this);
        }
    }
}