using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "EffectAoEAbility", menuName = "MOBA/Abilities/Effect AoE Ability")]
    public class EffectAoEAbilityDefinition : AoEAbilityDefinition
    {
        [Header("Target Rules")]
        public MOBA.Core.Simulation.AbilityTargetTeamRule TargetTeamRule = MOBA.Core.Simulation.AbilityTargetTeamRule.Enemy;
        public MOBA.Core.Simulation.AbilityTargetSelectionRule TargetSelectionRule = MOBA.Core.Simulation.AbilityTargetSelectionRule.Nearest;
        public bool IncludeSelf = false;
        public bool RequireAlive = true;

        [Header("Effects")]
        public AbilityEffectDefinition[] Effects;
    }
}