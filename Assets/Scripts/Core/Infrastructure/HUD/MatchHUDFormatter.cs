using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class MatchHUDFormatter
    {
        public static string FormatModeMapPrefix(GameModeId mode, string mapName)
        {
            string modeName = FormatModeName(mode);
            return string.IsNullOrWhiteSpace(mapName)
                ? modeName
                : $"{modeName} - {mapName}";
        }

        public static string FormatModeName(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.GemGrab:
                    return "Gem Grab";
                case GameModeId.Knockout:
                    return "Knockout";
                case GameModeId.BrawlBall:
                    return "Brawl Ball";
                case GameModeId.HotZone:
                    return "Hot Zone";
                case GameModeId.SoloShowdown:
                    return "Solo Showdown";
                default:
                    return mode.ToString();
            }
        }

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

        public static string FormatActiveKnockoutStatus(
            int blueRounds,
            int redRounds,
            int roundsToWin,
            int blueAlive,
            int redAlive,
            int blueTeamSize,
            int redTeamSize,
            int currentRound,
            bool roundEnding)
        {
            int target = Mathf.Max(1, roundsToWin);
            string roundState = roundEnding ? "round ending" : $"alive {blueAlive}/{blueTeamSize} - {redAlive}/{redTeamSize}";
            return $"Rounds  Blue {blueRounds}/{target}  Red {redRounds}/{target}  R{Mathf.Max(1, currentRound)}  {roundState}";
        }

        public static string FormatKnockoutRoundStatus(
            int blueAlive,
            int redAlive,
            int blueTeamSize,
            int redTeamSize,
            int currentRound,
            bool roundEnding)
        {
            if (roundEnding)
                return $"Round {Mathf.Max(1, currentRound)} ending";

            return $"R{Mathf.Max(1, currentRound)} alive {blueAlive}/{blueTeamSize} - {redAlive}/{redTeamSize}";
        }

        public static string FormatActiveBrawlBallStatus(
            int blueGoals,
            int redGoals,
            int goalsToWin,
            TeamType carrierTeam)
        {
            int target = Mathf.Max(1, goalsToWin);
            string possession = carrierTeam == TeamType.Blue || carrierTeam == TeamType.Red
                ? $"  {carrierTeam} ball"
                : "  Loose ball";

            return $"Goals  Blue {blueGoals}/{target}  Red {redGoals}/{target}{possession}";
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
