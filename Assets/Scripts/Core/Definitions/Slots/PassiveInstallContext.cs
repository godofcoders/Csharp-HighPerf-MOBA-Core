using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    public readonly struct PassiveInstallContext
    {
        public BrawlerState State { get; }
        public BrawlerController Owner { get; }
        public object SourceToken { get; }

        public PassiveInstallContext(BrawlerState state, BrawlerController owner, object sourceToken)
        {
            State = state;
            Owner = owner;
            SourceToken = sourceToken;
        }
    }
}