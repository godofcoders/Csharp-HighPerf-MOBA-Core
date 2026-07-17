using MOBA.Core.Simulation;
using MOBA.Core.Infrastructure;
using UnityEngine;

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
        public readonly GameModeId Mode;
        public readonly AIGameModeMacroCall Call;
        public readonly AIGameModeObjectivePhase Phase;
        public readonly int OwnGems;
        public readonly int EnemyGems;
        public readonly int GemsToWin;
        public readonly int OwnScore;
        public readonly int EnemyScore;
        public readonly int ScoreToWin;
        public readonly float WinTimerRemainingSeconds;
        public readonly float MatchTimeRemainingSeconds;
        public readonly bool IsLeading;
        public readonly bool IsBehind;
        public readonly bool OwnTeamHasCountdown;
        public readonly bool EnemyTeamHasCountdown;
        public readonly string Reason;

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
            : this(
                GameModeId.GemGrab,
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
                enemyTeamHasCountdown,
                string.Empty)
        {
        }

        public AIGameModeMacroState(
            GameModeId mode,
            AIGameModeMacroCall call,
            AIGameModeObjectivePhase phase,
            int ownScore,
            int enemyScore,
            int scoreToWin,
            float winTimerRemainingSeconds,
            float matchTimeRemainingSeconds,
            bool isLeading,
            bool isBehind,
            bool ownTeamHasCountdown,
            bool enemyTeamHasCountdown,
            string reason)
        {
            Mode = mode;
            Call = call;
            Phase = phase;
            OwnGems = ownScore;
            EnemyGems = enemyScore;
            GemsToWin = scoreToWin;
            OwnScore = ownScore;
            EnemyScore = enemyScore;
            ScoreToWin = scoreToWin;
            WinTimerRemainingSeconds = winTimerRemainingSeconds;
            MatchTimeRemainingSeconds = matchTimeRemainingSeconds;
            IsLeading = isLeading;
            IsBehind = isBehind;
            OwnTeamHasCountdown = ownTeamHasCountdown;
            EnemyTeamHasCountdown = enemyTeamHasCountdown;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
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

            string scoreLabel = Mode == GameModeId.GemGrab
                ? $"Gems={OwnScore}/{EnemyScore}/{ScoreToWin}"
                : $"Score={OwnScore}/{EnemyScore}/{ScoreToWin}";

            return
                $"Mode={Mode} " +
                $"Macro={Call} " +
                $"Phase={Phase} " +
                $"{scoreLabel} " +
                $"Lead={IsLeading} Behind={IsBehind} " +
                $"Timer={WinTimerRemainingSeconds:0.0} " +
                $"Clock={MatchTimeRemainingSeconds:0.0} " +
                $"Reason={Reason}";
        }
    }

    public static class AIGameModeMacroStrategy
    {
        private const float FinalPressureSeconds = 30f;

        public static AIGameModeMacroState ResolveCurrentMode(TeamType team)
        {
            if (team == TeamType.Neutral)
                return AIGameModeMacroState.Neutral;

            if (ServiceProvider.TryGet<IAIGameModeMacroStateProvider>(out var provider))
            {
                if (provider is UnityEngine.Object unityProvider && unityProvider == null)
                {
                    ServiceProvider.Unregister<IAIGameModeMacroStateProvider>();
                }
                else if (provider != null &&
                         provider.TryResolveMacroState(team, out AIGameModeMacroState providedState))
                {
                    return providedState;
                }
            }

            if (GemGrabMode.Instance != null)
                return ResolveGemGrab(GemGrabMode.Instance, team);

            if (KnockoutMode.Instance != null)
                return ResolveKnockout(KnockoutMode.Instance, team);

            if (SoloShowdownMode.Instance != null)
                return ResolveSoloShowdown(SoloShowdownMode.Instance, team);

            switch (SceneSelection.SelectedMode)
            {
                case GameModeId.Knockout:
                    return NeutralForMode(GameModeId.Knockout, "knockout_unavailable");

                case GameModeId.SoloShowdown:
                    return NeutralForMode(GameModeId.SoloShowdown, "showdown_unavailable");

                case GameModeId.BrawlBall:
                    return ResolveBrawlBall(
                        ownGoals: 0,
                        enemyGoals: 0,
                        goalsToWin: 2,
                        ownHasBall: false,
                        enemyHasBall: false,
                        matchTimeRemainingSeconds: 0f);

                case GameModeId.HotZone:
                    return ResolveHotZone(
                        ownProgress: 0f,
                        enemyProgress: 0f,
                        progressToWin: 100f,
                        ownControllingZone: false,
                        enemyControllingZone: false,
                        matchTimeRemainingSeconds: 0f);

                case GameModeId.GemGrab:
                default:
                    return NeutralForMode(GameModeId.GemGrab, "gem_grab_unavailable");
            }
        }

        public static AIGameModeMacroState ResolveKnockout(
            KnockoutMode mode,
            TeamType team)
        {
            if (mode == null || team == TeamType.Neutral)
                return NeutralForMode(GameModeId.Knockout, "knockout_unavailable");

            TeamType enemyTeam = GetEnemyTeam(team);
            int teamSize = Mathf.Max(
                mode.GetDisplayTeamSize(team),
                mode.GetDisplayTeamSize(enemyTeam));
            int ownRegistered = mode.GetRegisteredCount(team);
            int enemyRegistered = mode.GetRegisteredCount(enemyTeam);
            int ownAlive = ownRegistered > 0 ? mode.GetAliveCount(team) : teamSize;
            int enemyAlive = enemyRegistered > 0 ? mode.GetAliveCount(enemyTeam) : teamSize;

            return ResolveKnockout(
                mode.GetTeamRoundsWon(team),
                mode.GetTeamRoundsWon(enemyTeam),
                mode.RoundsToWin,
                ownAlive,
                enemyAlive,
                teamSize,
                matchTimeRemainingSeconds: 0f);
        }

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

        public static AIGameModeMacroState ResolveSoloShowdown(
            SoloShowdownMode mode,
            TeamType team)
        {
            if (mode == null || !TeamRelationshipUtility.IsSoloTeam(team))
                return NeutralForMode(GameModeId.SoloShowdown, "showdown_unavailable");

            return ResolveSoloShowdown(
                mode.IsTeamAlive(team) ? 1 : 0,
                mode.GetAliveOpponentCount(team),
                mode.AliveCount,
                outsideSafeZone: false,
                distanceBeyondSafeZone: 0f,
                matchTimeRemainingSeconds: 0f);
        }

        public static AIGameModeMacroState ResolveKnockout(
            int ownRoundsWon,
            int enemyRoundsWon,
            int roundsToWin,
            int ownAlive,
            int enemyAlive,
            int teamSize,
            float matchTimeRemainingSeconds)
        {
            roundsToWin = Mathf.Max(1, roundsToWin);
            teamSize = Mathf.Max(1, teamSize);
            ownAlive = Mathf.Clamp(ownAlive, 0, teamSize);
            enemyAlive = Mathf.Clamp(enemyAlive, 0, teamSize);

            bool isLeading = ownRoundsWon > enemyRoundsWon;
            bool isBehind = ownRoundsWon < enemyRoundsWon;
            bool finalPressure = IsFinalPressure(matchTimeRemainingSeconds) ||
                                 ownRoundsWon >= roundsToWin - 1 ||
                                 enemyRoundsWon >= roundsToWin - 1;
            bool anyDeaths = ownAlive < teamSize || enemyAlive < teamSize;

            AIGameModeObjectivePhase phase = finalPressure
                ? AIGameModeObjectivePhase.FinalPressure
                : anyDeaths
                    ? AIGameModeObjectivePhase.Contest
                    : AIGameModeObjectivePhase.Opening;

            AIGameModeMacroCall call;
            string reason;

            if (ownAlive <= 0 && enemyAlive > 0)
            {
                call = AIGameModeMacroCall.Reset;
                reason = "round_lost";
            }
            else if (enemyAlive <= 0 && ownAlive > 0)
            {
                call = AIGameModeMacroCall.Hold;
                reason = "round_secure";
            }
            else if (ownAlive < enemyAlive)
            {
                call = AIGameModeMacroCall.Reset;
                reason = "down_players";
            }
            else if (ownAlive > enemyAlive)
            {
                call = AIGameModeMacroCall.Push;
                reason = "numbers_advantage";
            }
            else if (finalPressure && isBehind)
            {
                call = AIGameModeMacroCall.Push;
                reason = "match_point_behind";
            }
            else if (finalPressure && isLeading)
            {
                call = AIGameModeMacroCall.Hold;
                reason = "match_point_lead";
            }
            else
            {
                call = AIGameModeMacroCall.Push;
                reason = phase == AIGameModeObjectivePhase.Opening
                    ? "opening_contest_center"
                    : "even_round_trade";
            }

            return new AIGameModeMacroState(
                GameModeId.Knockout,
                call,
                phase,
                ownRoundsWon,
                enemyRoundsWon,
                roundsToWin,
                0f,
                matchTimeRemainingSeconds,
                isLeading,
                isBehind,
                false,
                false,
                reason);
        }

        public static AIGameModeMacroState ResolveSoloShowdown(
            int ownAlive,
            int aliveOpponents,
            int totalAlive,
            bool outsideSafeZone,
            float distanceBeyondSafeZone,
            float matchTimeRemainingSeconds)
        {
            ownAlive = Mathf.Clamp(ownAlive, 0, 1);
            aliveOpponents = Mathf.Max(0, aliveOpponents);
            totalAlive = Mathf.Max(totalAlive, ownAlive + aliveOpponents);

            AIGameModeObjectivePhase phase = totalAlive <= 2
                ? AIGameModeObjectivePhase.FinalPressure
                : totalAlive <= 4
                    ? AIGameModeObjectivePhase.Contest
                    : AIGameModeObjectivePhase.Opening;

            AIGameModeMacroCall call;
            string reason;

            if (ownAlive <= 0)
            {
                call = AIGameModeMacroCall.Reset;
                reason = "eliminated";
            }
            else if (outsideSafeZone)
            {
                call = AIGameModeMacroCall.Reset;
                phase = AIGameModeObjectivePhase.FinalPressure;
                reason = distanceBeyondSafeZone > 1f
                    ? "poison_escape_urgent"
                    : "poison_escape";
            }
            else if (aliveOpponents <= 0)
            {
                call = AIGameModeMacroCall.Hold;
                reason = "last_standing";
            }
            else if (aliveOpponents == 1)
            {
                call = AIGameModeMacroCall.Push;
                phase = AIGameModeObjectivePhase.FinalPressure;
                reason = "final_duel";
            }
            else if (aliveOpponents <= 2)
            {
                call = AIGameModeMacroCall.Push;
                reason = "thin_lobby";
            }
            else
            {
                call = AIGameModeMacroCall.Hold;
                reason = "survive_field";
            }

            bool isLeading = ownAlive > 0 && aliveOpponents <= 1;
            bool isBehind = ownAlive <= 0 || outsideSafeZone;

            return new AIGameModeMacroState(
                GameModeId.SoloShowdown,
                call,
                phase,
                ownAlive,
                aliveOpponents,
                1,
                0f,
                matchTimeRemainingSeconds,
                isLeading,
                isBehind,
                false,
                false,
                reason);
        }

        public static AIGameModeMacroState ResolveBrawlBall(
            int ownGoals,
            int enemyGoals,
            int goalsToWin,
            bool ownHasBall,
            bool enemyHasBall,
            float matchTimeRemainingSeconds)
        {
            goalsToWin = Mathf.Max(1, goalsToWin);
            bool isLeading = ownGoals > enemyGoals;
            bool isBehind = ownGoals < enemyGoals;
            bool finalPressure = IsFinalPressure(matchTimeRemainingSeconds) ||
                                 ownGoals >= goalsToWin - 1 ||
                                 enemyGoals >= goalsToWin - 1;

            AIGameModeObjectivePhase phase = finalPressure
                ? AIGameModeObjectivePhase.FinalPressure
                : ownGoals == 0 && enemyGoals == 0 && !ownHasBall && !enemyHasBall
                    ? AIGameModeObjectivePhase.Opening
                    : AIGameModeObjectivePhase.Contest;

            AIGameModeMacroCall call;
            string reason;
            if (ownHasBall)
            {
                call = AIGameModeMacroCall.Push;
                reason = ownGoals >= goalsToWin - 1 ? "score_point" : "ball_possession";
            }
            else if (enemyHasBall)
            {
                call = AIGameModeMacroCall.Reset;
                reason = enemyGoals >= goalsToWin - 1 ? "defend_score_point" : "enemy_ball";
            }
            else if (finalPressure && isBehind)
            {
                call = AIGameModeMacroCall.Push;
                reason = "late_goal_needed";
            }
            else if (finalPressure && isLeading)
            {
                call = AIGameModeMacroCall.Hold;
                reason = "protect_lead";
            }
            else
            {
                call = AIGameModeMacroCall.Neutral;
                reason = "loose_ball";
            }

            return new AIGameModeMacroState(
                GameModeId.BrawlBall,
                call,
                phase,
                ownGoals,
                enemyGoals,
                goalsToWin,
                0f,
                matchTimeRemainingSeconds,
                isLeading,
                isBehind,
                false,
                false,
                reason);
        }

        public static AIGameModeMacroState ResolveHotZone(
            float ownProgress,
            float enemyProgress,
            float progressToWin,
            bool ownControllingZone,
            bool enemyControllingZone,
            float matchTimeRemainingSeconds)
        {
            progressToWin = Mathf.Max(1f, progressToWin);
            int ownScore = Mathf.RoundToInt(Mathf.Clamp(ownProgress, 0f, progressToWin));
            int enemyScore = Mathf.RoundToInt(Mathf.Clamp(enemyProgress, 0f, progressToWin));
            int scoreToWin = Mathf.RoundToInt(progressToWin);
            bool isLeading = ownProgress > enemyProgress;
            bool isBehind = ownProgress < enemyProgress;
            bool finalPressure = IsFinalPressure(matchTimeRemainingSeconds) ||
                                 ownProgress >= progressToWin - 10f ||
                                 enemyProgress >= progressToWin - 10f;

            AIGameModeObjectivePhase phase = finalPressure
                ? AIGameModeObjectivePhase.FinalPressure
                : ownProgress < 15f && enemyProgress < 15f
                    ? AIGameModeObjectivePhase.Opening
                    : AIGameModeObjectivePhase.Contest;

            AIGameModeMacroCall call;
            string reason;
            if (enemyProgress >= progressToWin - 8f ||
                (enemyControllingZone && isBehind))
            {
                call = AIGameModeMacroCall.Reset;
                reason = "deny_zone_finish";
            }
            else if (ownControllingZone && isLeading)
            {
                call = AIGameModeMacroCall.Hold;
                reason = "hold_zone_lead";
            }
            else if (isBehind || finalPressure)
            {
                call = AIGameModeMacroCall.Push;
                reason = "contest_zone";
            }
            else
            {
                call = AIGameModeMacroCall.Neutral;
                reason = "shared_zone_pressure";
            }

            return new AIGameModeMacroState(
                GameModeId.HotZone,
                call,
                phase,
                ownScore,
                enemyScore,
                scoreToWin,
                0f,
                matchTimeRemainingSeconds,
                isLeading,
                isBehind,
                false,
                false,
                reason);
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

        private static AIGameModeMacroState NeutralForMode(GameModeId mode, string reason)
        {
            return new AIGameModeMacroState(
                mode,
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
                false,
                reason);
        }

        private static bool IsFinalPressure(float matchTimeRemainingSeconds)
        {
            return matchTimeRemainingSeconds > 0f &&
                   matchTimeRemainingSeconds <= FinalPressureSeconds;
        }

        private static TeamType GetEnemyTeam(TeamType team)
        {
            return team == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        }
    }
}
