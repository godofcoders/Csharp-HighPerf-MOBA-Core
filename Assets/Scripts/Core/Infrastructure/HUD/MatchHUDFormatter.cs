using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class MatchHUDFormatter
    {
        public static string FormatActiveGemGrabStatus(
            int blueGems,
            int redGems,
            int gemsToWin,
            bool hasLeader,
            TeamType leadingTeam,
            float winTimerRemainingSeconds,
            float matchTimeRemainingSeconds)
        {
            int safeTarget = Mathf.Max(1, gemsToWin);
            string status =
                $"Gems  Blue {blueGems}/{safeTarget}  Red {redGems}/{safeTarget}  Match {FormatClock(matchTimeRemainingSeconds)}";

            if (!hasLeader)
                return status;

            return status + $"  {leadingTeam} hold {Mathf.Max(0f, winTimerRemainingSeconds):0.0}s";
        }

        public static string FormatGemGrabHoldStatus(
            bool hasLeader,
            TeamType leadingTeam,
            float winTimerRemainingSeconds)
        {
            if (!hasLeader)
                return "Collect gems";

            return $"{leadingTeam} hold {Mathf.Max(0f, winTimerRemainingSeconds):0.0}s";
        }

        public static string FormatClock(float seconds)
        {
            float clamped = Mathf.Max(0f, seconds);
            int minutes = Mathf.FloorToInt(clamped / 60f);
            int remainder = Mathf.FloorToInt(clamped - minutes * 60f);
            return $"{minutes}:{remainder:00}";
        }
    }
}
