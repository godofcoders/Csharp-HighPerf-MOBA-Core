using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using MOBA.Core.Definitions;

namespace MOBA.Core.Infrastructure
{
    public class MainMenuBrawlerPreview : MonoBehaviour, IDragHandler, IPointerClickHandler
    {
        [SerializeField] private Transform _modelAnchor;
        [SerializeField] private BrawlerDefinition _fallbackBrawler;

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
        [SerializeField] private float _previewGroundLocalY = -0.45f;

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

        private void Start() => Refresh();

        public bool IsPreviewVisible => _modelAnchor == null || _modelAnchor.gameObject.activeSelf;

        public void SetPreviewVisible(bool visible)
        {
            if (_modelAnchor != null)
                _modelAnchor.gameObject.SetActive(visible);

            if (visible && _spawned == null)
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
            BrawlerDefinition def =
                SceneSelection.SelectedBrawler ?? _fallbackBrawler;

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

            if (!ProceduralBrawlerModelFactory.TryCreate(def, _modelAnchor, null, out _spawned))
            {
                if (def.ModelPrefab == null)
                {
                    Debug.LogWarning("[MMBP] def.ModelPrefab null on " + def.name);
                    return;
                }

                _spawned = Instantiate(def.ModelPrefab, _modelAnchor);
            }

            _baseLocalPosition = Vector3.zero;
            _currentYaw = _initialYaw;
            ApplyPreviewPose();

            _baseLocalPosition = CalculateGroundedLocalPosition(_spawned);
            ApplyPreviewPose();

            StripGameplayComponents(_spawned);
            UpdateInfoText(def);

            _rotationVelocity = 0f;
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

        private Vector3 CalculateGroundedLocalPosition(GameObject root)
        {
            if (root == null || _modelAnchor == null)
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

            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(_modelAnchor.lossyScale.y));
            float minLocalY = (minWorldY - _modelAnchor.position.y) / scaleY;
            return Vector3.up * (_previewGroundLocalY - minLocalY);
        }
    }
}
