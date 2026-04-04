using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "ThrownHybridAoEAbility", menuName = "MOBA/Abilities/Thrown Hybrid AoE")]
    public class ThrownHybridAoEAbilityDefinition : AbilityDefinition
    {
        [Header("Throw Delivery")]
        public float ThrowRange = 7f;
        public float ThrowSpeed = 16f;

        [Header("Impact AoE")]
        public float ImpactRadius = 3f;
        public float EnemyDamage = 350f;
        public float AllyHeal = 350f;

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.Abilities.ThrownHybridAoEAbilityLogic(this);
        }
    }
}