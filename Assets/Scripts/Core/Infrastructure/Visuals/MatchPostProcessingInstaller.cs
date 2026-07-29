using System;
using System.Reflection;
using UnityEngine;
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

        private ScriptableObject _runtimeProfile;

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
            Type volumeType = FindType("UnityEngine.Rendering.Volume");
            Type profileType = FindType("UnityEngine.Rendering.VolumeProfile");
            if (volumeType == null || profileType == null)
                return;

            if (!typeof(Component).IsAssignableFrom(volumeType) || !typeof(ScriptableObject).IsAssignableFrom(profileType))
                return;

            Component volume = gameObject.AddComponent(volumeType);
            SetMember(volume, "isGlobal", true);
            SetMember(volume, "priority", 20f);
            SetMember(volume, "weight", 1f);

            _runtimeProfile = ScriptableObject.CreateInstance(profileType);
            _runtimeProfile.name = "Runtime Match Polish";
            _runtimeProfile.hideFlags = HideFlags.HideAndDontSave;

            AddBloom(_runtimeProfile, profileType);
            AddMotionBlur(_runtimeProfile, profileType);

            SetMember(volume, "profile", _runtimeProfile);
            SetMember(volume, "sharedProfile", _runtimeProfile);
        }

        private static void AddBloom(ScriptableObject profile, Type profileType)
        {
            Type bloomType = FindType("UnityEngine.Rendering.Universal.Bloom");
            object bloom = AddVolumeComponent(profile, profileType, bloomType);
            if (bloom == null)
                return;

            SetVolumeParameter(bloom, "threshold", 0.82f);
            SetVolumeParameter(bloom, "intensity", 0.18f);
            SetVolumeParameter(bloom, "scatter", 0.56f);
            SetVolumeParameter(bloom, "tint", new Color(1f, 0.92f, 0.78f, 1f));
            SetVolumeParameter(bloom, "highQualityFiltering", false);
            SetEnumVolumeParameter(bloom, "downscale", "UnityEngine.Rendering.Universal.BloomDownscaleMode", "Quarter");
            SetVolumeParameter(bloom, "maxIterations", 4);
        }

        private static void AddMotionBlur(ScriptableObject profile, Type profileType)
        {
            Type motionBlurType = FindType("UnityEngine.Rendering.Universal.MotionBlur");
            object motionBlur = AddVolumeComponent(profile, profileType, motionBlurType);
            if (motionBlur == null)
                return;

            SetEnumVolumeParameter(motionBlur, "mode", "UnityEngine.Rendering.Universal.MotionBlurMode", "CameraOnly");
            SetEnumVolumeParameter(motionBlur, "quality", "UnityEngine.Rendering.Universal.MotionBlurQuality", "Low");
            SetVolumeParameter(motionBlur, "intensity", 0.12f);
            SetVolumeParameter(motionBlur, "clamp", 0.035f);
        }

        private static object AddVolumeComponent(ScriptableObject profile, Type profileType, Type componentType)
        {
            if (profile == null || profileType == null || componentType == null)
                return null;

            Type volumeComponentType = FindType("UnityEngine.Rendering.VolumeComponent");
            if (volumeComponentType != null && !volumeComponentType.IsAssignableFrom(componentType))
                return null;

            MethodInfo addMethod = profileType.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Type), typeof(bool) },
                null);

            return addMethod != null ? addMethod.Invoke(profile, new object[] { componentType, true }) : null;
        }

        private static void EnableMainCameraPostProcessing()
        {
            Camera camera = Camera.main;
            if (camera == null)
                camera = FindObjectOfType<Camera>();

            if (camera == null)
                return;

            Type cameraDataType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (cameraDataType == null || !typeof(Component).IsAssignableFrom(cameraDataType))
                return;

            Component cameraData = camera.GetComponent(cameraDataType);
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent(cameraDataType);

            if (cameraData != null)
                SetMember(cameraData, "renderPostProcessing", true);
        }

        private static void SetEnumVolumeParameter(
            object component,
            string fieldName,
            string enumTypeName,
            string enumValueName)
        {
            Type enumType = FindType(enumTypeName);
            if (enumType == null || !enumType.IsEnum)
                return;

            object enumValue = Enum.Parse(enumType, enumValueName);
            SetVolumeParameter(component, fieldName, enumValue);
        }

        private static void SetVolumeParameter(object component, string fieldName, object value)
        {
            if (component == null)
                return;

            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object parameter = field != null ? field.GetValue(component) : null;
            if (parameter == null)
                return;

            SetMember(parameter, "value", value);
        }

        private static void SetMember(object target, string memberName, object value)
        {
            if (target == null)
                return;

            Type targetType = target.GetType();

            PropertyInfo property = targetType.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, CoerceValue(value, property.PropertyType));
                return;
            }

            FieldInfo field = targetType.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(target, CoerceValue(value, field.FieldType));
        }

        private static object CoerceValue(object value, Type targetType)
        {
            if (value == null || targetType == null)
                return value;

            Type valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
                return value;

            if (targetType.IsEnum && value is string enumName)
                return Enum.Parse(targetType, enumName);

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
                return Convert.ChangeType(value, targetType);

            return value;
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return null;

            Type direct = Type.GetType(fullName);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
