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
        [SerializeField] private string[] _sceneNames = new[]
        {
            "Loading",
            "MainMenu",
            "BrawlerSelect",
            "GameModeSelect",
            "Match",
            "Results"
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
                Debug.LogError($"[SceneFlow] No mapping for SceneId.{id}");
                return;
            }
            SceneManager.LoadScene(_sceneNames[index]);
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
