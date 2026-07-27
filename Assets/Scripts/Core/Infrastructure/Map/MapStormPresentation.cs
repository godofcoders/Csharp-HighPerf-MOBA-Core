using UnityEngine;
using UnityEngine.Rendering;

namespace MOBA.Core.Infrastructure
{
    public sealed class MapStormPresentation : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private const string StormRootName = "RuntimeStormPresentation";

        [Header("Rain")]
        [SerializeField, Range(0f, 1f)] private float _rainIntensity = 0.74f;
        [SerializeField, Min(1f)] private float _rainHeight = 11f;
        [SerializeField, Min(0.1f)] private float _rainFallSpeed = 17f;
        [SerializeField, Min(0.1f)] private float _windSpeed = 3.2f;

        [Header("Lightning")]
        [SerializeField, Min(0.5f)] private float _minLightningInterval = 4.5f;
        [SerializeField, Min(0.5f)] private float _maxLightningInterval = 9.5f;
        [SerializeField, Min(0.02f)] private float _lightningFlashSeconds = 0.14f;

        private Transform _stormRoot;
        private ParticleSystem _rainSystem;
        private Light _lightningLight;
        private Material _rainMaterial;
        private Material _puddleMaterial;
        private Material _cloudShadowMaterial;
        private Color _previousAmbientLight;
        private Color _previousFogColor;
        private bool _previousFogEnabled;
        private Color _previousCameraColor;
        private bool _hasCapturedLightingState;
        private bool _hasCameraColor;
        private float _nextLightningTime;
        private float _lightningTimer;

        public static void InstallUnder(GameObject root)
        {
            if (root == null || SceneSelection.SelectedMode != GameModeId.GemGrab)
                return;

            MapStormPresentation presentation = root.GetComponent<MapStormPresentation>();
            bool created = false;
            if (presentation == null)
            {
                presentation = root.AddComponent<MapStormPresentation>();
                created = true;
            }

            if (!created)
                presentation.RefreshStorm();
        }

        private void Awake()
        {
            if (SceneSelection.SelectedMode != GameModeId.GemGrab)
            {
                enabled = false;
                return;
            }

            CaptureLightingState();
            ApplyOvercastLighting();
            RefreshStorm();
            ScheduleNextLightning();
        }

        private void Update()
        {
            UpdateLightning();
        }

        private void OnDestroy()
        {
            RestoreLightingState();
            DestroyMaterial(_rainMaterial);
            DestroyMaterial(_puddleMaterial);
            DestroyMaterial(_cloudShadowMaterial);
        }

        public void RefreshStorm()
        {
            if (SceneSelection.SelectedMode != GameModeId.GemGrab)
                return;

            if (!TryResolveGroundBounds(out Bounds bounds))
                return;

            EnsureStormRoot();
            EnsureMaterials();
            BuildRain(bounds);
            BuildGroundWetness(bounds);
            EnsureLightningLight(bounds);
        }

