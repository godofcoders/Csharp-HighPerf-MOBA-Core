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
        [SerializeField, Range(0f, 1f)] private float _rainIntensity = 0.82f;
        [SerializeField, Min(1f)] private float _rainHeight = 11f;
        [SerializeField, Min(0.1f)] private float _rainFallSpeed = 34f;
        [SerializeField, Min(0.1f)] private float _windSpeed = 5.4f;

        [Header("Lightning")]
        [SerializeField, Min(0.5f)] private float _minLightningInterval = 4.5f;
        [SerializeField, Min(0.5f)] private float _maxLightningInterval = 9.5f;
        [SerializeField, Min(0.02f)] private float _lightningFlashSeconds = 0.14f;

        [Header("Wind Dressing")]
        [SerializeField, Min(0)] private int _swayingTreeCount = 7;

        private Transform _stormRoot;
        private Transform _cameraDropletRoot;
        private Transform _lensDropletOverlayRoot;
        private ParticleSystem _rainSystem;
        private ParticleSystem _cameraDropletSystem;
        private ParticleSystem _leafGustSystem;
        private Light _lightningLight;
        private Material _rainMaterial;
        private Material _lensDropletMaterial;
        private Material _wetGroundMaterial;
        private Material _puddleMaterial;
        private Material _cloudShadowMaterial;
        private Material _leafMaterial;
        private Material _treeTrunkMaterial;
        private Material _treeCanopyMaterial;
        private Material _lensOverlayMaterial;
        private Color _previousAmbientLight;
        private Color _previousFogColor;
        private bool _previousFogEnabled;
        private Color _previousCameraColor;
        private bool _hasCapturedLightingState;
        private bool _hasCameraColor;
        private float _nextLightningTime;
        private float _lightningTimer;
        private Transform[] _treeRoots;
        private Quaternion[] _treeBaseRotations;
        private float[] _treePhases;

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
            UpdateSwayingTrees();
        }

        private void OnDestroy()
        {
            RestoreLightingState();
            DestroyCameraDroplets();
            DestroyMaterial(_rainMaterial);
            DestroyMaterial(_lensDropletMaterial);
            DestroyMaterial(_wetGroundMaterial);
            DestroyMaterial(_puddleMaterial);
            DestroyMaterial(_cloudShadowMaterial);
            DestroyMaterial(_leafMaterial);
            DestroyMaterial(_treeTrunkMaterial);
            DestroyMaterial(_treeCanopyMaterial);
            DestroyMaterial(_lensOverlayMaterial);
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
            BuildCameraDroplets();
            BuildGroundWetness(bounds);
            BuildWindAmbience(bounds);
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
            main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(640f, 1450f, _rainIntensity));
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.065f, 0.095f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.98f, 1f, 1f, 0.9f));
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = _rainSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Lerp(220f, 560f, _rainIntensity));

            ParticleSystem.ShapeModule shape = _rainSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x + 8f, 1f, bounds.size.z + 8f);

            ParticleSystem.VelocityOverLifetimeModule velocity = _rainSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-_windSpeed * 0.35f, _windSpeed * 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-_rainFallSpeed * 1.12f, -_rainFallSpeed * 0.92f);
            velocity.z = new ParticleSystem.MinMaxCurve(-_windSpeed, -_windSpeed * 0.45f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _rainSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.78f, 0.9f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.82f, 0.14f),
                    new GradientAlphaKey(0.58f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = _rainSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _rainMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 0.12f;
                renderer.velocityScale = 0.035f;
                renderer.cameraVelocityScale = 0.02f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _rainSystem.Play(true);
        }

        private void BuildCameraDroplets()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            if (_cameraDropletRoot == null)
            {
                Transform existing = camera.transform.Find("StormCameraDroplets");
                if (existing != null)
                {
                    _cameraDropletRoot = existing;
                    _cameraDropletSystem = existing.GetComponent<ParticleSystem>();
                }
            }

            if (_cameraDropletRoot == null)
            {
                GameObject dropletObject = new GameObject("StormCameraDroplets");
                dropletObject.transform.SetParent(camera.transform, false);
                _cameraDropletRoot = dropletObject.transform;
                _cameraDropletSystem = dropletObject.AddComponent<ParticleSystem>();
            }

            if (_cameraDropletSystem == null)
                _cameraDropletSystem = _cameraDropletRoot.gameObject.AddComponent<ParticleSystem>();

            _cameraDropletSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _cameraDropletRoot.localPosition = new Vector3(0f, 0f, Mathf.Max(1.6f, camera.nearClipPlane + 1.2f));
            _cameraDropletRoot.localRotation = Quaternion.identity;
            _cameraDropletRoot.localScale = Vector3.one;

            ParticleSystem.MainModule main = _cameraDropletSystem.main;
            main.loop = true;
            main.duration = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 180;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.35f, 2.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.075f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 1f, 1f, 0.52f));
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = _cameraDropletSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(28f);

            ParticleSystem.ShapeModule shape = _cameraDropletSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            float dropletViewHeight = camera.orthographic ? camera.orthographicSize * 2.0f : 4.4f;
            float dropletViewWidth = dropletViewHeight * Mathf.Max(0.1f, camera.aspect);
            shape.scale = new Vector3(dropletViewWidth, dropletViewHeight, 0.02f);

            ParticleSystem.VelocityOverLifetimeModule velocity = _cameraDropletSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.48f, -0.16f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _cameraDropletSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.72f, 0.88f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.48f, 0.12f),
                    new GradientAlphaKey(0.34f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = _cameraDropletSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _lensDropletMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.05f;
                renderer.velocityScale = 0.78f;
                renderer.cameraVelocityScale = 0f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _cameraDropletSystem.Play(true);
            BuildLensDropletOverlay(camera);
        }

        private void BuildLensDropletOverlay(Camera camera)
        {
            if (camera == null)
                return;

            if (_lensDropletOverlayRoot == null)
            {
                Transform existing = camera.transform.Find("StormLensDropletOverlay");
                if (existing != null)
                    _lensDropletOverlayRoot = existing;
            }

            if (_lensDropletOverlayRoot == null)
            {
                GameObject overlay = new GameObject("StormLensDropletOverlay");
                overlay.transform.SetParent(camera.transform, false);
                _lensDropletOverlayRoot = overlay.transform;
            }

            ClearChildren(_lensDropletOverlayRoot);

            float distance = camera.orthographic
                ? Mathf.Max(0.8f, camera.nearClipPlane + 0.75f)
                : Mathf.Max(2.0f, camera.nearClipPlane + 1.65f);
            float halfHeight = camera.orthographic
                ? Mathf.Max(1f, camera.orthographicSize)
                : Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
            float halfWidth = halfHeight * Mathf.Max(0.1f, camera.aspect);
            _lensDropletOverlayRoot.localPosition = new Vector3(0f, 0f, distance);
            _lensDropletOverlayRoot.localRotation = Quaternion.identity;
            _lensDropletOverlayRoot.localScale = Vector3.one;

            const int dropletCount = 54;
            for (int i = 0; i < dropletCount; i++)
            {
                float tx = Mathf.Repeat(i * 0.618f + 0.13f, 1f);
                float ty = Mathf.Repeat(i * 0.377f + 0.29f, 1f);
                float x = Mathf.Lerp(-halfWidth * 0.82f, halfWidth * 0.82f, tx);
                float y = Mathf.Lerp(-halfHeight * 0.72f, halfHeight * 0.72f, ty);
                float size = camera.orthographic
                    ? Mathf.Lerp(0.18f, 0.36f, Mathf.Repeat(i * 0.271f, 1f))
                    : Mathf.Lerp(0.055f, 0.145f, Mathf.Repeat(i * 0.271f, 1f));

                GameObject droplet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                droplet.name = "LensRainDroplet_" + i;
                droplet.transform.SetParent(_lensDropletOverlayRoot, false);
                droplet.transform.localPosition = new Vector3(x, y, 0f);
                droplet.transform.localRotation = Quaternion.identity;
                droplet.transform.localScale = new Vector3(size * 0.68f, size * 1.45f, size * 0.08f);

                Renderer renderer = droplet.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _lensOverlayMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                Collider collider = droplet.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
            }
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
                "StormWetGround",
                PrimitiveType.Cube,
                new Vector3(bounds.center.x, bounds.max.y + 0.018f, bounds.center.z),
                new Vector3(bounds.size.x + 1.5f, 0.012f, bounds.size.z + 1.5f),
                _wetGroundMaterial);

            CreateFlatPatch(
                wetnessRoot,
                "StormCloudShadow",
                PrimitiveType.Cube,
                new Vector3(bounds.center.x, bounds.max.y + 0.018f, bounds.center.z),
                new Vector3(bounds.size.x + 1.5f, 0.01f, bounds.size.z + 1.5f),
                _cloudShadowMaterial);

            int puddleCount = 14;
            for (int i = 0; i < puddleCount; i++)
            {
                float t = (i + 0.5f) / puddleCount;
                float wave = Mathf.Sin(t * Mathf.PI * 4.8f);
                float x = Mathf.Lerp(bounds.min.x + 2f, bounds.max.x - 2f, t);
                float z = bounds.center.z + wave * bounds.extents.z * 0.48f;
                float width = Mathf.Lerp(1.15f, 2.7f, Mathf.Repeat(t * 2.7f, 1f));
                float depth = Mathf.Lerp(0.42f, 1.05f, Mathf.Repeat(t * 3.1f, 1f));

                CreateFlatPatch(
                    wetnessRoot,
                    "StormPuddle_" + i,
                    PrimitiveType.Cylinder,
                    new Vector3(x, bounds.max.y + 0.032f, z),
                    new Vector3(width, 0.01f, depth),
                    _puddleMaterial);
            }
        }

        private void BuildWindAmbience(Bounds bounds)
        {
            BuildLeafGusts(bounds);
            BuildSwayingTrees(bounds);
        }

        private void BuildLeafGusts(Bounds bounds)
        {
            if (_leafGustSystem == null)
            {
                GameObject leafObject = new GameObject("StormLeafGusts");
                leafObject.transform.SetParent(_stormRoot, false);
                _leafGustSystem = leafObject.AddComponent<ParticleSystem>();
            }

            _leafGustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _leafGustSystem.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.9f, bounds.center.z);
            _leafGustSystem.transform.rotation = Quaternion.identity;

            ParticleSystem.MainModule main = _leafGustSystem.main;
            main.loop = true;
            main.duration = 6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 520;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.64f, 0.52f, 0.23f, 0.62f),
                new Color(0.88f, 0.72f, 0.28f, 0.78f));

            ParticleSystem.EmissionModule emission = _leafGustSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(62f);

            ParticleSystem.ShapeModule shape = _leafGustSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x + 4f, 0.45f, bounds.size.z + 3f);

            ParticleSystem.VelocityOverLifetimeModule velocity = _leafGustSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(_windSpeed * 0.7f, _windSpeed * 1.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.65f);
            velocity.z = new ParticleSystem.MinMaxCurve(-_windSpeed * 0.45f, _windSpeed * 0.28f);

            ParticleSystem.RotationOverLifetimeModule rotation = _leafGustSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            ParticleSystemRenderer renderer = _leafGustSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _leafMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _leafGustSystem.Play(true);
        }

        private void BuildSwayingTrees(Bounds bounds)
        {
            Transform treeRoot = _stormRoot.Find("StormSwayingTrees");
            if (treeRoot == null)
            {
                GameObject root = new GameObject("StormSwayingTrees");
                root.transform.SetParent(_stormRoot, false);
                treeRoot = root.transform;
            }

            ClearChildren(treeRoot);

            int count = Mathf.Clamp(_swayingTreeCount, 0, 12);
            _treeRoots = new Transform[count];
            _treeBaseRotations = new Quaternion[count];
            _treePhases = new float[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 position = ResolveTreePosition(bounds, i, count);
                GameObject tree = new GameObject("StormSwayingTree_" + i);
                tree.transform.SetParent(treeRoot, true);
                tree.transform.position = position;
                tree.transform.rotation = Quaternion.identity;
                float size = ResolveTreeSize(i);
                float heightStretch = ResolveTreeHeightStretch(i);
                tree.transform.localScale = new Vector3(size, size * heightStretch, size);

                CreateTreePart(
                    tree.transform,
                    "TreeTrunk",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 1.0f, 0f),
                    new Vector3(0.24f, 1.0f, 0.24f),
                    _treeTrunkMaterial);

                CreateTreePart(
                    tree.transform,
                    "TreeCanopy",
                    PrimitiveType.Sphere,
                    new Vector3(0.12f, 2.12f, -0.06f),
                    new Vector3(1.05f, 0.86f, 1.05f),
                    _treeCanopyMaterial);

                CreateTreePart(
                    tree.transform,
                    "TreeCanopy_Offset",
                    PrimitiveType.Sphere,
                    new Vector3(-0.36f, 1.82f, 0.26f),
                    new Vector3(0.74f, 0.62f, 0.74f),
                    _treeCanopyMaterial);

                _treeRoots[i] = tree.transform;
                _treeBaseRotations[i] = tree.transform.rotation;
                _treePhases[i] = i * 0.83f;
            }
        }

        private void CreateTreePart(
            Transform parent,
            string objectName,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = Quaternion.identity;
            primitive.transform.localScale = localScale;

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private static Vector3 ResolveTreePosition(Bounds bounds, int index, int count)
        {
            float t = (index + 0.5f) / Mathf.Max(1, count);
            bool leftOrRight = index % 2 == 0;
            float x = leftOrRight
                ? (index % 4 == 0 ? bounds.min.x + 1.15f : bounds.max.x - 1.15f)
                : Mathf.Lerp(bounds.min.x + 3f, bounds.max.x - 3f, Mathf.Repeat(t * 1.9f, 1f));
            float z = leftOrRight
                ? Mathf.Lerp(bounds.min.z + 2.3f, bounds.max.z - 2.3f, Mathf.Repeat(t * 1.37f, 1f))
                : (index % 4 == 1 ? bounds.min.z + 1.15f : bounds.max.z - 1.15f);

            return new Vector3(x, bounds.max.y, z);
        }

        private static float ResolveTreeSize(int index)
        {
            return Mathf.Lerp(2.0f, 3.1f, Mathf.Repeat(index * 0.47f + 0.18f, 1f));
        }

        private static float ResolveTreeHeightStretch(int index)
        {
            return Mathf.Lerp(0.86f, 1.32f, Mathf.Repeat(index * 0.31f + 0.27f, 1f));
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

        private void UpdateSwayingTrees()
        {
            if (_treeRoots == null || _treeBaseRotations == null || _treePhases == null)
                return;

            float time = Time.time;
            int count = Mathf.Min(_treeRoots.Length, Mathf.Min(_treeBaseRotations.Length, _treePhases.Length));
            for (int i = 0; i < count; i++)
            {
                Transform tree = _treeRoots[i];
                if (tree == null)
                    continue;

                float wave = Mathf.Sin(time * 1.65f + _treePhases[i]);
                float gust = Mathf.Sin(time * 0.52f + _treePhases[i] * 0.7f) * 0.45f;
                tree.rotation = _treeBaseRotations[i] * Quaternion.Euler(
                    (wave + gust) * 3.8f,
                    0f,
                    Mathf.Sin(time * 1.1f + _treePhases[i]) * 2.2f);
            }
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
            _rainMaterial = EnsureMaterial(_rainMaterial, "Runtime_StormRain", new Color(0.92f, 0.96f, 1f, 0.78f), true);
            _lensDropletMaterial = EnsureMaterial(_lensDropletMaterial, "Runtime_StormLensDrops", new Color(0.94f, 1f, 1f, 0.52f), true);
            _wetGroundMaterial = EnsureMaterial(_wetGroundMaterial, "Runtime_StormWetGround", new Color(0.05f, 0.09f, 0.12f, 0.32f), true);
            _puddleMaterial = EnsureMaterial(_puddleMaterial, "Runtime_StormPuddles", new Color(0.13f, 0.25f, 0.31f, 0.50f), true);
            _cloudShadowMaterial = EnsureMaterial(_cloudShadowMaterial, "Runtime_StormCloudShadow", new Color(0.02f, 0.03f, 0.05f, 0.18f), true);
            _leafMaterial = EnsureMaterial(_leafMaterial, "Runtime_StormLeaves", new Color(0.77f, 0.62f, 0.24f, 0.72f), true);
            _treeTrunkMaterial = EnsureMaterial(_treeTrunkMaterial, "Runtime_StormTreeTrunk", new Color(0.34f, 0.22f, 0.11f, 1f), false);
            _treeCanopyMaterial = EnsureMaterial(_treeCanopyMaterial, "Runtime_StormTreeCanopy", new Color(0.54f, 0.49f, 0.25f, 1f), false);
            _lensOverlayMaterial = EnsureMaterial(_lensOverlayMaterial, "Runtime_StormLensOverlayDrops", new Color(0.94f, 1f, 1f, 0.62f), true);
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

        private GameObject CreateFlatPatch(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
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

        private void DestroyCameraDroplets()
        {
            if (_cameraDropletRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(_cameraDropletRoot.gameObject);
                else
                    DestroyImmediate(_cameraDropletRoot.gameObject);

                _cameraDropletRoot = null;
                _cameraDropletSystem = null;
            }

            if (_lensDropletOverlayRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(_lensDropletOverlayRoot.gameObject);
                else
                    DestroyImmediate(_lensDropletOverlayRoot.gameObject);

                _lensDropletOverlayRoot = null;
            }
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
