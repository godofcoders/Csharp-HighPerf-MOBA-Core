using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public enum AIGameModeMacroCall
    {
        Neutral,
        Push,
        Hold,
        Reset
    }

    public enum AIGameModeObjectivePhase
    {
        None,
        Opening,
        Contest,
        Countdown,
        FinalPressure
    }

    public readonly struct AIGameModeMacroState
    {
        public readonly AIGameModeMacroCall Call;
        public readonly AIGameModeObjectivePhase Phase;
        public readonly int OwnGems;
        public readonly int EnemyGems;
        public readonly int GemsToWin;
        public readonly float WinTimerRemainingSeconds;
        public readonly float MatchTimeRemainingSeconds;
        public readonly bool IsLeading;
        public readonly bool IsBehind;
        public readonly bool OwnTeamHasCountdown;
        public readonly bool EnemyTeamHasCountdown;

        public AIGameModeMacroState(
            AIGameModeMacroCall call,
            AIGameModeObjectivePhase phase,
            int ownGems,
            int enemyGems,
            int gemsToWin,
            float winTimerRemainingSeconds,
            float matchTimeRemainingSeconds,
            bool isLeading,
            bool isBehind,
            bool ownTeamHasCountdown,
            bool enemyTeamHasCountdown)
        {
            Call = call;
            Phase = phase;
            OwnGems = ownGems;
            EnemyGems = enemyGems;
            GemsToWin = gemsToWin;
            WinTimerRemainingSeconds = winTimerRemainingSeconds;
            MatchTimeRemainingSeconds = matchTimeRemainingSeconds;
            IsLeading = isLeading;
            IsBehind = isBehind;
            OwnTeamHasCountdown = ownTeamHasCountdown;
            EnemyTeamHasCountdown = enemyTeamHasCountdown;
        }

        public static AIGameModeMacroState Neutral => new AIGameModeMacroState(
            AIGameModeMacroCall.Neutral,
            AIGameModeObjectivePhase.None,
            0,
            0,
            0,
            0f,
            0f,
            false,
            false,
            false,
            false);

        public string GetDebugSummary()
        {
            if (Phase == AIGameModeObjectivePhase.None)
                return "Macro=None";

            return
                $"Macro={Call} " +
                $"Phase={Phase} " +
                $"Gems={OwnGems}/{EnemyGems}/{GemsToWin} " +
                $"Lead={IsLeading} Behind={IsBehind} " +
                $"Timer={WinTimerRemainingSeconds:0.0} " +
                $"Clock={MatchTimeRemainingSeconds:0.0}";
        }
    }

    public static class AIGameModeMacroStrategy
    {
        private const float FinalPressureSeconds = 30f;

        public static AIGameModeMacroState ResolveGemGrab(
            GemGrabMode mode,
            TeamType team)
        {
            if (mode == null || team == TeamType.Neutral)
                return AIGameModeMacroState.Neutral;

            int ownGems = mode.GetTeamGemCount(team);
            int enemyGems = mode.GetTeamGemCount(GetEnemyTeam(team));
            bool ownCountdown = mode.HasLeader && mode.LeadingTeam == team;
            bool enemyCountdown = mode.HasLeader && mode.LeadingTeam == GetEnemyTeam(team);

            return ResolveGemGrab(
                ownGems,
                enemyGems,
                mode.GemsToWin,
                mode.WinTimerRemainingSeconds,
                mode.MatchTimeRemainingSeconds,
                ownCountdown,
                enemyCountdown);
        }

        public static AIGameModeMacroState ResolveGemGrab(
            int ownGems,
            int enemyGems,
            int gemsToWin,
            float winTimerRemainingSeconds,
            float matchTimeRemainingSeconds,
            bool ownTeamHasCountdown,
            bool enemyTeamHasCountdown)
        {
            gemsToWin = gemsToWin > 0 ? gemsToWin : 10;

            bool isLeading = ownGems > enemyGems;
            bool isBehind = ownGems < enemyGems;
            bool finalPressure = matchTimeRemainingSeconds > 0f &&
                                 matchTimeRemainingSeconds <= FinalPressureSeconds;

            AIGameModeObjectivePhase phase = ResolvePhase(
                ownGems,
                enemyGems,
                gemsToWin,
                ownTeamHasCountdown,
                enemyTeamHasCountdown,
                finalPressure);

            AIGameModeMacroCall call = ResolveCall(
                ownGems,
                enemyGems,
                gemsToWin,
                ownTeamHasCountdown,
                enemyTeamHasCountdown,
                finalPressure);

            return new AIGameModeMacroState(
                call,
                phase,
                ownGems,
                enemyGems,
                gemsToWin,
                winTimerRemainingSeconds,
                matchTimeRemainingSeconds,
                isLeading,
                isBehind,
                ownTeamHasCountdown,
                enemyTeamHasCountdown);
        }

        private static AIGameModeObjectivePhase ResolvePhase(
            int ownGems,
            int enemyGems,
            int gemsToWin,
            bool ownTeamHasCountdown,
            bool enemyTeamHasCountdown,
            bool finalPressure)
        {
            if (ownTeamHasCountdown || enemyTeamHasCountdown)
                return AIGameModeObjectivePhase.Countdown;

            if (finalPressure)
                return AIGameModeObjectivePhase.FinalPressure;

            int maxGems = ownGems > enemyGems ? ownGems : enemyGems;
            if (maxGems <= 3)
                return AIGameModeObjectivePhase.Opening;

            return AIGameModeObjectivePhase.Contest;
        }

        private static AIGameModeMacroCall ResolveCall(
            int ownGems,
            int enemyGems,
            int gemsToWin,
            bool ownTeamHasCountdown,
            bool enemyTeamHasCountdown,
            bool finalPressure)
        {
            if (ownTeamHasCountdown)
                return AIGameModeMacroCall.Hold;

            if (enemyTeamHasCountdown)
                return AIGameModeMacroCall.Reset;

            int gemDelta = ownGems - enemyGems;
            if (finalPressure && gemDelta < 0)
                return AIGameModeMacroCall.Push;

            if (enemyGems >= gemsToWin - 1 && gemDelta <= 0)
                return AIGameModeMacroCall.Reset;

            if (ownGems >= gemsToWin - 1 && gemDelta >= 0)
                return AIGameModeMacroCall.Hold;

            if (gemDelta <= -3)
                return AIGameModeMacroCall.Push;

            if (gemDelta >= 3 && ownGems >= 5)
                return AIGameModeMacroCall.Hold;

            return AIGameModeMacroCall.Neutral;
        }

        private static TeamType GetEnemyTeam(TeamType team)
        {
            return team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        }
    }
}
