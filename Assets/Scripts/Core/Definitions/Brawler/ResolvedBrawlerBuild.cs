using System.Collections.Generic;

namespace MOBA.Core.Definitions
{
    public sealed class ResolvedBrawlerBuild
    {
        public readonly List<GadgetDefinition> Gadgets = new List<GadgetDefinition>(2);
        public readonly List<PassiveDefinition> PassiveOptions = new List<PassiveDefinition>(4);
        public HyperchargeDefinition Hypercharge;
    }
}