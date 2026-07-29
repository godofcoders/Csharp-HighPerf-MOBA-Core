using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MOBA.Core.Infrastructure
{
    /// <summary>
    /// Runtime-only match camera polish. Menu screens stay clean; the Match
    /// scene gets a small global URP volume for subtle bloom and camera-only
    /// motion blur.
    /// </summary>
    public sealed class MatchPostProcessingInstaller : MonoBehaviour
    {
        private const string MatchSceneName = "Match";
        private const string RuntimeVolumeName = "RuntimeMatchPostProcessing";

        private VolumeProfile _runtimeProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstallForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstallForScene(scene);
        }

        private static void TryInstallForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MatchSceneName)
                return;

            if (GameObject.Find(RuntimeVolumeName) != null)
                return;

            GameObject host = new GameObject(RuntimeVolumeName);
            host.AddComponent<MatchPostProcessingInstaller>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            InstallVolume();
            EnableMainCameraPostProcessing();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_runtimeProfile != null)
                Destroy(_runtimeProfile);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || scene.name != MatchSceneName)
            {
                Destroy(gameObject);
                return;
            }

            EnableMainCameraPostProcessing();
        }

        private void InstallVolume()
        {
            Volume volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            volume.weight = 1f;

            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimeProfile.name = "Runtime Match Polish";
            _runtimeProfile.hideFlags = HideFlags.HideAndDontSave;

            Bloom bloom = _runtimeProfile.Add<Bloom>(true);
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.56f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.78f, 1f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);

            MotionBlur motionBlur = _runtimeProfile.Add<MotionBlur>(true);
            motionBlur.mode.Override(MotionBlurMode.CameraOnly);
            motionBlur.quality.Override(MotionBlurQuality.Low);
            motionBlur.intensity.Override(0.12f);
            motionBlur.clamp.Override(0.035f);

            volume.profile = _runtimeProfile;
        }

        private static void EnableMainCameraPostProcessing()
        {
            Camera camera = Camera.main;
            if (camera == null)
                camera = FindObjectOfType<Camera>();

            if (camera == null)
                return;

            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
                cameraData.renderPostProcessing = true;
        }
    }
}
