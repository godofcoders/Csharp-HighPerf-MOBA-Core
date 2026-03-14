using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public class MatchParticipant
    {
        public string Name;
        public TeamType Team;
        public BrawlerDefinition SelectedBrawler;
        public bool IsAI;

        public MatchParticipant(string name, TeamType team, BrawlerDefinition brawler, bool isAI)
        {
            Name = name;
            Team = team;
            SelectedBrawler = brawler;
            IsAI = isAI;
        }
    }
}