        private void BuildRain(Bounds bounds)
        {
            if (_rainSystem == null)
            {
                GameObject rainObject = new GameObject("StormRain");
                rainObject.transform.SetParent(_stormRoot, false);
                _rainSystem = rainObject.AddComponent<ParticleSystem>();
            }

            _rainSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Transform rainTransform = _rainSystem.transform;
            rainTransform.position = new Vector3(bounds.center.x, bounds.max.y + _rainHeight, bounds.center.z);
            rainTransform.rotation = Quaternion.identity;
            rainTransform.localScale = Vector3.one;

            ParticleSystem.MainModule main = _rainSystem.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(280f, 920f, _rainIntensity));
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.62f, 0.92f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.64f, 0.79f, 0.92f, 0.55f));
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = _rainSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Lerp(100f, 330f, _rainIntensity));

            ParticleSystem.ShapeModule shape = _rainSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x + 8f, 1f, bounds.size.z + 8f);

            ParticleSystem.VelocityOverLifetimeModule velocity = _rainSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-_windSpeed * 0.35f, _windSpeed * 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-_rainFallSpeed * 1.08f, -_rainFallSpeed * 0.86f);
            velocity.z = new ParticleSystem.MinMaxCurve(-_windSpeed, -_windSpeed * 0.45f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _rainSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.73f, 0.86f, 1f), 0f),
                    new GradientColorKey(new Color(0.47f, 0.64f, 0.78f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.58f, 0.18f),
                    new GradientAlphaKey(0.42f, 0.78f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = _rainSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _rainMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.3f;
                renderer.velocityScale = 0.72f;
                renderer.cameraVelocityScale = 0.02f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _rainSystem.Play(true);
        }

        private void BuildGroundWetness(Bounds bounds)
        {
            Transform wetnessRoot = _stormRoot.Find("StormGroundWetness");
            if (wetnessRoot == null)
            {
                GameObject wetness = new GameObject("StormGroundWetness");
                wetness.transform.SetParent(_stormRoot, false);
                wetnessRoot = wetness.transform;
            }

            ClearChildren(wetnessRoot);

            CreateFlatPatch(
                wetnessRoot,
                "StormCloudShadow",
                new Vector3(bounds.center.x, bounds.max.y + 0.018f, bounds.center.z),
                new Vector3(bounds.size.x + 1.5f, 0.01f, bounds.size.z + 1.5f),
                _cloudShadowMaterial);

            int puddleCount = 9;
            for (int i = 0; i < puddleCount; i++)
            {
                float t = (i + 0.5f) / puddleCount;
                float wave = Mathf.Sin(t * Mathf.PI * 4.8f);
                float x = Mathf.Lerp(bounds.min.x + 2f, bounds.max.x - 2f, t);
                float z = bounds.center.z + wave * bounds.extents.z * 0.48f;
                float width = Mathf.Lerp(1.0f, 2.2f, Mathf.Repeat(t * 2.7f, 1f));
                float depth = Mathf.Lerp(0.34f, 0.78f, Mathf.Repeat(t * 3.1f, 1f));

                CreateFlatPatch(
                    wetnessRoot,
                    "StormPuddle_" + i,
                    new Vector3(x, bounds.max.y + 0.026f, z),
                    new Vector3(width, 0.01f, depth),
                    _puddleMaterial);
            }
        }

        private void EnsureLightningLight(Bounds bounds)
        {
            if (_lightningLight != null)
                return;

            GameObject lightObject = new GameObject("StormLightningLight");
            lightObject.transform.SetParent(_stormRoot, false);
            lightObject.transform.position = new Vector3(bounds.center.x, bounds.max.y + 12f, bounds.center.z - bounds.extents.z * 0.25f);
            lightObject.transform.rotation = Quaternion.Euler(58f, -28f, 0f);

            _lightningLight = lightObject.AddComponent<Light>();
            _lightningLight.type = LightType.Directional;
            _lightningLight.color = new Color(0.66f, 0.82f, 1f, 1f);
            _lightningLight.intensity = 0f;
            _lightningLight.shadows = LightShadows.None;
        }

        private void UpdateLightning()
        {
            if (_lightningLight == null)
                return;

            if (_lightningTimer > 0f)
            {
                _lightningTimer -= Time.deltaTime;
                float normalized = Mathf.Clamp01(_lightningTimer / _lightningFlashSeconds);
                float pulse = Mathf.Sin(normalized * Mathf.PI);
                _lightningLight.intensity = Mathf.Lerp(0f, 2.8f, pulse);
                return;
            }

            _lightningLight.intensity = 0f;
            if (Time.time < _nextLightningTime)
                return;

            _lightningTimer = _lightningFlashSeconds;
            ScheduleNextLightning();
        }

        private void ScheduleNextLightning()
        {
            float min = Mathf.Max(0.5f, _minLightningInterval);
            float max = Mathf.Max(min, _maxLightningInterval);
            _nextLightningTime = Time.time + Random.Range(min, max);
        }

        private void CaptureLightingState()
        {
            _previousAmbientLight = RenderSettings.ambientLight;
            _previousFogColor = RenderSettings.fogColor;
            _previousFogEnabled = RenderSettings.fog;

            Camera camera = Camera.main;
            if (camera != null)
            {
                _previousCameraColor = camera.backgroundColor;
                _hasCameraColor = true;
            }

            _hasCapturedLightingState = true;
        }

        private void ApplyOvercastLighting()
        {
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.48f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.23f, 0.27f, 0.32f, 1f);

            Camera camera = Camera.main;
            if (camera != null)
                camera.backgroundColor = new Color(0.14f, 0.17f, 0.22f, 1f);
        }

        private void RestoreLightingState()
        {
            if (!_hasCapturedLightingState)
                return;

            RenderSettings.ambientLight = _previousAmbientLight;
            RenderSettings.fogColor = _previousFogColor;
            RenderSettings.fog = _previousFogEnabled;

            Camera camera = Camera.main;
            if (camera != null && _hasCameraColor)
                camera.backgroundColor = _previousCameraColor;
        }

        private void EnsureStormRoot()
        {
            if (_stormRoot != null)
                return;

            Transform existing = transform.Find(StormRootName);
            if (existing != null)
            {
                _stormRoot = existing;
                return;
            }

            GameObject root = new GameObject(StormRootName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            _stormRoot = root.transform;
        }

        private void EnsureMaterials()
        {
            _rainMaterial = EnsureMaterial(_rainMaterial, "Runtime_StormRain", new Color(0.64f, 0.79f, 0.92f, 0.58f), true);
            _puddleMaterial = EnsureMaterial(_puddleMaterial, "Runtime_StormPuddles", new Color(0.16f, 0.23f, 0.28f, 0.36f), true);
            _cloudShadowMaterial = EnsureMaterial(_cloudShadowMaterial, "Runtime_StormCloudShadow", new Color(0.03f, 0.05f, 0.08f, 0.28f), true);
        }

        private static Material EnsureMaterial(Material current, string materialName, Color color, bool transparent)
        {
            if (current != null)
                return current;

            Shader shader = transparent
                ? ResolveTransparentShader()
                : ResolveOpaqueShader();
            if (shader == null)
                return null;

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);

            if (transparent)
                ConfigureTransparentMaterial(material);

            return material;
        }

        private static Shader ResolveOpaqueShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit") ??
                   Shader.Find("Unlit/Color") ??
                   Shader.Find("Sprites/Default") ??
                   Shader.Find("Standard");
        }

        private static Shader ResolveTransparentShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                return Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                       Shader.Find("Universal Render Pipeline/Unlit") ??
                       Shader.Find("Sprites/Default") ??
                       Shader.Find("Standard");
            }

            return Shader.Find("Particles/Standard Unlit") ??
                   Shader.Find("Sprites/Default") ??
                   Shader.Find("Legacy Shaders/Transparent/Diffuse") ??
                   Shader.Find("Unlit/Transparent") ??
                   Shader.Find("Standard");
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetMaterialFloatIfPresent(material, SurfaceId, 1f);
            SetMaterialFloatIfPresent(material, ModeId, 3f);
            SetMaterialFloatIfPresent(material, BlendId, 0f);
            SetMaterialFloatIfPresent(material, AlphaClipId, 0f);
            SetMaterialFloatIfPresent(material, CullId, (float)CullMode.Off);
            SetMaterialIntIfPresent(material, SrcBlendId, (int)BlendMode.SrcAlpha);
            SetMaterialIntIfPresent(material, DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            SetMaterialIntIfPresent(material, ZWriteId, 0);
            SetMaterialIntIfPresent(material, CullId, (int)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private GameObject CreateFlatPatch(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, true);
            primitive.transform.position = position;
            primitive.transform.rotation = Quaternion.identity;
            primitive.transform.localScale = scale;

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            return primitive;
        }

        private void ClearChildren(Transform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private bool TryResolveGroundBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            int excludedMask = ResolveObstacleMask() | ResolveLayerMask("Bushes") | ResolveLayerMask("Bush");

            Collider[] colliders = GetComponentsInChildren<Collider>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.isTrigger ||
                    !IsGroundCandidate(collider.gameObject, excludedMask))
                {
                    continue;
                }

                Encapsulate(collider.bounds, ref bounds, ref found);
            }

            if (found)
                return true;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !IsGroundCandidate(renderer.gameObject, excludedMask))
                {
                    continue;
                }

                Encapsulate(renderer.bounds, ref bounds, ref found);
            }

            return found;
        }

        private bool IsGroundCandidate(GameObject candidate, int excludedMask)
        {
            if (candidate == null)
                return false;

            if (_stormRoot != null && candidate.transform.IsChildOf(_stormRoot))
                return false;

            string objectName = candidate.name;
            if (objectName.IndexOf(StormRootName, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("RuntimeArenaBoundary", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("ArenaWall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Poison", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            int layerMask = 1 << candidate.layer;
            return (excludedMask & layerMask) == 0;
        }

        private static void Encapsulate(Bounds candidate, ref Bounds bounds, ref bool found)
        {
            if (!found)
            {
                bounds = candidate;
                found = true;
                return;
            }

            bounds.Encapsulate(candidate);
        }

        private static void SetMaterialFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetFloat(propertyId, value);
        }

        private static void SetMaterialIntIfPresent(Material material, int propertyId, int value)
        {
            if (material != null && material.HasProperty(propertyId))
                material.SetInt(propertyId, value);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        private static int ResolveObstacleMask()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            return obstacleLayer >= 0 ? 1 << obstacleLayer : 0;
        }

        private static int ResolveLayerMask(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
