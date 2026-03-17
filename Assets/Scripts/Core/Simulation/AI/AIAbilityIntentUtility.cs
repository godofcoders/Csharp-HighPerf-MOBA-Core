using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public static class AIAbilityIntentUtility
    {
        public static bool IsFinisher(AbilityDefinition ability)
        {
            return ability != null && ability.HasTag(AbilityTag.Finisher);
        }

        public static bool IsEngage(AbilityDefinition ability)
        {
            return ability != null && ability.HasTag(AbilityTag.Engage);
        }

        public static bool IsEscape(AbilityDefinition ability)
        {
            return ability != null && ability.HasTag(AbilityTag.Escape);
        }

        public static bool IsDefensive(AbilityDefinition ability)
        {
            return ability != null && ability.HasTag(AbilityTag.Defensive);
        }

        public static bool IsAoE(AbilityDefinition ability)
        {
            return ability != null && ability.HasTag(AbilityTag.AoE);
        }
    }
}