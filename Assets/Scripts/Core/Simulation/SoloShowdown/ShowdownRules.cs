using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public static class ShowdownRules
    {
        public const int DuoTeamCount = 5;
        public const int DuoPlayersPerTeam = 2;
        public const int DuoContestantCount = DuoTeamCount * DuoPlayersPerTeam;
        public const float DuoRespawnDelaySeconds = 15f;

        public static bool IsDuoSelected =>
            SceneSelection.SelectedMode == GameModeId.SoloShowdown &&
            SceneSelection.SelectedShowdownVariant == ShowdownVariant.Duo;

        public static TeamType GetDuoTeamForRosterIndex(int rosterIndex)
        {
            int safeIndex = UnityEngine.Mathf.Clamp(rosterIndex, 0, DuoContestantCount - 1);
            return TeamRelationshipUtility.GetSoloTeam(safeIndex / DuoPlayersPerTeam);
        }

        public static bool IsDuoTeam(TeamType team)
        {
            int teamIndex = (int)team - (int)TeamType.Solo1;
            return teamIndex >= 0 && teamIndex < DuoTeamCount;
        }

        public static int GetDuoSpawnOrdinal(TeamType team, int teammateIndex)
        {
            int teamIndex = UnityEngine.Mathf.Clamp(
                (int)team - (int)TeamType.Solo1,
                0,
                DuoTeamCount - 1);
            int safeTeammateIndex = UnityEngine.Mathf.Clamp(
                teammateIndex,
                0,
                DuoPlayersPerTeam - 1);
            return teamIndex * DuoPlayersPerTeam + safeTeammateIndex;
        }

        public static bool IsTeamEliminated(int livingMembers)
        {
            return livingMembers <= 0;
        }
    }
}
