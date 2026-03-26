using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public interface IBrawlerCommandSource
    {
        void CollectCommands(List<BrawlerCommand> output, uint currentTick);
    }
}