using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public static class MatchStateUtility
    {
        public static bool IsCombatResolutionOpen()
        {
            MatchManager matchManager = MatchManager.Instance;
            return matchManager == null || matchManager.CurrentState == MatchState.Active;
        }

        public static bool IsMatchEnded()
        {
            MatchManager matchManager = MatchManager.Instance;
            return matchManager != null && matchManager.CurrentState == MatchState.Ended;
        }
    }
}
