using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Drop into the Match scene. Subscribes to MatchManager.OnStateChanged
    /// and, on the Active→Ended transition, captures the final scores into
    /// MatchResultBoard and loads the Results scene.
    ///
    /// Score capture asks the GemGrabMode singleton for current team gem
    /// totals (since that's what the player's been competing over). If
    /// GemGrabMode isn't in the scene, scores fall back to 0 and the
    /// winner is whoever MatchManager named.
    ///
    /// Brief delay before transition (default 1.5s) so the "Match Over"
    /// state is briefly visible / audible before the scene swap.
    /// </summary>
    public class MatchEndRouter : MonoBehaviour
    {
        [Tooltip("Seconds to hold on the Match scene after MatchManager goes Ended before loading Results. Lets victory SFX/anim play.")]
        [Min(0f)]
        [SerializeField] private float _delayBeforeResultsSeconds = 1.5f;

        private TeamType _capturedWinner = TeamType.Blue;
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
            else if (GemGrabMode.Instance != null)
            {
                blue = GemGrabMode.Instance.BlueTeamGems;
                red = GemGrabMode.Instance.RedTeamGems;
                if (MatchManager.Instance == null || !MatchManager.Instance.WinnerKnown)
                    _capturedWinner = blue >= red ? TeamType.Blue : TeamType.Red;
            }

            MatchResultBoard.Capture(_capturedWinner, blue, red);

            // MVP snapshot, if a MatchStatsTracker was in the scene.
            if (MatchStatsTracker.Instance != null)
            {
                BrawlerController mvp = MatchStatsTracker.Instance.FindMVP();
                if (mvp != null)
                {
                    string mvpName = mvp.Definition != null && !string.IsNullOrWhiteSpace(mvp.Definition.BrawlerName)
                        ? mvp.Definition.BrawlerName
                        : (mvp.Definition != null ? mvp.Definition.name : "Unknown");
                    MatchResultBoard.CaptureMvp(mvpName, MatchStatsTracker.Instance.GetStats(mvp));
                }
            }

            Invoke(nameof(GoToResults), _delayBeforeResultsSeconds);
        }

        private void GoToResults()
        {
            SceneFlow.Instance?.LoadScene(SceneId.Results);
        }
    }
}
