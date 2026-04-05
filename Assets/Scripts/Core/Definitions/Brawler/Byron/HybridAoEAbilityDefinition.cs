using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "HybridAoEAbility", menuName = "MOBA/Abilities/Hybrid AoE")]
    public class HybridAoEAbilityDefinition : AoEAbilityDefinition
    {
        [Header("Hybrid Payload")]
        public float EnemyDamage = 400f;
        public float AllyHeal = 400f;
        [Header("Presentation")]
        public ProjectilePresentationProfile PresentationProfile;

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.Abilities.HybridAoEAbilityLogic(this);
        }
    }
}