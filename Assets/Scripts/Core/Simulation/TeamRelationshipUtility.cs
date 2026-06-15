namespace MOBA.Core.Simulation
{
    public static class TeamRelationshipUtility
    {
        public const int MaxSoloTeams = 10;

        public static bool IsNeutral(TeamType team)
        {
            return team == TeamType.Neutral;
        }

        public static bool IsSoloTeam(TeamType team)
        {
            return team >= TeamType.Solo1 && team <= TeamType.Solo10;
        }

        public static bool AreAllies(TeamType a, TeamType b)
        {
            if (IsNeutral(a) || IsNeutral(b))
                return false;

            return a == b;
        }

        public static bool AreEnemies(TeamType a, TeamType b)
        {
            if (IsNeutral(a) || IsNeutral(b))
                return false;

            return a != b;
        }

        public static bool CanAffectTeam(
            ProjectileHitTeamRule hitTeamRule,
            TeamType sourceTeam,
            TeamType targetTeam)
        {
            switch (hitTeamRule)
            {
                case ProjectileHitTeamRule.EnemiesOnly:
                    return targetTeam == TeamType.Neutral ||
                           AreEnemies(sourceTeam, targetTeam);

                case ProjectileHitTeamRule.AlliesOnly:
                    return AreAllies(sourceTeam, targetTeam);

                case ProjectileHitTeamRule.AlliesAndEnemies:
                    return true;

                default:
                    return false;
            }
        }

        public static TeamType GetPrimaryEnemyTeam(TeamType team)
        {
            if (team == TeamType.Blue)
                return TeamType.Red;

            if (team == TeamType.Red)
                return TeamType.Blue;

            return TeamType.Neutral;
        }

        public static TeamType GetSoloTeam(int zeroBasedIndex)
        {
            if (zeroBasedIndex <= 0)
                return TeamType.Solo1;

            if (zeroBasedIndex >= MaxSoloTeams - 1)
                return TeamType.Solo10;

            return (TeamType)((int)TeamType.Solo1 + zeroBasedIndex);
        }
    }
}
