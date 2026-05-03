using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Drop into the Match scene. Logs Errors / Warnings on Awake for any
    /// required component that's missing or unwired. Catches the
    /// "playtest doesn't spawn anything" class of bugs before you have to
    /// guess.
    ///
    /// Editor-time noise only; in a shipped build this still runs but is
    /// a single Awake pass and exits.
    /// </summary>
    public class MatchSceneSetupChecker : MonoBehaviour
    {
        [Tooltip("If true, also log a green 'all good' confirmation when nothing's missing. Off by default to avoid console noise.")]
        [SerializeField] private bool _logSuccess = false;

        private void Awake()
        {
            int errors = 0;

            // ---------- Required scene-scoped singletons ----------
            errors += Require<MatchManager>("MatchManager", "drives match-state lifecycle (Waiting → CountingDown → Active → Ended)");
            errors += Require<SpawnManager>("SpawnManager", "instantiates brawlers from the roster + handles respawn");
            errors += Require<MatchmakingManager>("MatchmakingManager", "builds the roster from SceneSelection + bots, calls SpawnManager.PrepareMatch");
            errors += Require<GemGrabMode>("GemGrabMode", "Gem Grab game-mode coordinator (death-drop, win timer, sudden death)");
            errors += Require<SimulationClock>("SimulationClock", "drives the tick pipeline; without it nothing in the simulation updates");

            // ---------- Recommended ----------
            int warnings = 0;
            warnings += Recommend<MatchEndRouter>("MatchEndRouter", "transitions to Results scene on match end. Without it, the match ends but you stay on the Match scene.");
            warnings += Recommend<SceneFlow>("SceneFlow", "scene-flow controller. Should normally come from the Loading scene via DontDestroyOnLoad, but if you started the game from the Match scene directly there'll be no SceneFlow Instance and scene transitions silently fail.");

            // ---------- MatchmakingManager configuration ----------
            MatchmakingManager mm = MatchmakingManager.Instance;
            if (mm != null)
            {
                if (SceneSelection.SelectedBrawler == null)
                {
                    // Likely launched the Match scene directly (skipping
                    // BrawlerSelect). Inspector fallback _playerBrawler
                    // must be set or no player will spawn.
                    Debug.LogWarning("[SetupCheck] SceneSelection.SelectedBrawler is null (Match scene launched directly without going through BrawlerSelect). MatchmakingManager will fall back to its inspector _playerBrawler — make sure that's assigned.");
                }
            }

            if (errors > 0)
                Debug.LogError($"[SetupCheck] {errors} required component(s) missing — match will not start. Fix the errors above.");
            else if (warnings > 0)
                Debug.LogWarning($"[SetupCheck] {warnings} recommended component(s) missing — match will work but flow may be incomplete.");
            else if (_logSuccess)
                Debug.Log("[SetupCheck] All required + recommended components present.");
        }

        private static int Require<T>(string label, string why) where T : Component
        {
            T found = FindObjectOfType<T>();
            if (found == null)
            {
                Debug.LogError($"[SetupCheck] MISSING required: {label} — {why}");
                return 1;
            }
            return 0;
        }

        private static int Recommend<T>(string label, string why) where T : Component
        {
            T found = FindObjectOfType<T>();
            if (found == null)
            {
                Debug.LogWarning($"[SetupCheck] Recommended: {label} — {why}");
                return 1;
            }
            return 0;
        }
    }
}
