using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Drop into the Match scene. Subscribes to MatchManager.OnStateChanged
    /// and, on the Active→Ended transition, captures the final scores into
    /// MatchResultBoard and loads the Results scene.
    ///
    /// Score capture asks the active mode singleton for its mode-specific
    /// score totals. If no supported mode is in the scene, scores fall
    /// back to MatchManager and the winner is whoever MatchManager named.
    ///
    /// Brief delay before transition (default 1.5s) so the "Match Over"
    /// state is briefly visible / audible before the scene swap.
    /// </summary>
    public class MatchEndRouter : MonoBehaviour
    {
        [Tooltip("Seconds to hold on the Match scene after MatchManager goes Ended before loading Results. Lets victory SFX/anim play.")]
        [Min(0f)]
        [SerializeField] private float _delayBeforeResultsSeconds = 1.5f;

        private TeamType _capturedWinner = TeamType.Neutral;
        private bool _routed;

        private void OnEnable()
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(MatchState state)
        {
            if (state != MatchState.Ended || _routed) return;
            _routed = true;

            // Capture scores immediately so any state mutations between
            // now and the delayed scene-load don't taint the snapshot.
            int blue = MatchManager.Instance != null
                ? MatchManager.Instance.GetScore(TeamType.Blue)
                : 0;
            int red = MatchManager.Instance != null
                ? MatchManager.Instance.GetScore(TeamType.Red)
                : 0;
            if (MatchManager.Instance != null &&
                MatchManager.Instance.TryGetWinner(out TeamType winner))
            {
                _capturedWinner = winner;
            }

            if (KnockoutMode.Instance != null)
            {
                blue = KnockoutMode.Instance.BlueRoundsWon;
                red = KnockoutMode.Instance.RedRoundsWon;
                if (MatchManager.Instance == null || !MatchManager.Instance.WinnerKnown)
                    _capturedWinner = blue >= red ? TeamType.Blue : TeamType.Red;
            }
            else if (BrawlBallMode.Instance != null)
            {
                blue = BrawlBallMode.Instance.BlueGoals;
                red = BrawlBallMode.Instance.RedGoals;
                if (MatchManager.Instance == null || !MatchManager.Instance.WinnerKnown)
                {
                    _capturedWinner = BrawlBallMode.Instance.IsMatchResolved
                        ? BrawlBallMode.Instance.ResolvedWinner
                        : ResolveWinnerFromScore(blue, red);
                }
            }
            else if (GemGrabMode.Instance != null)
            {
                blue = GemGrabMode.Instance.BlueTeamGems;
                red = GemGrabMode.Instance.RedTeamGems;
                if (MatchManager.Instance == null || !MatchManager.Instance.WinnerKnown)
                    _capturedWinner = blue >= red ? TeamType.Blue : TeamType.Red;
            }

            TeamType localPlayerTeam = ResolveLocalPlayerTeam();
            MatchResultBoard.Capture(_capturedWinner, blue, red, localPlayerTeam);

            CaptureMatchStats(_capturedWinner, blue, red);

            Invoke(nameof(GoToResults), _delayBeforeResultsSeconds);
        }

        private static void CaptureMatchStats(TeamType winner, int blueScore, int redScore)
        {
            MatchStatsTracker tracker = MatchStatsTracker.Instance;
            if (tracker == null || tracker.Stats.Count == 0)
            {
                MatchResultBoard.CaptureEntries(null);
                return;
            }

            bool soloShowdown = SceneSelection.SelectedMode == GameModeId.SoloShowdown;
            BrawlerController localPlayer = ResolveLocalPlayerBrawler();
            bool restrictToLocalPlayer = soloShowdown && localPlayer != null;
            BrawlerController starPlayer = null;
            float bestScore = float.NegativeInfinity;

            foreach (KeyValuePair<BrawlerController, MatchStats> kvp in tracker.Stats)
            {
                BrawlerController brawler = kvp.Key;
                if (ShouldSkipResultEntry(brawler, restrictToLocalPlayer, localPlayer))
                    continue;

                MatchStats stats = kvp.Value;
                int ownScore = ResolveTeamScore(brawler.Team, blueScore, redScore);
                int enemyScore = ResolveTeamScore(GetEnemyTeam(brawler.Team), blueScore, redScore);
                float starScore = ComputeStarScore(
                    stats,
                    SceneSelection.SelectedMode,
                    brawler.Team,
                    winner,
                    ownScore,
                    enemyScore);

                if (starScore > bestScore)
                {
                    bestScore = starScore;
                    starPlayer = brawler;
                }
            }

            List<MatchResultEntry> entries = new List<MatchResultEntry>(tracker.Stats.Count);
            foreach (KeyValuePair<BrawlerController, MatchStats> kvp in tracker.Stats)
            {
                BrawlerController brawler = kvp.Key;
                if (ShouldSkipResultEntry(brawler, restrictToLocalPlayer, localPlayer))
                    continue;

                MatchStats stats = kvp.Value;
                int ownScore = ResolveTeamScore(brawler.Team, blueScore, redScore);
                int enemyScore = ResolveTeamScore(GetEnemyTeam(brawler.Team), blueScore, redScore);
                float starScore = ComputeStarScore(
                    stats,
                    SceneSelection.SelectedMode,
                    brawler.Team,
                    winner,
                    ownScore,
                    enemyScore);

                entries.Add(new MatchResultEntry
                {
                    DisplayName = ResolveBrawlerName(brawler),
                    Team = brawler.Team,
                    Definition = brawler.Definition,
                    Stats = stats,
                    StarScore = starScore,
                    IsStarPlayer = brawler == starPlayer,
                    IsLocalPlayer = brawler == localPlayer
                });
            }

            if (starPlayer != null)
            {
                MatchResultBoard.CaptureMvp(
                    ResolveBrawlerName(starPlayer),
                    tracker.GetStats(starPlayer));
            }

            entries.Sort(CompareResultEntries);
            MatchResultBoard.CaptureEntries(entries.ToArray());
            CaptureQuestProgress(localPlayer, winner, tracker);
        }

        private static void CaptureQuestProgress(
            BrawlerController localPlayer,
            TeamType winner,
            MatchStatsTracker tracker)
        {
            if (localPlayer == null || tracker == null)
                return;

            MatchStats stats = tracker.GetStats(localPlayer);
            bool wonMatch = localPlayer.Team != TeamType.Neutral &&
                            localPlayer.Team == winner;

            QuestProgressionService.ApplyMatchReport(new QuestMatchReport(
                localPlayer.Definition,
                ResolveBrawlerName(localPlayer),
                SceneSelection.SelectedMode,
                wonMatch,
                stats));
        }

        private static bool ShouldSkipResultEntry(
            BrawlerController brawler,
            bool restrictToLocalPlayer,
            BrawlerController localPlayer)
        {
            if (brawler == null)
                return true;

            return restrictToLocalPlayer && brawler != localPlayer;
        }

        private static float ComputeStarScore(
            MatchStats stats,
            GameModeId mode,
            TeamType team,
            TeamType matchWinner,
            int ownModeScore,
            int enemyModeScore)
        {
            float combatScore =
                stats.Kills * 180f +
                stats.Assists * 85f +
                stats.DamageDealt * 0.07f +
                stats.HealingDone * 0.08f +
                stats.DamageTaken * 0.015f -
                stats.Deaths * 70f;

            return combatScore + ComputeModeObjectiveScore(
                stats,
                mode,
                team,
                matchWinner,
                ownModeScore,
                enemyModeScore);
        }

        private static float ComputeModeObjectiveScore(
            MatchStats stats,
            GameModeId mode,
            TeamType team,
            TeamType matchWinner,
            int ownModeScore,
            int enemyModeScore)
        {
            bool wonMatch = team != TeamType.Neutral && team == matchWinner;
            int scoreLead = ownModeScore - enemyModeScore;

            switch (mode)
            {
                case GameModeId.GemGrab:
                    return stats.GemsCollected * 190f + (wonMatch ? 140f : 0f);

                case GameModeId.Knockout:
                    return stats.Kills * 110f + stats.Assists * 50f - stats.Deaths * 85f + (wonMatch ? 170f : 0f);

                case GameModeId.BrawlBall:
                    return Mathf.Max(0, scoreLead) * 170f + (wonMatch ? 220f : 0f);

                case GameModeId.HotZone:
                    return Mathf.Max(0, scoreLead) * 3f + (wonMatch ? 230f : 0f);

                case GameModeId.SoloShowdown:
                    return stats.Kills * 140f - stats.Deaths * 120f + (wonMatch ? 260f : 0f);

                default:
                    return wonMatch ? 150f : 0f;
            }
        }

        private static int CompareResultEntries(MatchResultEntry left, MatchResultEntry right)
        {
            if (left.IsStarPlayer != right.IsStarPlayer)
                return left.IsStarPlayer ? -1 : 1;

            if (left.Team != right.Team)
                return GetTeamSortOrder(left.Team).CompareTo(GetTeamSortOrder(right.Team));

            return right.StarScore.CompareTo(left.StarScore);
        }

        private static int GetTeamSortOrder(TeamType team)
        {
            if (team == TeamType.Blue)
                return 0;

            if (team == TeamType.Red)
                return 1;

            if (team == TeamType.Neutral)
                return 99;

            return 10 + (int)team;
        }

        private static int ResolveTeamScore(TeamType team, int blueScore, int redScore)
        {
            if (team == TeamType.Blue)
                return blueScore;

            if (team == TeamType.Red)
                return redScore;

            return 0;
        }

        private static TeamType GetEnemyTeam(TeamType team)
        {
            if (team == TeamType.Blue)
                return TeamType.Red;

            if (team == TeamType.Red)
                return TeamType.Blue;

            return TeamType.Neutral;
        }

        private static string ResolveBrawlerName(BrawlerController brawler)
        {
            if (brawler == null)
                return "Unknown";

            if (brawler.Definition != null && !string.IsNullOrWhiteSpace(brawler.Definition.BrawlerName))
                return brawler.Definition.BrawlerName;

            return brawler.Definition != null ? brawler.Definition.name : brawler.name;
        }

        private static TeamType ResolveWinnerFromScore(int blue, int red)
        {
            if (blue > red)
                return TeamType.Blue;

            if (red > blue)
                return TeamType.Red;

            return TeamType.Neutral;
        }

        private static TeamType ResolveLocalPlayerTeam()
        {
            BrawlerController localPlayer = ResolveLocalPlayerBrawler();
            if (localPlayer != null)
                return localPlayer.Team;

            return BrawlerController.TryGetLocalObserverTeam(out TeamType team)
                ? team
                : TeamType.Neutral;
        }

        private static BrawlerController ResolveLocalPlayerBrawler()
        {
            PlayerCommandSource[] sources = FindObjectsOfType<PlayerCommandSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                PlayerCommandSource source = sources[i];
                if (source == null)
                    continue;

                BrawlerController brawler = source.GetComponent<BrawlerController>();
                if (brawler != null)
                    return brawler;
            }

            return null;
        }

        private void GoToResults()
        {
            SceneFlow.Instance?.LoadScene(SceneId.Results);
        }
    }
}
