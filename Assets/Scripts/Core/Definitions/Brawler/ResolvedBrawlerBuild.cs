using System.Collections.Generic;

namespace MOBA.Core.Definitions
{
    public sealed class ResolvedBrawlerBuild
    {
        public AbilityDefinition MainAttack;
        public AbilityDefinition SuperAbility;
        public readonly List<GadgetDefinition> Gadgets = new List<GadgetDefinition>(2);
        public readonly List<PassiveDefinition> PassiveOptions = new List<PassiveDefinition>(4);
        public readonly List<BrawlerSkillTreeNodeDefinition> SkillTreeNodes =
            new List<BrawlerSkillTreeNodeDefinition>(8);
        public HyperchargeDefinition Hypercharge;
    }
}
