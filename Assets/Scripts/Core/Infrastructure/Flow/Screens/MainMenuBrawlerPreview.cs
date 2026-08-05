using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public class MainMenuBrawlerPreview : MonoBehaviour, IDragHandler, IPointerClickHandler
    {
        private const string RuntimePreviewSurfaceName = "RuntimeBrawlerPreviewRender";
        private const string RuntimePreviewStageName = "RuntimeHomeBrawlerPreviewStage";
        private const int PreviewTextureWidth = 768;
        private const int PreviewTextureHeight = 1024;

        [SerializeField] private Transform _modelAnchor;
        [SerializeField] private BrawlerDefinition _fallbackBrawler;
        [Tooltip("Roster used to resolve the saved brawler when the menu opens before SceneSelection has been populated.")]
        [SerializeField] private BrawlerDefinition[] _availableBrawlers;

        [Header("Rotation")]
        [Min(0.01f)]
        [SerializeField] private float _rotateSensitivity = 0.4f;

        [Tooltip("How quickly inertial rotation slows down.")]
        [SerializeField] private float _rotationDamping = 6f;

        [Tooltip("Maximum spin speed.")]
        [SerializeField] private float _maxRotationVelocity = 500f;

        [Tooltip("Initial Y rotation of the spawned model.")]
        [SerializeField] private float _initialYaw = 180f;

        [Header("Preview Placement")]
        [Tooltip("Local Y level where the bottom of the preview model should sit. The home stage was authored around the old cube center, so procedural characters need a small downward grounding offset.")]
        [SerializeField] private float _previewGroundLocalY = -1.10f;

        [Tooltip("Renders the 3D brawler into a UI texture so the opaque home lobby artwork cannot cover it.")]
        [SerializeField] private bool _useRenderTexturePreview = true;

        [Tooltip("Drag distance threshold (in screen pixels) above which a pointer-up is treated as a drag instead of a click.")]
        [Min(0f)]
        [SerializeField] private float _clickDragThreshold = 8f;

        [Header("Info display")]
        [SerializeField] private TMP_Text _nameTextTmp;
        [SerializeField] private Text _nameTextLegacy;
        [SerializeField] private TMP_Text _levelTextTmp;
        [SerializeField] private Text _levelTextLegacy;

        [Tooltip("Format string for the power-level display. {0} = level number.")]
        [SerializeField] private string _levelFormat = "Power Level {0}";

        private GameObject _spawned;
        private BrawlerDefinition _currentDef;

        private float _accumulatedDragMagnitude;

        // Inertial rotation state
        private float _rotationVelocity;
        private float _currentYaw;
        private Vector3 _baseLocalPosition;

        private RawImage _previewSurface;
        private RenderTexture _previewTexture;
        private GameObject _previewStageRoot;
        private Transform _previewStageAnchor;
        private Camera _previewCamera;

        private void Start() => Refresh();

        public bool IsPreviewVisible
        {
            get
            {
                if (_previewSurface != null)
                    return _previewSurface.gameObject.activeSelf;

                return _modelAnchor == null || _modelAnchor.gameObject.activeSelf;
            }
        }

        public void SetPreviewVisible(bool visible)
        {
            if (_previewSurface != null)
                _previewSurface.gameObject.SetActive(visible);

            if (_previewStageRoot != null)
                _previewStageRoot.SetActive(visible);

            if (!_useRenderTexturePreview && _modelAnchor != null)
                _modelAnchor.gameObject.SetActive(visible);

            if (visible && _spawned == null)
                Refresh();
        }

        public void ConfigureSelection(
            BrawlerDefinition selected,
            BrawlerDefinition[] availableBrawlers,
            BrawlerDefinition fallbackBrawler)
        {
            if (availableBrawlers != null && availableBrawlers.Length > 0)
                _availableBrawlers = availableBrawlers;

            if (fallbackBrawler != null)
                _fallbackBrawler = fallbackBrawler;

            if (selected != null)
                SceneSelection.SelectedBrawler = selected;

            Refresh();
        }

        private void Update()
        {
            if (_spawned == null)
                return;

            float deltaTime = Time.unscaledDeltaTime;

            // Apply inertial rotation
            if (Mathf.Abs(_rotationVelocity) > 0.01f)
            {
                _currentYaw += _rotationVelocity * deltaTime;

                // Smooth damping
                _rotationVelocity = Mathf.Lerp(
                    _rotationVelocity,
                    0f,
                    _rotationDamping * deltaTime);
            }
            else
            {
                _rotationVelocity = 0f;
            }

            ApplyPreviewPose();
        }

        public void Refresh()
        {
            BrawlerDefinition def = ResolvePreviewBrawler();

            if (def == _currentDef && _spawned != null)
                return;

            if (_spawned != null)
                Destroy(_spawned);

            _currentDef = def;

            if (def == null)
                return;

            if (_modelAnchor == null)
            {
                Debug.LogWarning("[MMBP] _modelAnchor null");
                return;
            }

            Transform spawnParent = ResolveSpawnParent();
            if (spawnParent == null)
                return;

            if (!BrawlerVisualModelFactory.TryCreate(def, spawnParent, null, out _spawned))
            {
                Debug.LogWarning("[MMBP] no visual model available on " + def.name);
                return;
            }

            _baseLocalPosition = Vector3.zero;
            _currentYaw = _initialYaw;
            ApplyPreviewPose();

            _baseLocalPosition = CalculateGroundedLocalPosition(_spawned, spawnParent);
            ApplyPreviewPose();

            StripGameplayComponents(_spawned);
            UpdateInfoText(def);

            _rotationVelocity = 0f;
        }

        private BrawlerDefinition ResolvePreviewBrawler()
        {
            if (SceneSelection.SelectedBrawler != null)
                return SceneSelection.SelectedBrawler;

            if (PlayerBrawlerProgress.TryGetSelectedBrawler(
                    _availableBrawlers,
                    out BrawlerDefinition saved))
            {
                SceneSelection.SelectedBrawler = saved;
                SceneSelection.SelectedBuildPowerLevel = PlayerBrawlerProgress.GetLevel(saved);
                return saved;
            }

            return _fallbackBrawler;
        }

        private void StripGameplayComponents(GameObject root)
        {
            MonoBehaviour[] scripts =
                root.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < scripts.Length; i++)
            {
                MonoBehaviour s = scripts[i];

                if (s == null)
                    continue;

                if (s is Animator)
                    continue;

                if (s is BrawlerAnimationRuntime)
                    continue;

                if (s is BrawlerProceduralModelAnimator)
                    continue;

                if (s is BrawlerPresentationAnchors)
                    continue;

                Destroy(s);
            }

            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);

            Rigidbody[] bodies =
                root.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < bodies.Length; i++)
                Destroy(bodies[i]);
        }

        private void UpdateInfoText(BrawlerDefinition def)
        {
            string nm =
                !string.IsNullOrWhiteSpace(def.BrawlerName)
                    ? def.BrawlerName
                    : def.name;

            if (_nameTextTmp != null)
                _nameTextTmp.text = nm;
            else if (_nameTextLegacy != null)
                _nameTextLegacy.text = nm;

            int level = PlayerBrawlerProgress.GetLevel(def);

            string levelStr = string.Format(_levelFormat, level);

            if (_levelTextTmp != null)
                _levelTextTmp.text = levelStr;
            else if (_levelTextLegacy != null)
                _levelTextLegacy.text = levelStr;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_spawned == null)
                return;

            float deltaYaw =
                -eventData.delta.x * _rotateSensitivity;

            _currentYaw += deltaYaw;
            ApplyPreviewPose();

            // Feed inertia velocity
            _rotationVelocity =
                Mathf.Clamp(
                    deltaYaw / Mathf.Max(Time.unscaledDeltaTime, 0.0001f),
                    -_maxRotationVelocity,
                    _maxRotationVelocity);

            _accumulatedDragMagnitude +=
                eventData.delta.magnitude;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_accumulatedDragMagnitude > _clickDragThreshold)
            {
                _accumulatedDragMagnitude = 0f;
                return;
            }

            _accumulatedDragMagnitude = 0f;

            SceneSelection.PickerReturnsToMainMenu = true;
            SceneFlow.Instance?.LoadScene(SceneId.BrawlerSelect);
        }

        private void ApplyPreviewPose()
        {
            if (_spawned == null)
                return;

            _spawned.transform.localPosition = _baseLocalPosition;
            _spawned.transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
        }

        private Transform ResolveSpawnParent()
        {
            if (!_useRenderTexturePreview)
                return _modelAnchor;

            EnsurePreviewRenderPipeline();

            if (_previewStageAnchor == null)
                return _modelAnchor;

            if (_modelAnchor != null)
                _modelAnchor.gameObject.SetActive(false);

            return _previewStageAnchor;
        }

        private void EnsurePreviewRenderPipeline()
        {
            if (_previewSurface == null)
                _previewSurface = EnsurePreviewSurface();

            if (_previewTexture == null)
            {
                _previewTexture = new RenderTexture(PreviewTextureWidth, PreviewTextureHeight, 16, RenderTextureFormat.ARGB32)
                {
                    name = "MainMenuBrawlerPreviewTexture",
                    antiAliasing = 4
                };
                _previewTexture.Create();
            }

            if (_previewSurface != null)
                _previewSurface.texture = _previewTexture;

            if (_previewStageRoot != null)
                return;

            _previewStageRoot = new GameObject(RuntimePreviewStageName);
            _previewStageRoot.transform.position = new Vector3(0f, -320f - Mathf.Abs(GetInstanceID() % 37), 0f);

            GameObject anchor = new GameObject("PreviewAnchor");
            anchor.transform.SetParent(_previewStageRoot.transform, false);
            anchor.transform.localPosition = Vector3.zero;
            _previewStageAnchor = anchor.transform;

            CreatePreviewCamera(_previewStageRoot.transform);
            CreatePreviewLights(_previewStageRoot.transform);
        }

        private RawImage EnsurePreviewSurface()
        {
            Transform existing = transform.Find(RuntimePreviewSurfaceName);
            RawImage surface = existing != null
                ? existing.GetComponent<RawImage>()
                : null;

            if (surface == null)
            {
                GameObject surfaceObject = new GameObject(RuntimePreviewSurfaceName, typeof(RectTransform), typeof(RawImage));
                surfaceObject.transform.SetParent(transform, false);
                surface = surfaceObject.GetComponent<RawImage>();
            }

            surface.color = Color.white;
            surface.raycastTarget = false;

            RectTransform rect = surface.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-30f, -10f);
            rect.offsetMax = new Vector2(30f, 20f);
            rect.SetAsFirstSibling();

            return surface;
        }

        private void CreatePreviewCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.22f, -4.4f);
            cameraObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCamera.orthographic = true;
            _previewCamera.orthographicSize = 1.72f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 20f;
            _previewCamera.allowHDR = false;
            _previewCamera.targetTexture = _previewTexture;
        }

        private static void CreatePreviewLights(Transform parent)
        {
            GameObject keyObject = new GameObject("PreviewKeyLight");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.localRotation = Quaternion.Euler(42f, -24f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.20f;
            key.color = new Color(1f, 0.93f, 0.82f, 1f);

            GameObject rimObject = new GameObject("PreviewRimLight");
            rimObject.transform.SetParent(parent, false);
            rimObject.transform.localPosition = new Vector3(0.55f, 1.5f, -1.7f);
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.range = 5.5f;
            rim.intensity = 1.05f;
            rim.color = new Color(0.48f, 0.72f, 1f, 1f);
        }

        private Vector3 CalculateGroundedLocalPosition(GameObject root, Transform spawnParent)
        {
            if (root == null || spawnParent == null)
                return Vector3.zero;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            float minWorldY = float.PositiveInfinity;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                minWorldY = Mathf.Min(minWorldY, r.bounds.min.y);
            }

            if (float.IsInfinity(minWorldY))
                return Vector3.zero;

            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(spawnParent.lossyScale.y));
            float minLocalY = (minWorldY - spawnParent.position.y) / scaleY;
            float groundY = _useRenderTexturePreview ? 0f : _previewGroundLocalY;
            return Vector3.up * (groundY - minLocalY);
        }

        private void OnDestroy()
        {
            if (_previewTexture != null)
            {
                _previewTexture.Release();
                Destroy(_previewTexture);
                _previewTexture = null;
            }

            if (_previewStageRoot != null)
            {
                Destroy(_previewStageRoot);
                _previewStageRoot = null;
            }
        }
    }
}
