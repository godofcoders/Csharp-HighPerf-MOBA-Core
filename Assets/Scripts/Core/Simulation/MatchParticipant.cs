using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public class MatchParticipant
    {
        public string Name;
        public TeamType Team;
        public BrawlerDefinition SelectedBrawler;
        public BrawlerBuildDefinition SelectedBuild;
        public int PowerLevel;
        public bool IsAI;

        public MatchParticipant(
            string name,
            TeamType team,
            BrawlerDefinition brawler,
            bool isAI,
            BrawlerBuildDefinition build = null,
            int powerLevel = 0)
        {
            Name = name;
            Team = team;
            SelectedBrawler = brawler;
            IsAI = isAI;
            SelectedBuild = build;
            PowerLevel = powerLevel;
        }
    }
}
