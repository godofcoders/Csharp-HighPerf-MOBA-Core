using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "BasicSuper", menuName = "MOBA/Abilities/Super/Basic Super")]
    public class BasicSuperDefinition : AbilityDefinition
    {
        public float ProjectileSpeed = 12f;
        public float Range = 10f;
        public float Damage = 1200f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.Super;
        }

        public override IAbilityLogic CreateLogic()
        {
            return new BasicSuperLogic(this);
        }
    }
}