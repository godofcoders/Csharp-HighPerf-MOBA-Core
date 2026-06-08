using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public enum BrawlerHealthBarPerspective
    {
        Unknown,
        Own,
        Ally,
        Enemy,
        Neutral
    }

    public static class BrawlerHealthBarColorUtility
    {
        public static BrawlerHealthBarPerspective ResolvePerspective(
            TeamType subjectTeam,
            int subjectEntityId,
            TeamType localTeam,
            int localEntityId)
        {
            if (subjectTeam == TeamType.Neutral)
                return BrawlerHealthBarPerspective.Neutral;

            if (localTeam == TeamType.Neutral || localEntityId == 0)
                return BrawlerHealthBarPerspective.Unknown;

            if (subjectEntityId != 0 && subjectEntityId == localEntityId)
                return BrawlerHealthBarPerspective.Own;

            return subjectTeam == localTeam
                ? BrawlerHealthBarPerspective.Ally
                : BrawlerHealthBarPerspective.Enemy;
        }
    }
}
