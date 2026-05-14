using UnityEngine;
using UnityEngine.SceneManagement;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Singleton scene-flow controller. Survives scene loads via
    /// DontDestroyOnLoad. Maps logical <see cref="SceneId"/> to scene asset
    /// names + provides the <see cref="LoadScene(SceneId)"/> entry point
    /// that screen controllers call.
    ///
    /// The first scene loaded should boot SceneFlow (drop a SceneFlow
    /// GameObject into the Loading scene). Subsequent scenes find it via
    /// the static Instance.
    ///
    /// Designer renames a scene file? Edit only the array below; gameplay
    /// code stays unchanged.
    /// </summary>
    public class SceneFlow : MonoBehaviour
    {
        public static SceneFlow Instance { get; private set; }

        [Header("Scene name mapping (parallel to SceneId enum)")]
        [Tooltip("Index = SceneId enum value. Each entry is the scene asset name (without .unity) added to Build Settings.")]
        [SerializeField]
        private readonly string[] _sceneNames =
{

    "Loading",

    "MainMenu",

    "BrawlerSelect",

    "GameModeSelect",

    "Match",

    "Results",

    "MapSelect"

};


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(SceneId id)
        {
            int index = (int)id;
            if (index < 0 || index >= _sceneNames.Length)
            {
                Debug.LogError($"[SceneFlow] No mapping for SceneId.{index}");
                return;
            }

            // Prefer the fade-transition path if SceneTransition is in the
            // scene (production flow). Falls back to a hard cut if it
            // isn't (e.g. EditMode tests, scenes loaded directly without
            // going through Loading).
            if (SceneTransition.Instance != null)
                StartCoroutine(SceneTransition.Instance.TransitionTo(_sceneNames[index]));
            else
                SceneManager.LoadScene(_sceneNames[index]);
        }

        /// <summary>
        /// Async scene load. Returns the AsyncOperation so the caller (e.g.
        /// LoadingScreen) can poll <see cref="AsyncOperation.progress"/> for
        /// a real progress bar.
        ///
        /// When <paramref name="allowActivation"/> is false, the new scene
        /// loads but doesn't swap in until you set
        /// <c>op.allowSceneActivation = true</c>. This lets you finish a
        /// progress-bar animation before the scene visibly changes — Unity
        /// caps <c>op.progress</c> at 0.9 until activation is allowed, so
        /// scale your bar by <c>op.progress / 0.9f</c>.
        /// </summary>
        public AsyncOperation LoadSceneAsync(SceneId id, bool allowActivation = true)
        {
            int index = (int)id;
            if (index < 0 || index >= _sceneNames.Length)
            {
                Debug.LogError($"[SceneFlow] No mapping for SceneId.{id}");
                return null;
            }
            AsyncOperation op = SceneManager.LoadSceneAsync(_sceneNames[index]);
            if (op != null) op.allowSceneActivation = allowActivation;
            return op;
        }

        /// <summary>Convenience: reset selection state and return to the
        /// main menu. Call from end-of-match flows.</summary>
        public void ReturnToMainMenu()
        {
            SceneSelection.Reset();
            LoadScene(SceneId.MainMenu);
        }
    }
}
