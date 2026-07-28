using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public struct QuestMatchReport
    {
        public BrawlerDefinition BrawlerDefinition;
        public string BrawlerName;
        public GameModeId Mode;
        public bool WonMatch;
        public MatchStats Stats;

        public QuestMatchReport(
            BrawlerDefinition brawlerDefinition,
            string brawlerName,
            GameModeId mode,
            bool wonMatch,
            MatchStats stats)
        {
            BrawlerDefinition = brawlerDefinition;
            BrawlerName = brawlerName ?? string.Empty;
            Mode = mode;
            WonMatch = wonMatch;
            Stats = stats;
        }
    }
}
